using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Destruct_FractureCore;
using UnityEditor;
#endif

// One baked gib: a chunk of a goblin's body or head, its mesh centred on its own middle (so it spins about that), and
// where that middle sat in the part it came from (so it can start exactly where it was on the goblin)
[Serializable]
public struct Horde_Gib
{
    public Mesh mesh;
    public Vector3 centre;
}

// The pieces a goblin comes apart into: its Body and Head cut into a few chunks each (Voronoi cells, like the
// destruction fracture), then rounded off so they read as soft, fleshy lumps instead of glassy shards. The cut faces
// are marked in the vertex colours (red = 1), so the gib shader draws them in gore purple while the outside keeps the
// goblin's skin colour. Cuts are uneven (Unevenness): a big piece and small scraps, which reads as torn rather than
// shattered. Chunks are sorted biggest first. The whole head is baked too, for heads popping off in one piece, with
// X eyes built into it (green in the vertex colours) at the prefab's Eye_Left and Eye_Right.
//
// Setup: Create > Goblins/Gib Set, drag in the goblin prefab, set the piece counts and press Bake. The meshes are
// saved inside this asset; Bake again after changing the goblin model or the settings.
//
// GC: none at runtime (read-only data). Baking is editor-only.
[CreateAssetMenu(menuName = "Goblins/Gib Set", fileName = "Horde_GibSet")]
public class Horde_GibSet : ScriptableObject
{
    [Header("Source")]
    [Tooltip("The goblin prefab whose Body and Head are cut up.")]
    public GameObject goblinPrefab;

    [Header("Cutting")]
    [Tooltip("Chunks the body is cut into. Few and big reads best from the plane.")]
    [Range(2, 12)] public int bodyPieces = 4;

    [Tooltip("Chunks the head is cut into.")]
    [Range(2, 8)] public int headPieces = 3;

    [Tooltip("How uneven the cuts are: 0 is similar-sized pieces, 1 is one big piece and small scraps torn from one spot.")]
    [Range(0f, 1f)] public float unevenness = 0.7f;

    [Tooltip("How round the chunks are made after cutting: 0 keeps the sharp cuts, 1 is a soft blob.")]
    [Range(0f, 1f)] public float roundness = 0.55f;

    [Tooltip("Times each chunk's triangles are split before rounding. A cut face is only a few big triangles, so it can't curve without these. More is smoother but heavier.")]
    [Range(0, 4)] public int subdivisions = 2;

    [Tooltip("Passes that average each point with its neighbours, melting the sharp edges away.")]
    [Range(0, 30)] public int smoothing = 8;

    [Tooltip("Stops splitting a chunk once it has this many triangles. Gibs are drawn by the hundred, so keep it low.")]
    [Min(12)] public int maxTriangles = 600;

    [Tooltip("Change for differently cut chunks.")]
    public int seed = 1;

    [Header("Baked")]
    public Horde_Gib[] bodyChunks = Array.Empty<Horde_Gib>();
    public Horde_Gib[] headChunks = Array.Empty<Horde_Gib>();

    [Tooltip("The head in one piece with X eyes, centred on its middle, for heads popping off whole.")]
    public Mesh wholeHead;

    [Tooltip("Where the whole head's middle sits in the head part.")]
    public Vector3 wholeHeadCentre;

    [Header("X Eyes")]
    [Tooltip("Size of each X on a popped head, as a share of the head's size.")]
    [Range(0f, 0.5f)] public float eyeSize = 0.16f;

    [Tooltip("Thickness of the X's strokes, as a share of the X.")]
    [Range(0.05f, 0.6f)] public float eyeStroke = 0.22f;

#if UNITY_EDITOR
    [InspectorButton("Bake")]
    private void Bake()
    {
        if (goblinPrefab == null)
        {
            Debug.LogError($"{name}: drag the goblin prefab into Goblin Prefab first.", this);
            return;
        }

        Horde_Goblin.FindParts(goblinPrefab, out Transform body, out Transform head);
        MeshFilter bodyFilter = body != null ? body.GetComponentInChildren<MeshFilter>() : null;
        MeshFilter headFilter = head != null ? head.GetComponentInChildren<MeshFilter>() : null;

        if (bodyFilter == null || headFilter == null || bodyFilter.sharedMesh == null || headFilter.sharedMesh == null)
        {
            Debug.LogError($"{name}: couldn't find meshes on the prefab's Body and Head.", this);
            return;
        }

        // Old meshes out of the asset
        foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(this)))
        {
            if (sub is Mesh)
                DestroyImmediate(sub, true);
        }

        bodyChunks = Cut(bodyFilter, body, bodyPieces, seed, "Body");
        headChunks = Cut(headFilter, head, headPieces, seed + 101, "Head");
        wholeHead = BakeWholeHead(headFilter, head, out wholeHeadCentre);
        AssetDatabase.AddObjectToAsset(wholeHead, this);

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Debug.Log($"{name}: baked {bodyChunks.Length} body and {headChunks.Length} head chunks.", this);
    }

    // A mesh's points and normals in its anchor's space (the mesh may sit on a child of Body or Head)
    private static void InAnchorSpace(MeshFilter filter, Transform anchor, out Vector3[] vertices, out Vector3[] normals)
    {
        Mesh source = filter.sharedMesh;
        Matrix4x4 toAnchor = anchor.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        vertices = source.vertices;
        normals = source.normals;

        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = toAnchor.MultiplyPoint3x4(vertices[i]);

        for (int i = 0; i < normals.Length; i++)
            normals[i] = toAnchor.MultiplyVector(normals[i]).normalized;
    }

    private Horde_Gib[] Cut(MeshFilter filter, Transform anchor, int pieces, int cutSeed, string label)
    {
        Mesh source = filter.sharedMesh;
        InAnchorSpace(filter, anchor, out Vector3[] anchorVertices, out Vector3[] anchorNormals);

        Bounds bounds = new Bounds(anchorVertices[0], Vector3.zero);

        foreach (Vector3 vertex in anchorVertices)
            bounds.Encapsulate(vertex);

        int nextPid = 0;
        Vector3 size = bounds.size;
        float meshSize = Mathf.Max(size.x, Mathf.Max(size.y, size.z));

        int[][] triangles = new int[source.subMeshCount][];

        for (int s = 0; s < source.subMeshCount; s++)
            triangles[s] = source.GetTriangles(s);

        Tolerances tolerances = new Tolerances(meshSize);
        Piece whole = Piece.FromArrays(anchorVertices, anchorNormals, source.uv, triangles, tolerances.weld, ref nextPid);

        // Uneven: most of the cuts cluster round one spot high on the part, leaving a big piece and small scraps
        System.Random random = new System.Random(cutSeed);
        Vector3 spot = bounds.center + Vector3.Scale(size, new Vector3(
            ((float)random.NextDouble() - 0.5f) * 0.6f, 0.3f, ((float)random.NextDouble() - 0.5f) * 0.6f));

        FractureParams settings = new FractureParams
        {
            count = pieces,
            mode = unevenness > 0f ? PointMode.Impact : PointMode.Uniform,
            impactPoint = spot,
            impactRadius = meshSize * Mathf.Lerp(1f, 0.35f, unevenness),
            impactConcentration = Mathf.Lerp(1f, 2.5f, unevenness),
            uniformFill = 0.25f,
            seed = cutSeed,
            splitIslands = false,
            tolerances = tolerances
        };

        List<Piece> cut = FractureGeometry.Fracture(whole, settings, out _);
        List<Horde_Gib> gibs = new List<Horde_Gib>();

        if (cut == null)
            return gibs.ToArray();

        for (int i = 0; i < cut.Count; i++)
        {
            if (cut[i].IsEmpty)
                continue;

            Mesh mesh = Round(cut[i], out Vector3 centre);
            mesh.name = $"{label} Chunk {gibs.Count}";
            AssetDatabase.AddObjectToAsset(mesh, this);
            gibs.Add(new Horde_Gib { mesh = mesh, centre = centre });
        }

        // Biggest first: scraps are the ones torn off a goblin that stays standing
        gibs.Sort((a, b) => Volume(b.mesh).CompareTo(Volume(a.mesh)));
        return gibs.ToArray();
    }

    private static float Volume(Mesh mesh)
    {
        Vector3 size = mesh.bounds.size;
        return size.x * size.y * size.z;
    }

    // The head whole, centred on its middle, with an X at each eye
    private Mesh BakeWholeHead(MeshFilter filter, Transform anchor, out Vector3 centre)
    {
        Mesh source = filter.sharedMesh;
        InAnchorSpace(filter, anchor, out Vector3[] anchorVertices, out Vector3[] anchorNormals);

        Bounds bounds = new Bounds(anchorVertices[0], Vector3.zero);

        foreach (Vector3 vertex in anchorVertices)
            bounds.Encapsulate(vertex);

        centre = bounds.center;

        List<Vector3> vertices = new List<Vector3>(anchorVertices.Length + 64);
        List<Vector3> normals = new List<Vector3>(anchorNormals);
        List<Color> colours = new List<Color>(anchorVertices.Length + 64);
        List<int> triangles = new List<int>(source.triangles);

        foreach (Vector3 vertex in anchorVertices)
        {
            vertices.Add(vertex - centre);
            colours.Add(Color.black);
        }

        float headSize = Mathf.Max(bounds.size.x, bounds.size.y);

        foreach (Transform child in goblinPrefab.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "Eye_Left" && child.name != "Eye_Right")
                continue;

            Vector3 at = anchor.InverseTransformPoint(child.position) - centre;
            Vector3 front = anchor.InverseTransformDirection(child.forward).normalized;

            // Whichever way the card faces, the X goes on the outside of the head
            if (Vector3.Dot(front, at) < 0f)
                front = -front;

            AddX(vertices, normals, colours, triangles, at + front * headSize * 0.015f, front, headSize * eyeSize);
        }

        Mesh mesh = new Mesh { name = "Head Whole" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetColors(colours);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Two crossed strokes facing 'front', green in the vertex colours (the gib shader draws those dark)
    private void AddX(List<Vector3> vertices, List<Vector3> normals, List<Color> colours, List<int> triangles, Vector3 at, Vector3 front, float size)
    {
        Vector3 right = Vector3.Cross(Vector3.up, front).normalized;
        Vector3 up = Vector3.Cross(front, right).normalized;
        Vector3 one = (right + up).normalized;
        Vector3 two = (right - up).normalized;

        AddStroke(vertices, normals, colours, triangles, at, one, two, front, size);
        AddStroke(vertices, normals, colours, triangles, at, two, one, front, size);
    }

    private void AddStroke(List<Vector3> vertices, List<Vector3> normals, List<Color> colours, List<int> triangles, Vector3 at,
        Vector3 along, Vector3 across, Vector3 front, float size)
    {
        int start = vertices.Count;
        Vector3 length = along * size * 0.5f;
        Vector3 width = across * size * eyeStroke * 0.5f;

        vertices.Add(at - length - width);
        vertices.Add(at + length - width);
        vertices.Add(at + length + width);
        vertices.Add(at - length + width);

        for (int i = 0; i < 4; i++)
        {
            normals.Add(front);
            colours.Add(Color.green);
        }

        // Both windings, so it shows whichever way round it came out
        triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
    }

    // One cut piece as a soft lump: welded, its triangles split so flat cut faces have points to bend, smoothed, pulled
    // toward a ball (Roundness) and smooth-shaded, centred on its middle. The cut faces stay marked red in the vertex
    // colours (for the purple insides); points on the seam are doubled so that colour edge stays sharp.
    private Mesh Round(Piece piece, out Vector3 centre)
    {
        // Weld: one point per position id
        Dictionary<int, int> pointOf = new Dictionary<int, int>();
        List<Vector3> points = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<bool> cutFace = new List<bool>();

        for (int s = 0; s < piece.tris.Length; s++)
        {
            List<int> list = piece.tris[s];

            for (int t = 0; t < list.Count; t++)
            {
                int vertex = list[t];

                if (!pointOf.TryGetValue(piece.pid[vertex], out int point))
                {
                    point = points.Count;
                    points.Add(piece.pos[vertex]);
                    pointOf.Add(piece.pid[vertex], point);
                }

                triangles.Add(point);

                if (t % 3 == 0)
                    cutFace.Add(s == piece.InteriorSubMesh);
            }
        }

        // Split every triangle into four, sharing the new mid-edge points
        for (int level = 0; level < subdivisions && cutFace.Count * 4 <= maxTriangles; level++)
        {
            Dictionary<long, int> middles = new Dictionary<long, int>();
            List<int> split = new List<int>(triangles.Count * 4);
            List<bool> splitCut = new List<bool>(cutFace.Count * 4);

            for (int t = 0; t < cutFace.Count; t++)
            {
                int a = triangles[t * 3], b = triangles[t * 3 + 1], c = triangles[t * 3 + 2];
                int ab = Middle(a, b, points, middles), bc = Middle(b, c, points, middles), ca = Middle(c, a, points, middles);

                split.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });

                for (int k = 0; k < 4; k++)
                    splitCut.Add(cutFace[t]);
            }

            triangles = split;
            cutFace = splitCut;
        }

        centre = Vector3.zero;

        for (int i = 0; i < points.Count; i++)
            centre += points[i];

        centre /= Mathf.Max(points.Count, 1);

        float startRadius = AverageRadius(points, centre);

        // Melt: each point moves to the average of the points it's joined to
        List<int>[] neighbours = new List<int>[points.Count];

        for (int i = 0; i < points.Count; i++)
            neighbours[i] = new List<int>(6);

        for (int t = 0; t < triangles.Count; t += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int from = triangles[t + k], to = triangles[t + (k + 1) % 3];

                if (!neighbours[from].Contains(to))
                    neighbours[from].Add(to);

                if (!neighbours[to].Contains(from))
                    neighbours[to].Add(from);
            }
        }

        Vector3[] melted = points.ToArray();
        Vector3[] next = new Vector3[melted.Length];

        for (int pass = 0; pass < smoothing; pass++)
        {
            for (int i = 0; i < melted.Length; i++)
            {
                Vector3 sum = Vector3.zero;

                foreach (int other in neighbours[i])
                    sum += melted[other];

                next[i] = neighbours[i].Count > 0 ? sum / neighbours[i].Count : melted[i];
            }

            (melted, next) = (next, melted);
        }

        // Melting shrinks it: back to its size, then toward a ball, all relative to its middle
        float meltedRadius = AverageRadius(melted, centre);
        float grow = meltedRadius > 0.0001f ? startRadius / meltedRadius : 1f;
        Vector3[] final = new Vector3[melted.Length];

        for (int i = 0; i < melted.Length; i++)
        {
            Vector3 offset = (melted[i] - centre) * grow;
            float distance = offset.magnitude;
            final[i] = distance > 0.0001f ? offset / distance * Mathf.Lerp(distance, startRadius, roundness) : offset;
        }

        // Smooth normals per point
        Vector3[] pointNormals = new Vector3[final.Length];

        for (int t = 0; t < triangles.Count; t += 3)
        {
            int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
            Vector3 face = Vector3.Cross(final[b] - final[a], final[c] - final[a]);
            pointNormals[a] += face;
            pointNormals[b] += face;
            pointNormals[c] += face;
        }

        // Output vertices: one per point per side of the colour edge
        Dictionary<long, int> outIndex = new Dictionary<long, int>();
        List<Vector3> vertices = new List<Vector3>(final.Length);
        List<Vector3> normals = new List<Vector3>(final.Length);
        List<Color> colours = new List<Color>(final.Length);
        int[] outTriangles = new int[triangles.Count];

        for (int t = 0; t < triangles.Count; t++)
        {
            int point = triangles[t];
            bool isCut = cutFace[t / 3];
            long key = point * 2L + (isCut ? 1 : 0);

            if (!outIndex.TryGetValue(key, out int index))
            {
                index = vertices.Count;
                vertices.Add(final[point]);
                normals.Add(pointNormals[point].normalized);
                colours.Add(isCut ? Color.red : Color.black);
                outIndex.Add(key, index);
            }

            outTriangles[t] = index;
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetColors(colours);
        mesh.SetTriangles(outTriangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // The point halfway along an edge, made once and shared by both triangles on it
    private static int Middle(int a, int b, List<Vector3> points, Dictionary<long, int> middles)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        if (middles.TryGetValue(key, out int middle))
            return middle;

        middle = points.Count;
        points.Add((points[a] + points[b]) * 0.5f);
        middles.Add(key, middle);
        return middle;
    }

    private static float AverageRadius(IList<Vector3> points, Vector3 centre)
    {
        float sum = 0f;

        for (int i = 0; i < points.Count; i++)
            sum += (points[i] - centre).magnitude;

        return sum / Mathf.Max(points.Count, 1);
    }
#endif
}
