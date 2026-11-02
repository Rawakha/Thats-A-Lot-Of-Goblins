using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// The level's flat ground, built as a mesh with a round opening for every Level_Hole over it, each opening carrying on
// down as the hole's inside wall and (optionally) a bottom. Three material slots: ground, walls, bottoms. The mesh
// (and a matching collider) is built in the editor whenever a hole or the ground changes, and saved with the scene, so
// nothing is built at runtime unless it's missing. Replaces a plain ground plane: holes go anywhere, no modelling.
//
// The ground is one flat polygon (the Size rectangle) with every hole cut out, triangulated by Level_Triangulator;
// walls and bottoms are rings and fans on the same circles, so the edges meet exactly. UVs are in metres / Texture
// Size, so textures tile evenly across ground, walls and bottoms.
//
// Holes must sit fully inside the ground and not overlap each other; ones that don't are left out with a warning.
// A level tool: nothing here knows about goblins.
//
// GC: none at runtime (the mesh is built in the editor). No Update and no subscriptions in play.
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Level_Ground : MonoBehaviour
{
    [Header("Shape")]
    [Tooltip("Width (x) and length (z) of the ground, metres, centred on this object.")]
    [SerializeField] private Vector2 size = new Vector2(300f, 300f);

    [Tooltip("Scene handle grid in metres for movement and sizes. Zero allows free dragging.")]
    [SerializeField, Min(0f)] private float snap = 0.25f;

    [Tooltip("Metres one repeat of a texture covers, on the ground, walls and bottoms alike.")]
    [SerializeField, Min(0.01f)] private float textureSize = 4f;

    [Header("Materials")]
    [Tooltip("Empty keeps the renderer's first material.")]
    [SerializeField] private Material groundMaterial;
    [Tooltip("Inside walls of the holes. Empty uses the ground material.")]
    [SerializeField] private Material wallMaterial;
    [Tooltip("Hole bottoms. Empty uses the wall material. Something dark sells the depth.")]
    [SerializeField] private Material bottomMaterial;

    [Header("Physics")]
    [Tooltip("Give the ground a matching MeshCollider (holes open, walls solid), so things can fall in.")]
    [SerializeField] private bool makeCollider = true;

    [Header("Stats")]
    [SerializeField, ReadOnly] private int holes;
    [SerializeField, ReadOnly] private int vertices;
    [SerializeField, ReadOnly] private int triangles;
    [SerializeField, ReadOnly] private float buildMs;

    [SerializeField, HideInInspector] private Mesh mesh;

    public Vector2 Size => size;

    private void Awake()
    {
        // Built and saved in the editor; only build here if it never was (or the scene lost it)
        if (Application.isPlaying && mesh == null)
            Build();
    }

    [InspectorButton("Rebuild")]
    public void Build()
    {
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

        if (mesh == null)
            mesh = new Mesh { name = $"{name} (Level Ground)" };

        List<Vector3> positions = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> groundTriangles = new List<int>();
        List<int> wallTriangles = new List<int>();
        List<int> bottomTriangles = new List<int>();

        List<Level_Hole> cut = CollectHoles();
        List<List<Vector2>> rings = new List<List<Vector2>>(cut.Count);

        // Each hole's circle in the ground's space, anticlockwise; the cut-out uses it backwards (holes go clockwise)
        for (int h = 0; h < cut.Count; h++)
            rings.Add(Circle(cut[h]));

        BuildGround(rings, positions, normals, uvs, groundTriangles);

        for (int h = 0; h < cut.Count; h++)
            BuildHole(cut[h], rings[h], positions, normals, uvs, wallTriangles, bottomTriangles);

        mesh.Clear();
        mesh.indexFormat = positions.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(groundTriangles, 0, false);
        mesh.SetTriangles(wallTriangles, 1, false);
        mesh.SetTriangles(bottomTriangles, 2, false);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        ApplyMaterials();
        ApplyCollider();

        holes = cut.Count;
        vertices = positions.Count;
        triangles = (groundTriangles.Count + wallTriangles.Count + bottomTriangles.Count) / 3;
        buildMs = (float)watch.Elapsed.TotalMilliseconds;

#if UNITY_EDITOR
        builtKey = InputKey();

        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    // Every enabled hole whose opening fits inside this ground without touching another one
    private List<Level_Hole> CollectHoles()
    {
        List<Level_Hole> cut = new List<Level_Hole>();
        Vector2 half = size * 0.5f;

        foreach (Level_Hole hole in FindObjectsByType<Level_Hole>(FindObjectsInactive.Exclude))
        {
            if (!hole.isActiveAndEnabled)
                continue;

            Vector2 centre = Local(hole);
            float r = hole.Radius;

            // Not over this ground at all: some other ground's (or none)
            if (Mathf.Abs(centre.x) > half.x || Mathf.Abs(centre.y) > half.y)
                continue;

            if (Mathf.Abs(centre.x) + r >= half.x || Mathf.Abs(centre.y) + r >= half.y)
            {
                Debug.LogWarning($"{hole.name}: the hole runs off the edge of {name}; left out.", hole);
                continue;
            }

            Level_Hole clash = null;

            for (int i = 0; i < cut.Count && clash == null; i++)
            {
                if (Vector2.Distance(centre, Local(cut[i])) < r + cut[i].Radius + 0.01f)
                    clash = cut[i];
            }

            if (clash != null)
            {
                Debug.LogWarning($"{hole.name}: the hole overlaps {clash.name}; left out. Move them apart.", hole);
                continue;
            }

            cut.Add(hole);
        }

        return cut;
    }

    // The ground: the rectangle with every hole cut out, facing up
    private void BuildGround(List<List<Vector2>> rings, List<Vector3> positions, List<Vector3> normals,
        List<Vector2> uvs, List<int> output)
    {
        Vector2 half = size * 0.5f;
        List<Vector2> outline = new List<Vector2>
        {
            new Vector2(-half.x, -half.y),
            new Vector2(half.x, -half.y),
            new Vector2(half.x, half.y),
            new Vector2(-half.x, half.y)
        };

        List<List<Vector2>> cutOuts = new List<List<Vector2>>(rings.Count);

        for (int h = 0; h < rings.Count; h++)
        {
            List<Vector2> reversed = new List<Vector2>(rings[h]);
            reversed.Reverse();
            cutOuts.Add(reversed);
        }

        List<Vector2> points = new List<Vector2>();
        List<int> flat = Level_Triangulator.Triangulate(outline, cutOuts, points);
        int first = positions.Count;

        for (int i = 0; i < points.Count; i++)
        {
            positions.Add(new Vector3(points[i].x, 0f, points[i].y));
            normals.Add(Vector3.up);
            uvs.Add(points[i] / textureSize);
        }

        for (int t = 0; t < flat.Count; t += 3)
            AddTriangle(output, positions, first + flat[t], first + flat[t + 1], first + flat[t + 2], Vector3.up);
    }

    // The inside wall from the opening down to the depth, facing in, and the bottom facing up
    private void BuildHole(Level_Hole hole, List<Vector2> ring, List<Vector3> positions, List<Vector3> normals,
        List<Vector2> uvs, List<int> walls, List<int> bottoms)
    {
        int segments = ring.Count;
        float depth = hole.Depth;
        Vector2 centre = Local(hole);
        float circumference = 2f * Mathf.PI * hole.Radius;
        int wallStart = positions.Count;

        // One more column than segments, so the texture wraps round without a seam squeeze
        for (int k = 0; k <= segments; k++)
        {
            Vector2 p = ring[k % segments];
            Vector3 inward = new Vector3(centre.x - p.x, 0f, centre.y - p.y).normalized;
            float u = circumference * k / segments / textureSize;

            positions.Add(new Vector3(p.x, 0f, p.y));
            normals.Add(inward);
            uvs.Add(new Vector2(u, 0f));

            positions.Add(new Vector3(p.x, -depth, p.y));
            normals.Add(inward);
            uvs.Add(new Vector2(u, -depth / textureSize));
        }

        for (int k = 0; k < segments; k++)
        {
            int top = wallStart + k * 2;
            int low = top + 1;
            int nextTop = top + 2;
            int nextLow = top + 3;
            Vector3 inward = normals[top] + normals[nextTop];

            AddTriangle(walls, positions, top, nextTop, low, inward);
            AddTriangle(walls, positions, nextTop, nextLow, low, inward);
        }

        if (!hole.HasBottom)
            return;

        int middle = positions.Count;
        positions.Add(new Vector3(centre.x, -depth, centre.y));
        normals.Add(Vector3.up);
        uvs.Add(centre / textureSize);

        for (int k = 0; k < segments; k++)
        {
            positions.Add(new Vector3(ring[k].x, -depth, ring[k].y));
            normals.Add(Vector3.up);
            uvs.Add(ring[k] / textureSize);
        }

        for (int k = 0; k < segments; k++)
            AddTriangle(bottoms, positions, middle, middle + 1 + k, middle + 1 + (k + 1) % segments, Vector3.up);
    }

    // A hole's circle in the ground's space (x, z), anticlockwise
    private List<Vector2> Circle(Level_Hole hole)
    {
        Vector2 centre = Local(hole);
        int segments = hole.Segments;
        List<Vector2> ring = new List<Vector2>(segments);

        for (int k = 0; k < segments; k++)
        {
            float angle = 2f * Mathf.PI * k / segments;
            ring.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * hole.Radius);
        }

        return ring;
    }

    private Vector2 Local(Level_Hole hole)
    {
        Vector3 local = transform.InverseTransformPoint(hole.transform.position);
        return new Vector2(local.x, local.z);
    }

    // Adds a triangle wound so it faces the given way (Unity's front face), whichever order the corners came in
    private static void AddTriangle(List<int> output, List<Vector3> positions, int a, int b, int c, Vector3 facing)
    {
        Vector3 normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);

        output.Add(a);

        if (Vector3.Dot(normal, facing) >= 0f)
        {
            output.Add(b);
            output.Add(c);
        }
        else
        {
            output.Add(c);
            output.Add(b);
        }
    }

    private void ApplyMaterials()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        Material ground = groundMaterial != null ? groundMaterial : meshRenderer.sharedMaterial;
        Material wall = wallMaterial != null ? wallMaterial : ground;
        Material floor = bottomMaterial != null ? bottomMaterial : wall;
        meshRenderer.sharedMaterials = new[] { ground, wall, floor };
    }

    private void ApplyCollider()
    {
        TryGetComponent(out MeshCollider meshCollider);

        if (!makeCollider)
        {
            if (meshCollider != null)
                meshCollider.enabled = false;

            return;
        }

        if (meshCollider == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();

        meshCollider.enabled = true;
        meshCollider.sharedMesh = null;     // forces the collider to take the rebuilt mesh
        meshCollider.sharedMesh = mesh;
    }

#if UNITY_EDITOR
    #region Editor rebuilds
    private static bool rebuildQueued;
    private static int handleEdits;
    private static readonly List<Transform> watched = new List<Transform>();
    private static bool watchListStale = true;

    // Custom Scene handles preview with outlines; defer mesh/collider work until all drags finish.
    public static void BeginHandleEdit() => handleEdits++;

    public static void EndHandleEdit()
    {
        handleEdits = Mathf.Max(0, handleEdits - 1);
        RequestRebuild();
    }

    // Rebuilds every ground on the next editor tick (several changes in one tick make one rebuild). Never in play.
    public static void RequestRebuild()
    {
        if (rebuildQueued || handleEdits > 0 || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        rebuildQueued = true;
        EditorApplication.delayCall += RebuildAll;
    }

    private static void RebuildAll()
    {
        rebuildQueued = false;

        if (handleEdits > 0 || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        foreach (Level_Ground ground in FindObjectsByType<Level_Ground>(FindObjectsInactive.Exclude))
            ground.BuildIfChanged();
    }

    // What the mesh was last built from (see Level_BuildKey): this ground's settings and every hole's, with where the
    // hole sits on it. Not saved, so an undo or reload never makes it lie.
    [System.NonSerialized] private string builtKey;

    private string InputKey()
    {
        System.Text.StringBuilder holeKey = new System.Text.StringBuilder();

        foreach (Level_Hole hole in FindObjectsByType<Level_Hole>(FindObjectsInactive.Exclude))
        {
            if (!hole.isActiveAndEnabled)
                continue;

            Vector2 centre = Local(hole);
            holeKey.Append(centre.x.ToString("F3")).Append(',').Append(centre.y.ToString("F3")).Append(':');
            holeKey.Append(EditorJsonUtility.ToJson(hole)).Append(';');
        }

        return Level_BuildKey.Of(this, holeKey.ToString(), "holes", "vertices", "triangles", "buildMs", "mesh");
    }

    // Rebuilds only if this ground or a hole changed since the last build. Straight after a load there's no key yet:
    // the saved mesh was built from the saved inputs, so they're adopted instead of rebuilding (which would dirty
    // the scene on every script reload, scene open and return from play).
    private void BuildIfChanged()
    {
        string key = InputKey();

        if (builtKey == null && mesh != null)
        {
            builtKey = key;
            return;
        }

        if (key != builtKey)
            Build();
    }

    private void OnValidate()
    {
        size.x = Mathf.Max(0.5f, size.x);
        size.y = Mathf.Max(0.5f, size.y);
        snap = Mathf.Max(0f, snap);
        RequestRebuild();
    }

    // Moving a hole or the ground in the Scene view doesn't call OnValidate, so the editor watches their transforms
    [InitializeOnLoadMethod]
    private static void Watch()
    {
        EditorApplication.update += CheckMoved;
        Undo.undoRedoPerformed += RequestRebuild;
        EditorApplication.hierarchyChanged += () => watchListStale = true;
    }

    private static void CheckMoved()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (watchListStale)
        {
            watchListStale = false;
            watched.Clear();

            foreach (Level_Hole hole in FindObjectsByType<Level_Hole>(FindObjectsInactive.Exclude))
                watched.Add(hole.transform);

            foreach (Level_Ground ground in FindObjectsByType<Level_Ground>(FindObjectsInactive.Exclude))
                watched.Add(ground.transform);
        }

        bool moved = false;

        for (int i = 0; i < watched.Count; i++)
        {
            Transform t = watched[i];

            if (t != null && t.hasChanged)
            {
                t.hasChanged = false;
                moved = true;
            }
        }

        if (moved)
            RequestRebuild();
    }
    #endregion
#endif
}
