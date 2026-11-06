using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Painted grass: clumps of chunky, squarish blades you paint onto the ground and cliff tops with a brush in the Scene
// view (select the object, press Paint in its inspector; Shift erases). Like Cube & Seek's grass: thin rectangular
// slabs, a little tapered and leaning, with slanted tops, coloured from a dark base to bright tips, swaying in the wind.
//
// Built for huge counts: no GameObjects. Every clump is one instance of a small generated mesh, drawn with GPU
// instancing ("Goblins/Grass" shader, which also does the colour and the wind) in spatial chunks of up to 1023, and
// chunks outside a camera's view are skipped. Colours and wind live on the material, so change them there (or make a
// second material for a different-coloured patch on another Level_Grass).
//
// The painted clumps are stored on this component (positions in its own space, so you can move it), packed into one
// byte array so the scene file stays small. Paint lower grass (Height) where goblins walk so it never hides them.
// A level tool: nothing here knows about goblins.
//
// Setup: GameObject > Level > Grass, then make a material on Goblins/Grass and assign it (purple by default).
//
// GC: none per frame. Loading and every paint stroke rebuild the chunk arrays (editor, or once on enable). One
// subscription to RenderPipelineManager.beginCameraRendering while enabled (draws for each camera, edit mode too).
[ExecuteAlways]
[DisallowMultipleComponent]
public class Level_Grass : MonoBehaviour
{
    private const int FloatsPerClump = 6;   // x, y, z (local), yaw, size, height
    private const int Batch = 1023;

    private class Chunk
    {
        public Matrix4x4[] Matrices;
        public int Count;
        public Bounds Bounds;
    }

    [SerializeField] private Material material;

    [Header("Clump Shape")]
    [Tooltip("Blades in each clump.")]
    [SerializeField, Range(1, 16)] private int bladesPerClump = 7;

    [Tooltip("How far blades spread from the clump's centre, metres.")]
    [SerializeField, Min(0f)] private float clumpRadius = 0.22f;

    [Tooltip("Blade width and thickness, metres: squarish slabs, a bit wider than thick.")]
    [SerializeField] private Vector2 bladeSize = new Vector2(0.07f, 0.035f);

    [Tooltip("Blade heights, metres (random between).")]
    [SerializeField] private Vector2 bladeHeight = new Vector2(0.35f, 0.8f);

    [Tooltip("How much narrower blades are at the top than the root (0 none, 0.8 almost a point).")]
    [SerializeField, Range(0f, 0.8f)] private float taper = 0.3f;

    [Tooltip("Most a blade leans outward from the clump, degrees.")]
    [SerializeField, Range(0f, 45f)] private float lean = 14f;

    [Tooltip("Slant of the cut across each blade's top, as a share of its width.")]
    [SerializeField, Range(0f, 2f)] private float topSlant = 0.8f;

    [Tooltip("Change for differently shaped clumps.")]
    [SerializeField] private int shapeSeed = 1;

    [Header("Drawing")]
    [Tooltip("Size of the chunks clumps are grouped into for skipping off-screen grass, metres.")]
    [SerializeField, Min(2f)] private float chunkSize = 16f;

    [Header("Brush")]
    [Tooltip("Brush radius, metres. In the Scene view, [ and ] change it too.")]
    [SerializeField, Min(0.1f)] private float brushRadius = 3f;

    [Tooltip("Clumps per square metre the brush fills to.")]
    [SerializeField, Range(0.1f, 30f)] private float density = 6f;

    [Tooltip("Clump size, as a multiple (random between).")]
    [SerializeField] private Vector2 size = new Vector2(0.85f, 1.2f);

    [Tooltip("Clump height, as a multiple (random between). Paint lower grass where goblins walk.")]
    [SerializeField] private Vector2 height = new Vector2(0.8f, 1.2f);

    [Tooltip("Surfaces the brush paints on (the ground and cliffs).")]
    [SerializeField] private LayerMask paintLayers = ~0;

    [Tooltip("Only paints on surfaces at least this flat (1 flat only, 0 walls too), so cliff sides stay bare.")]
    [SerializeField, Range(0f, 1f)] private float flatness = 0.8f;

    [Tooltip("Most clumps this object holds, so a heavy brush can't freeze the editor.")]
    [SerializeField, Min(1)] private int maxClumps = 60000;

    [SerializeField, HideInInspector] private byte[] packed = Array.Empty<byte>();

    private readonly List<Chunk> chunks = new List<Chunk>();
    private static readonly Plane[] frustum = new Plane[6];
    private float[] clumps = Array.Empty<float>();
    private int clumpCount;
    private Mesh mesh;
    private RenderParams renderParams;
    private bool subscribed;

    public float BrushRadius { get => brushRadius; set => brushRadius = Mathf.Max(0.1f, value); }
    public float Density => density;
    public LayerMask PaintLayers => paintLayers;
    public float Flatness => flatness;
    public int ClumpCount => clumpCount;
    public int MaxClumps => maxClumps;

    private void OnEnable()
    {
        Load();
        BuildMesh();
        RebuildChunks();

        if (!subscribed)
        {
            RenderPipelineManager.beginCameraRendering += Render;
            subscribed = true;
        }
    }

    private void OnDisable()
    {
        if (subscribed)
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            subscribed = false;
        }
    }

    private void OnDestroy()
    {
        if (mesh != null)
            DestroyImmediate(mesh);
    }

    private void OnValidate()
    {
        // Shape changes rebuild the clump mesh; moving the object needs no rebuild (matrices are re-made on enable)
        if (isActiveAndEnabled)
        {
            BuildMesh();
            RebuildChunks();
        }
    }

    // Each camera, before it renders: every chunk it can see, instanced
    private void Render(ScriptableRenderContext context, Camera camera)
    {
        if (material == null || mesh == null || chunks.Count == 0 || camera.cameraType == CameraType.Preview)
            return;

        GeometryUtility.CalculateFrustumPlanes(camera, frustum);
        renderParams.camera = camera;

        for (int i = 0; i < chunks.Count; i++)
        {
            Chunk chunk = chunks[i];

            if (GeometryUtility.TestPlanesAABB(frustum, chunk.Bounds))
                Graphics.RenderMeshInstanced(renderParams, mesh, 0, chunk.Matrices, chunk.Count);
        }
    }

    #region Data

    private void Load()
    {
        int floats = packed != null ? packed.Length / sizeof(float) : 0;
        clumpCount = floats / FloatsPerClump;
        clumps = new float[Mathf.Max(clumpCount * FloatsPerClump, 64)];

        if (clumpCount > 0)
            Buffer.BlockCopy(packed, 0, clumps, 0, clumpCount * FloatsPerClump * sizeof(float));
    }

    private void Save()
    {
        int bytes = clumpCount * FloatsPerClump * sizeof(float);
        packed = new byte[bytes];

        if (bytes > 0)
            Buffer.BlockCopy(clumps, 0, packed, 0, bytes);
    }

    // A clump standing at a world point, with the brush's random size and height. False once full.
    public bool AddClump(Vector3 world, System.Random random)
    {
        if (clumpCount >= maxClumps)
            return false;

        if ((clumpCount + 1) * FloatsPerClump > clumps.Length)
            Array.Resize(ref clumps, Mathf.Max(clumps.Length * 2, (clumpCount + 1) * FloatsPerClump));

        Vector3 local = transform.InverseTransformPoint(world);
        int at = clumpCount * FloatsPerClump;
        clumps[at] = local.x;
        clumps[at + 1] = local.y;
        clumps[at + 2] = local.z;
        clumps[at + 3] = (float)random.NextDouble() * 360f;
        clumps[at + 4] = Mathf.Lerp(size.x, size.y, (float)random.NextDouble());
        clumps[at + 5] = Mathf.Lerp(height.x, height.y, (float)random.NextDouble());
        clumpCount++;
        return true;
    }

    // Clumps within a flat radius of a world point
    public int CountWithin(Vector3 world, float radius)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        float radiusSqr = radius * radius;
        int found = 0;

        for (int i = 0; i < clumpCount; i++)
        {
            int at = i * FloatsPerClump;
            float dx = clumps[at] - local.x;
            float dz = clumps[at + 2] - local.z;

            if (dx * dx + dz * dz <= radiusSqr)
                found++;
        }

        return found;
    }

    // Removes clumps within a flat radius of a world point (each with the given chance, for a soft eraser)
    public int RemoveWithin(Vector3 world, float radius, float chance, System.Random random)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        float radiusSqr = radius * radius;
        int removed = 0;

        for (int i = clumpCount - 1; i >= 0; i--)
        {
            int at = i * FloatsPerClump;
            float dx = clumps[at] - local.x;
            float dz = clumps[at + 2] - local.z;

            if (dx * dx + dz * dz > radiusSqr || (chance < 1f && random.NextDouble() > chance))
                continue;

            // Swap-back: the last clump moves into this slot
            int last = (clumpCount - 1) * FloatsPerClump;
            Array.Copy(clumps, last, clumps, at, FloatsPerClump);
            clumpCount--;
            removed++;
        }

        return removed;
    }

    // The stored clumps loaded again (after an undo) and redrawn
    public void Reload()
    {
        Load();
        RebuildChunks();
    }

    public void ClearAll()
    {
        clumpCount = 0;
    }

    // After painting: store the clumps and remake what's drawn
    public void Commit()
    {
        Save();
        RebuildChunks();
    }

    #endregion

    #region Drawing

    // Groups the clumps into chunks by where they stand, each a ready-to-draw matrix array (split past 1023)
    public void RebuildChunks()
    {
        chunks.Clear();

        if (material != null)
        {
            material.enableInstancing = true;
            renderParams = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
        }

        if (clumpCount == 0)
            return;

        Dictionary<Vector2Int, List<Matrix4x4>> cells = new Dictionary<Vector2Int, List<Matrix4x4>>();
        Matrix4x4 toWorld = transform.localToWorldMatrix;

        for (int i = 0; i < clumpCount; i++)
        {
            int at = i * FloatsPerClump;
            Vector3 world = toWorld.MultiplyPoint3x4(new Vector3(clumps[at], clumps[at + 1], clumps[at + 2]));
            float s = clumps[at + 4];
            Matrix4x4 matrix = Matrix4x4.TRS(world, Quaternion.Euler(0f, clumps[at + 3], 0f), new Vector3(s, s * clumps[at + 5], s));

            Vector2Int cell = new Vector2Int(Mathf.FloorToInt(world.x / chunkSize), Mathf.FloorToInt(world.z / chunkSize));

            if (!cells.TryGetValue(cell, out List<Matrix4x4> list))
            {
                list = new List<Matrix4x4>(256);
                cells.Add(cell, list);
            }

            list.Add(matrix);
        }

        float reach = bladeHeight.y * Mathf.Max(size.y, 1f) * Mathf.Max(height.y, 1f) + clumpRadius + 1f;

        foreach (List<Matrix4x4> list in cells.Values)
        {
            for (int start = 0; start < list.Count; start += Batch)
            {
                int count = Mathf.Min(Batch, list.Count - start);
                Chunk chunk = new Chunk { Matrices = new Matrix4x4[count], Count = count };
                Bounds bounds = new Bounds(list[start].GetColumn(3), Vector3.zero);

                for (int i = 0; i < count; i++)
                {
                    chunk.Matrices[i] = list[start + i];
                    bounds.Encapsulate((Vector3)list[start + i].GetColumn(3));
                }

                bounds.Expand(reach * 2f);
                chunk.Bounds = bounds;
                chunks.Add(chunk);
            }
        }
    }

    // One clump: blades as thin rectangular slabs, a little tapered, leaning out from the middle, with a slanted cut
    // on top. Every face has its own vertices (flat shading). UV.y is the height up the blade (0 root, 1 top), which
    // the shader uses for the colour gradient and the wind.
    private void BuildMesh()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "Grass Clump", hideFlags = HideFlags.HideAndDontSave };
        }

        System.Random random = new System.Random(shapeSeed);
        List<Vector3> vertices = new List<Vector3>(bladesPerClump * 20);
        List<Vector3> normals = new List<Vector3>(bladesPerClump * 20);
        List<Vector2> uvs = new List<Vector2>(bladesPerClump * 20);
        List<int> triangles = new List<int>(bladesPerClump * 30);

        for (int b = 0; b < bladesPerClump; b++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Sqrt((float)random.NextDouble()) * clumpRadius;
            Vector3 root = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            float bladeH = Mathf.Lerp(bladeHeight.x, bladeHeight.y, (float)random.NextDouble());
            float turn = (float)random.NextDouble() * 360f;

            // Lean outward from the clump's middle (straight up in the middle)
            Vector3 outward = distance > 0.001f ? root / distance : Vector3.forward;
            float leanAngle = lean * (0.3f + 0.7f * (distance / Mathf.Max(clumpRadius, 0.001f))) * (0.6f + 0.4f * (float)random.NextDouble());
            Quaternion rotation = Quaternion.AngleAxis(leanAngle, Vector3.Cross(Vector3.up, outward)) * Quaternion.Euler(0f, turn, 0f);

            AddBlade(vertices, normals, uvs, triangles, root, rotation, bladeH, (float)random.NextDouble() < 0.5f ? 1f : -1f);
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    private void AddBlade(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
        Vector3 root, Quaternion rotation, float bladeH, float slantSide)
    {
        float w = bladeSize.x * 0.5f;
        float t = bladeSize.y * 0.5f;
        float topW = w * (1f - taper);
        float topT = t * (1f - taper);
        float slant = topSlant * bladeSize.x * 0.5f * slantSide;

        // Bottom corners, then top corners (the top cut slants across the width)
        Vector3[] c =
        {
            new Vector3(-w, 0f, -t), new Vector3(w, 0f, -t), new Vector3(w, 0f, t), new Vector3(-w, 0f, t),
            new Vector3(-topW, bladeH - slant, -topT), new Vector3(topW, bladeH + slant, -topT),
            new Vector3(topW, bladeH + slant, topT), new Vector3(-topW, bladeH - slant, topT)
        };

        for (int i = 0; i < c.Length; i++)
            c[i] = root + rotation * c[i];

        float top = bladeH + Mathf.Abs(slant);
        Vector3 centre = root + rotation * new Vector3(0f, bladeH * 0.5f, 0f);

        // Four sides and the top, each its own quad facing away from the blade's middle
        Quad(vertices, normals, uvs, triangles, c[0], c[1], c[5], c[4], top, centre);
        Quad(vertices, normals, uvs, triangles, c[1], c[2], c[6], c[5], top, centre);
        Quad(vertices, normals, uvs, triangles, c[2], c[3], c[7], c[6], top, centre);
        Quad(vertices, normals, uvs, triangles, c[3], c[0], c[4], c[7], top, centre);
        Quad(vertices, normals, uvs, triangles, c[4], c[5], c[6], c[7], top, centre);
    }

    // A flat quad (corners a, b, c, d in order round it) facing away from 'inside', its UV.y the height up the blade.
    // Unity's front faces wind clockwise: a triangle (p0, p1, p2) faces along Cross(p1 - p0, p2 - p0).
    private static void Quad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, float top, Vector3 inside)
    {
        int start = vertices.Count;
        Vector3 outward = (a + b + c + d) * 0.25f - inside;
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        bool flip = Vector3.Dot(normal, outward) < 0f;

        if (flip)
            normal = -normal;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);

        for (int i = 0; i < 4; i++)
            normals.Add(normal);

        uvs.Add(new Vector2(0f, Mathf.Clamp01(a.y / top)));
        uvs.Add(new Vector2(1f, Mathf.Clamp01(b.y / top)));
        uvs.Add(new Vector2(1f, Mathf.Clamp01(c.y / top)));
        uvs.Add(new Vector2(0f, Mathf.Clamp01(d.y / top)));

        // (a, b, c) faces along Cross(b - a, c - a); keep that order when it points out, reverse it when it doesn't
        if (!flip)
        {
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        else
        {
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }
    }

    #endregion

    [InspectorButton("Clear All Grass")]
    private void ClearButton()
    {
        ClearAll();
        Commit();
    }
}
