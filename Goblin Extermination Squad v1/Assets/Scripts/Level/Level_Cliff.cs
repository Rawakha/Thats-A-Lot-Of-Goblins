using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public enum Level_CliffShape
{
    Line,       // a wall along the nodes, Width thick: lanes and dividers
    Block       // a solid plateau whose outline is the nodes: rocks and mesas
}

[Serializable]
public struct Level_CliffNode
{
    [Tooltip("Where the node is, in the cliff's space. Height is ignored: cliffs stand on the cliff's own floor.")]
    public Vector3 position;

    [Tooltip("Height of the cliff at this node, metres. Blends to the next node.")]
    [Min(0.5f)] public float height;

    [Tooltip("Line only: how thick the wall is at this node, metres. Blends to the next node.")]
    [Min(0.5f)] public float width;

    public Level_CliffNode(Vector3 position, float height, float width)
    {
        this.position = position;
        this.height = height;
        this.width = width;
    }
}

// A cliff built from a few nodes, like the reference's chunky stone: every wall is broken into columns of square
// blocks that stick out by different amounts, at different heights, some stepped back halfway up and some with a
// chunk on top, each a slightly different shade. One mesh for the whole cliff (never a cube per block), built in the
// editor whenever the cliff changes and saved with the scene, plus a matching collider. Edit it in the Scene view:
// drag the nodes, pull their height (yellow) and width (orange) handles, click the green dots to add nodes, hold Ctrl
// and click a node to delete it.
//
// Both shapes become an outline (a Line is the outline round its wall). The outline gets a plain wall and a flat
// top (triangulated by Level_Triangulator), then the
// block columns are stood along every edge in front of the wall. Tiers stack the outline inset by Tier Inset, so a
// Block can step up like a plateau. Random choices are seeded per edge, so dragging one node only reshuffles the
// blocks next to it.
//
// Two material slots: sides and tops (tops get grass later). Colours come from the vertex colours; use a material on
// Goblins/Level (made by the GameObject > Level menu). Goblins can't walk on top: put cliffs on a layer
// the Flow_System bakes as an obstacle (the menu uses Building) and Bake Obstacles after placing them.
// A level tool: nothing here knows about goblins.
//
// GC: none at runtime (the mesh is built in the editor). No Update and no subscriptions in play.
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Level_Cliff : Level_BuiltTool, Level_IBakeSource
{
    [Header("Shape")]
    [SerializeField] private Level_CliffShape shape = Level_CliffShape.Line;
    [SerializeField] private List<Level_CliffNode> nodes = new List<Level_CliffNode>();

    [Header("Tiers")]
    [Tooltip("Layers stacked on top of each other, each stepped in by Tier Inset, like a plateau with ledges. " +
             "Tiers that would get too thin are left off.")]
    [SerializeField, Range(1, 4)] private int tiers = 1;
    [Tooltip("How far each tier steps in from the one below, metres.")]
    [SerializeField, Min(0.5f)] private float tierInset = 2.5f;

    [Header("Blocks")]
    [Tooltip("Width of a column of blocks along the wall, metres. Bigger is chunkier.")]
    [SerializeField, Min(0.25f)] private float blockSize = 2.4f;
    [Tooltip("How much column widths vary, as a share of Block Size.")]
    [SerializeField, Range(0f, 0.9f)] private float blockVariation = 0.6f;
    [Tooltip("Furthest a column sticks out from the wall, metres.")]
    [SerializeField, Min(0.05f)] private float depth = 1.2f;
    [Tooltip("How many set depths columns pick from (out to Depth). Few steps reads blockier.")]
    [SerializeField, Range(1, 6)] private int depthSteps = 3;
    [Tooltip("Columns' tops are this much above or below the cliff's height, metres.")]
    [SerializeField, Min(0f)] private float heightJitter = 1.4f;
    [Tooltip("Chance a column is a pillar, standing well above the cliff's top.")]
    [SerializeField, Range(0f, 1f)] private float pillarChance = 0.1f;
    [Tooltip("Chance a column is two blocks, the top one stepped in or out from the bottom one.")]
    [SerializeField, Range(0f, 1f)] private float stepChance = 0.35f;
    [Tooltip("Chance of a smaller, shorter slab standing in front of a column, so the wall has layers.")]
    [SerializeField, Range(0f, 1f)] private float frontChance = 0.35f;
    [Tooltip("Chance of a thin flat slab sticking out of the wall like a shelf, high up.")]
    [SerializeField, Range(0f, 1f)] private float ledgeChance = 0.06f;
    [Tooltip("Chance of a small block sitting on top of a column.")]
    [SerializeField, Range(0f, 1f)] private float chunkChance = 0.15f;
    [Tooltip("Columns are turned up to this much off the wall, degrees, so they don't line up like bricks.")]
    [SerializeField, Range(0f, 20f)] private float turnJitter = 5f;
    [Tooltip("Columns lean up to this much in or out, degrees.")]
    [SerializeField, Range(0f, 15f)] private float leanJitter = 3f;
    [Tooltip("A block on every outside corner, so the columns of the two walls don't leave a gap.")]
    [SerializeField] private bool cornerBlocks = true;
    [Tooltip("Change to reshuffle every block.")]
    [SerializeField] private int seed = 1;

    [Header("Top Rocks")]
    [Tooltip("Scatter clusters of stacked blocks over the top tier, like rocks lying on a plateau.")]
    [SerializeField] private bool topRocks;
    [Tooltip("Clusters per 100 square metres of top.")]
    [SerializeField, Range(0f, 5f)] private float rockDensity = 0.6f;
    [Tooltip("Smallest and largest block side in a cluster, metres.")]
    [SerializeField] private Vector2 rockSize = new Vector2(0.8f, 2.4f);
    [Tooltip("Fewest and most blocks in one cluster.")]
    [SerializeField] private Vector2Int rockBlocks = new Vector2Int(2, 6);
    [Tooltip("How tall blocks are, as a share of their side.")]
    [SerializeField] private Vector2 rockHeight = new Vector2(0.5f, 1.3f);
    [Tooltip("Chance a block sits on top of an earlier one in its cluster instead of beside it.")]
    [SerializeField, Range(0f, 1f)] private float rockStackChance = 0.35f;
    [Tooltip("Keep rocks at least this far in from the top's edge, metres.")]
    [SerializeField, Min(0f)] private float rockMargin = 0.75f;

    [Header("Colour")]
    [SerializeField] private Color sideColour = new Color(0.7f, 0.69f, 0.8f);
    [SerializeField] private Color topColour = new Color(0.87f, 0.86f, 0.93f);
    [Tooltip("How much each block's shade varies, lighter or darker.")]
    [SerializeField, Range(0f, 0.3f)] private float shadeVariation = 0.08f;
    [Tooltip("How much darker the cliff gets toward the ground.")]
    [SerializeField, Range(0f, 1f)] private float baseDarkening = 0.3f;
    [Tooltip("Fake ambient occlusion: how much darker blocks set back in the wall are than ones sticking out.")]
    [SerializeField, Range(0f, 1f)] private float recessDarkening = 0.25f;
    [Tooltip("How much darker the sides of a block are than its front, so the gaps between blocks read as crevices.")]
    [SerializeField, Range(0f, 1f)] private float sideDarkening = 0.12f;
    [Tooltip("How much darker the foot of every block is, where it meets what it stands on.")]
    [SerializeField, Range(0f, 1f)] private float contactDarkening = 0.2f;
    [Tooltip("Metres one repeat of a texture covers.")]
    [SerializeField, Min(0.01f)] private float textureSize = 4f;

    [Header("Materials")]
    [Tooltip("Empty keeps the renderer's first material.")]
    [SerializeField] private Material sideMaterial;
    [Tooltip("Flat tops and ledges. Empty uses the side material.")]
    [SerializeField] private Material topMaterial;

    [Header("Physics")]
    [Tooltip("Give the cliff a matching MeshCollider: bullets and bombs hit it, and the flow field bakes it as a wall.")]
    [SerializeField] private bool makeCollider = true;

    [Header("Editing")]
    [Tooltip("Nodes snap to this grid while dragged, metres. 0 is off.")]
    [SerializeField, Min(0f)] private float snap = 0.5f;

    [Header("Stats")]
    [SerializeField, ReadOnly] private int blocks;
    [SerializeField, ReadOnly] private int vertices;
    [SerializeField, ReadOnly] private int triangles;
    [SerializeField, ReadOnly] private float buildMs;

    [SerializeField, HideInInspector] private Mesh mesh;

    // One outline of the cliff in its own space (x, z), anticlockwise, so "outward" (away from the rock) is always the
    // right-hand side of each edge
    private struct Outline
    {
        public List<Vector2> Points;
        public List<float> Heights;
        public bool Detail;
    }

    // Working lists, reused between builds so dragging a node doesn't churn garbage
    private static readonly List<Vector3> positions = new List<Vector3>();
    private static readonly List<Vector3> normals = new List<Vector3>();
    private static readonly List<Color> colours = new List<Color>();
    private static readonly List<Vector2> uvs = new List<Vector2>();
    private static readonly List<int> sideTriangles = new List<int>();
    private static readonly List<int> topTriangles = new List<int>();

    // The last top built, as triangle corners: what top rocks stand on
    private static readonly List<Vector3> surface = new List<Vector3>();
    private static readonly List<Vector4> rockBoxes = new List<Vector4>();

    private float tallest;
    private bool linear;

    // The block being built, for its foot darkening (span 0 is off: walls and tops)
    private float contactLow;
    private float contactSpan;

    public Level_CliffShape Shape => shape;
    public List<Level_CliffNode> Nodes => nodes;
    public float Snap => snap;
    public bool IsClosed => shape == Level_CliffShape.Block;
    public bool UsesWidth => shape != Level_CliffShape.Block;
    public int MinNodes => shape == Level_CliffShape.Line ? 2 : 3;

    // Built without its collider while a handle is dragged (cooking is slow); the editor finishes it on release
    public bool ColliderStale { get; private set; }

    private void Reset()
    {
        seed = UnityEngine.Random.Range(1, 100000);
        Setup(shape);
    }

    private void Awake()
    {
        // Built and saved in the editor; only build here if it never was (or the scene lost it)
        if (Application.isPlaying && mesh == null)
            Build();
    }

    // Starting nodes for a new cliff of this shape, round the cliff's origin
    public void Setup(Level_CliffShape newShape)
    {
        shape = newShape;
        nodes.Clear();

        switch (shape)
        {
            case Level_CliffShape.Line:
                tiers = 1;
                nodes.Add(new Level_CliffNode(new Vector3(-10f, 0f, 0f), 6f, 4f));
                nodes.Add(new Level_CliffNode(new Vector3(0f, 0f, 3f), 6f, 4f));
                nodes.Add(new Level_CliffNode(new Vector3(10f, 0f, 0f), 6f, 4f));
                break;

            case Level_CliffShape.Block:
                tiers = 2;
                nodes.Add(new Level_CliffNode(new Vector3(-8f, 0f, -6f), 7f, 4f));
                nodes.Add(new Level_CliffNode(new Vector3(8f, 0f, -6f), 7f, 4f));
                nodes.Add(new Level_CliffNode(new Vector3(8f, 0f, 6f), 7f, 4f));
                nodes.Add(new Level_CliffNode(new Vector3(-8f, 0f, 6f), 7f, 4f));
                break;
        }
    }

    [InspectorButton("Rebuild")]
    private void Rebuild() => Build();

    [InspectorButton("Reshuffle Blocks")]
    private void Reshuffle()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Reshuffle Cliff");
#endif
        seed = UnityEngine.Random.Range(1, 100000);
        Build();
    }

    public void Build(bool withCollider = true)
    {
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

        if (mesh == null || SharesMesh())
            mesh = new Mesh { name = $"{name} (Level Cliff)" };

        positions.Clear();
        normals.Clear();
        colours.Clear();
        uvs.Clear();
        sideTriangles.Clear();
        topTriangles.Clear();
        blocks = 0;
        linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

        List<Outline> outlines = nodes.Count >= MinNodes ? MakeOutlines() : new List<Outline>();

        if (nodes.Count < MinNodes)
            Debug.LogWarning($"{name}: a {shape} cliff needs at least {MinNodes} nodes.", this);

        tallest = 0.5f;

        for (int i = 0; i < nodes.Count; i++)
            tallest = Mathf.Max(tallest, nodes[i].height);

        if (outlines.Count > 0)
            BuildTiers(outlines);

        mesh.Clear();
        mesh.indexFormat = positions.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetColors(colours);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(sideTriangles, 0, false);
        mesh.SetTriangles(topTriangles, 1, false);
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        ApplyMaterials();

        if (withCollider)
            ApplyCollider();

        ColliderStale = !withCollider && makeCollider;
        vertices = positions.Count;
        triangles = (sideTriangles.Count + topTriangles.Count) / 3;
        buildMs = (float)watch.Elapsed.TotalMilliseconds;

#if UNITY_EDITOR
        RememberBuild();

        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    // A duplicated cliff copies the reference to the same mesh, so editing one would change both: whichever builds
    // first while they share makes its own
    private bool SharesMesh()
    {
#if UNITY_EDITOR
        foreach (Level_Cliff other in FindObjectsByType<Level_Cliff>(FindObjectsInactive.Include))
        {
            if (other != this && other.mesh == mesh)
                return true;
        }
#endif
        return false;
    }

    #region Outlines
    // The shape as outlines: one, the outside (anticlockwise)
    private List<Outline> MakeOutlines()
    {
        List<Outline> outlines = new List<Outline>(2);
        List<Vector2> points = new List<Vector2>(nodes.Count);
        List<float> heights = new List<float>(nodes.Count);
        List<float> halfWidths = new List<float>(nodes.Count);

        for (int i = 0; i < nodes.Count; i++)
        {
            points.Add(new Vector2(nodes[i].position.x, nodes[i].position.z));
            heights.Add(nodes[i].height);
            halfWidths.Add(nodes[i].width * 0.5f);
        }

        switch (shape)
        {
            case Level_CliffShape.Line:
            {
                // Round the wall: down the right side, back up the left
                List<Vector2> right = Offset(points, halfWidths, 1f, false);
                List<Vector2> left = Offset(points, halfWidths, -1f, false);
                List<Vector2> outline = new List<Vector2>(right);
                List<float> outlineHeights = new List<float>(heights);

                for (int i = left.Count - 1; i >= 0; i--)
                {
                    outline.Add(left[i]);
                    outlineHeights.Add(heights[i]);
                }

                outlines.Add(Anticlockwise(outline, outlineHeights, true));
                break;
            }

            case Level_CliffShape.Block:
                outlines.Add(Anticlockwise(points, heights, true));
                break;
        }

        return outlines;
    }

    private static Outline Anticlockwise(List<Vector2> points, List<float> heights, bool detail)
    {
        if (SignedArea(points) < 0f)
        {
            points.Reverse();
            heights.Reverse();
        }

        return new Outline { Points = points, Heights = heights, Detail = detail };
    }

    // Each point moved along its corner's mitred right-hand normal by amount x sign (negative goes left). Open
    // polylines use the end edges' normals at the ends.
    private static List<Vector2> Offset(List<Vector2> points, List<float> amounts, float sign, bool closed)
    {
        int count = points.Count;
        List<Vector2> result = new List<Vector2>(count);

        for (int i = 0; i < count; i++)
        {
            bool hasPrevious = closed || i > 0;
            bool hasNext = closed || i < count - 1;
            Vector2 before = hasPrevious ? Outward(points[(i - 1 + count) % count], points[i]) : Vector2.zero;
            Vector2 after = hasNext ? Outward(points[i], points[(i + 1) % count]) : Vector2.zero;

            if (!hasPrevious)
                before = after;

            if (!hasNext)
                after = before;

            Vector2 mitre = before + after;
            mitre = mitre.sqrMagnitude > 1e-6f ? mitre.normalized : before;

            // A sharp corner's mitre is longer, capped so spikes don't shoot off
            float scale = 1f / Mathf.Max(Vector2.Dot(mitre, before), 0.35f);
            result.Add(points[i] + mitre * (amounts[i] * scale * sign));
        }

        return result;
    }

    // Right-hand normal of an edge: outward for anticlockwise outlines, into the hole for clockwise ones
    private static Vector2 Outward(Vector2 from, Vector2 to)
    {
        Vector2 edge = to - from;
        return new Vector2(edge.y, -edge.x).normalized;
    }

    private static float SignedArea(List<Vector2> points)
    {
        float area = 0f;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];
            area += a.x * b.y - b.x * a.y;
        }

        return area * 0.5f;
    }
    #endregion

    #region Tiers
    private void BuildTiers(List<Outline> outlines)
    {
        // Each tier is every outline stepped in again; stop at the first one that would turn inside out or vanish
        List<List<Outline>> stack = new List<List<Outline>>(tiers) { outlines };

        for (int k = 1; k < tiers; k++)
        {
            List<Outline> tier = new List<Outline>(outlines.Count);
            bool fits = true;

            for (int o = 0; o < outlines.Count && fits; o++)
            {
                Outline source = outlines[o];
                List<float> inset = new List<float>(source.Points.Count);

                for (int i = 0; i < source.Points.Count; i++)
                    inset.Add(tierInset * k);

                List<Vector2> points = Offset(source.Points, inset, -1f, true);
                float before = SignedArea(source.Points);
                float after = SignedArea(points);
                fits = Mathf.Sign(before) == Mathf.Sign(after) && (o > 0 || after > before * 0.05f);

                tier.Add(new Outline { Points = points, Heights = source.Heights, Detail = source.Detail });
            }

            if (!fits)
                break;

            stack.Add(tier);
        }

        int count = stack.Count;

        for (int k = 0; k < count; k++)
        {
            float bottom = (float)k / count;
            float top = (k + 1f) / count;
            List<Outline> tier = stack[k];

            BuildTop(tier, top);

            for (int o = 0; o < tier.Count; o++)
            {
                BuildWall(tier[o], bottom, top);

                if (tier[o].Detail)
                    BuildBlocks(tier[o], o, k, bottom, top);
            }
        }

        if (topRocks)
            BuildRocks(stack[count - 1]);
    }

    // The flat top of one tier: the outside with any hole cut out, each corner at its node's height
    private void BuildTop(List<Outline> tier, float share)
    {
        List<List<Vector2>> holes = new List<List<Vector2>>();
        List<float> heights = new List<float>(tier[0].Heights);

        for (int o = 1; o < tier.Count; o++)
        {
            holes.Add(tier[o].Points);
            heights.AddRange(tier[o].Heights);
        }

        List<Vector2> points = new List<Vector2>();
        List<int> flat;

        try
        {
            flat = Level_Triangulator.Triangulate(tier[0].Points, holes, points);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{name}: couldn't fill the cliff's top ({exception.Message}). Untangle the nodes.", this);
            return;
        }

        int first = positions.Count;
        surface.Clear();

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 position = new Vector3(points[i].x, heights[i] * share, points[i].y);
            positions.Add(position);
            normals.Add(Vector3.up);
            colours.Add(Shaded(topColour, 1f, position.y));
            uvs.Add(points[i] / textureSize);
        }

        for (int t = 0; t + 2 < flat.Count; t += 3)
        {
            AddTriangle(topTriangles, first + flat[t], first + flat[t + 1], first + flat[t + 2], Vector3.up);
            surface.Add(positions[first + flat[t]]);
            surface.Add(positions[first + flat[t + 1]]);
            surface.Add(positions[first + flat[t + 2]]);
        }
    }

    // Clusters of blocks scattered over the top tier: each a few boxes turned the same way, side by side or
    // stacked, sitting on the top wherever they land
    private void BuildRocks(List<Outline> tier)
    {
        float area = Mathf.Abs(SignedArea(tier[0].Points));

        for (int o = 1; o < tier.Count; o++)
            area -= Mathf.Abs(SignedArea(tier[o].Points));

        Unity.Mathematics.Random random = EdgeRandom(-1, -1, -1);
        float wanted = area / 100f * rockDensity;
        int clusters = Mathf.FloorToInt(wanted) + (random.NextFloat() < wanted % 1f ? 1 : 0);
        Bounds2(tier[0].Points, out Vector2 min, out Vector2 max);
        float smallest = Mathf.Max(0.1f, Mathf.Min(rockSize.x, rockSize.y));
        float largest = Mathf.Max(rockSize.x, rockSize.y);

        for (int c = 0; c < clusters; c++)
        {
            // A spot on the top (a few tries: tops can be thin)
            Vector2 spot = Vector2.zero;
            bool found = false;

            for (int attempt = 0; attempt < 20 && !found; attempt++)
            {
                spot = new Vector2(random.NextFloat(min.x, max.x), random.NextFloat(min.y, max.y));
                found = OnTop(tier, spot, largest * 0.5f + rockMargin);
            }

            if (!found)
                continue;

            float angle = random.NextFloat(0f, Mathf.PI * 2f);
            Vector2 u = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 v = new Vector2(-u.y, u.x);
            int count = random.NextInt(Mathf.Max(1, rockBlocks.x), Mathf.Max(rockBlocks.x, rockBlocks.y) + 1);
            float shade = 1f + random.NextFloat(-1f, 1f) * shadeVariation;
            rockBoxes.Clear();     // x, y: centre, z: half side, w: top

            for (int b = 0; b < count; b++)
            {
                float half = random.NextFloat(smallest, largest) * 0.5f;
                float height = half * 2f * random.NextFloat(rockHeight.x, Mathf.Max(rockHeight.x, rockHeight.y));
                Vector2 centre;
                float low;

                if (rockBoxes.Count > 0 && random.NextFloat() < rockStackChance)
                {
                    // On top of an earlier block, smaller than it
                    Vector4 under = rockBoxes[random.NextInt(0, rockBoxes.Count)];
                    half = Mathf.Min(half, under.z * 0.8f);
                    float slack = under.z - half;
                    centre = new Vector2(under.x, under.y) + u * random.NextFloat(-slack, slack) + v * random.NextFloat(-slack, slack);
                    low = under.w;
                }
                else
                {
                    // Beside the cluster's middle, touching or overlapping its neighbours
                    float reach = b == 0 ? 0f : largest * 0.6f;
                    centre = spot + u * random.NextFloat(-reach, reach) + v * random.NextFloat(-reach, reach);

                    if (!OnTop(tier, centre, half * 1.42f + rockMargin))
                        continue;

                    low = SurfaceHeight(centre) - 0.1f;
                }

                float high = low + height;
                rockBoxes.Add(new Vector4(centre.x, centre.y, half, high));
                Box(centre, u, v, half, half, low, high, shade * (1f + random.NextFloat(-1f, 1f) * shadeVariation), low);
            }
        }
    }

    // Whether a point is on the top at least margin in from every edge
    private static bool OnTop(List<Outline> tier, Vector2 point, float margin)
    {
        if (!Inside(tier[0].Points, point))
            return false;

        for (int o = 1; o < tier.Count; o++)
        {
            if (Inside(tier[o].Points, point))
                return false;
        }

        for (int o = 0; o < tier.Count; o++)
        {
            List<Vector2> points = tier[o].Points;

            for (int i = 0; i < points.Count; i++)
            {
                if (DistanceToSegment(point, points[i], points[(i + 1) % points.Count]) < margin)
                    return false;
            }
        }

        return true;
    }

    // Height of the last top built under a point (its highest corner if the point misses every triangle)
    private static float SurfaceHeight(Vector2 point)
    {
        float fallback = 0f;

        for (int t = 0; t + 2 < surface.Count; t += 3)
        {
            Vector3 a = surface[t];
            Vector3 b = surface[t + 1];
            Vector3 c = surface[t + 2];
            fallback = Mathf.Max(fallback, Mathf.Max(a.y, Mathf.Max(b.y, c.y)));

            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);

            if (Mathf.Abs(d) < 1e-8f)
                continue;

            float wa = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.y - c.z)) / d;
            float wb = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.y - c.z)) / d;
            float wc = 1f - wa - wb;

            if (wa >= -1e-4f && wb >= -1e-4f && wc >= -1e-4f)
                return wa * a.y + wb * b.y + wc * c.y;
        }

        return fallback;
    }

    private static bool Inside(List<Vector2> polygon, Vector2 point)
    {
        bool inside = false;

        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];

            if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }

        return inside;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(point, a + ab * t);
    }

    private static void Bounds2(List<Vector2> points, out Vector2 min, out Vector2 max)
    {
        min = max = points[0];

        for (int i = 1; i < points.Count; i++)
        {
            min = Vector2.Min(min, points[i]);
            max = Vector2.Max(max, points[i]);
        }
    }

    // The plain wall along an outline, between this tier's bottom and top, behind the blocks
    private void BuildWall(Outline outline, float bottom, float top)
    {
        List<Vector2> points = outline.Points;

        for (int i = 0; i < points.Count; i++)
        {
            int j = (i + 1) % points.Count;
            Vector2 a = points[i];
            Vector2 b = points[j];
            Vector2 outward = Outward(a, b);

            AddQuad(sideTriangles,
                new Vector3(a.x, outline.Heights[i] * bottom, a.y),
                new Vector3(b.x, outline.Heights[j] * bottom, b.y),
                new Vector3(b.x, outline.Heights[j] * top, b.y),
                new Vector3(a.x, outline.Heights[i] * top, a.y),
                new Vector3(outward.x, 0f, outward.y), sideColour, 1f, false);
        }
    }

    // Columns of blocks along every edge of an outline, standing out from its wall
    private void BuildBlocks(Outline outline, int outlineIndex, int tier, float bottom, float top)
    {
        List<Vector2> points = outline.Points;
        int count = points.Count;

        // On an upper tier the blocks stay on the ledge rather than hanging off the tier below
        float reach = tier > 0 ? Mathf.Min(depth, tierInset * 0.8f) : depth;

        for (int i = 0; i < count; i++)
        {
            int j = (i + 1) % count;
            Vector2 a = points[i];
            Vector2 b = points[j];
            float length = Vector2.Distance(a, b);

            if (length < 0.01f)
                continue;

            Vector2 along = (b - a) / length;
            Vector2 outward = new Vector2(along.y, -along.x);
            Unity.Mathematics.Random random = EdgeRandom(outlineIndex, tier, i);
            float s = 0f;

            while (s < length - 0.01f)
            {
                float width = blockSize * random.NextFloat(1f - blockVariation, 1f + blockVariation);

                // The last column takes what's left rather than leaving a sliver
                if (length - (s + width) < blockSize * 0.4f)
                    width = length - s;

                float end = Mathf.Min(s + width, length);
                float middle = (s + end) * 0.5f / length;
                float height = Mathf.Lerp(outline.Heights[i], outline.Heights[j], middle);
                float low = height * bottom;
                float high = Mathf.Max(height * top + random.NextFloat(-1f, 1f) * heightJitter, low + 0.3f);
                float out1 = Depth(ref random, reach);
                float wallTop = height * top;
                float overlap = blockSize * 0.06f;      // neighbours overlap a little, so turned ones leave no gaps

                // Set-back columns are darker, like light not reaching into the wall
                float shade = (1f + random.NextFloat(-1f, 1f) * shadeVariation) * Mathf.Lerp(1f - recessDarkening, 1f, out1 / reach);

                if (random.NextFloat() < pillarChance)
                    high += blockSize * random.NextFloat(0.6f, 1.8f);

                if (random.NextFloat() < stepChance && high - low > blockSize)
                {
                    float split = Mathf.Lerp(low, high, random.NextFloat(0.3f, 0.7f));
                    float out2 = Depth(ref random, reach);
                    float upper = (1f + random.NextFloat(-1f, 1f) * shadeVariation) * Mathf.Lerp(1f - recessDarkening, 1f, out2 / reach);
                    Column(a, along, outward, s - overlap, end + overlap, out1, low, split, wallTop, shade, false, ref random);
                    Column(a, along, outward, s - overlap, end + overlap, out2, split, high, wallTop, upper, false, ref random);
                    out1 = out2;
                }
                else
                {
                    Column(a, along, outward, s - overlap, end + overlap, out1, low, high, wallTop, shade, false, ref random);
                }

                // A smaller, shorter slab in front, so the wall builds up in layers
                if (random.NextFloat() < frontChance && end - s > 0.5f)
                {
                    float frontWidth = (end - s) * random.NextFloat(0.35f, 0.75f);
                    float frontStart = Mathf.Lerp(s, end - frontWidth, random.NextFloat());
                    float frontOut = out1 + reach * random.NextFloat(0.25f, 0.7f);
                    float frontHigh = low + (high - low) * random.NextFloat(0.2f, 0.65f);
                    Column(a, along, outward, frontStart, frontStart + frontWidth, frontOut, low, frontHigh, float.MaxValue,
                        shade * 1.05f, false, ref random);
                }

                // A thin shelf sticking out high up
                if (random.NextFloat() < ledgeChance && tier == 0)
                {
                    float ledgeWidth = blockSize * random.NextFloat(1.5f, 3f);
                    float ledgeStart = Mathf.Clamp(s - ledgeWidth * 0.25f, 0f, Mathf.Max(0f, length - ledgeWidth));
                    float ledgeLow = Mathf.Lerp(low, wallTop, random.NextFloat(0.55f, 0.92f));
                    float thickness = blockSize * random.NextFloat(0.15f, 0.3f);
                    Column(a, along, outward, ledgeStart, Mathf.Min(ledgeStart + ledgeWidth, length), reach * random.NextFloat(1.3f, 2f),
                        ledgeLow, ledgeLow + thickness, float.MaxValue, shade * 1.06f, true, ref random);
                }

                // A loose chunk sitting on the column
                if (random.NextFloat() < chunkChance)
                {
                    float size = Mathf.Min(end - s, blockSize) * random.NextFloat(0.35f, 0.6f);
                    float place = Mathf.Lerp(s + size * 0.5f, end - size * 0.5f, random.NextFloat());
                    Vector2 centre = a + along * place + outward * (out1 - size * 0.5f);
                    Box(centre, along, outward, size * 0.5f, size * 0.5f, high, high + size * random.NextFloat(0.5f, 0.9f),
                        shade * 1.04f, high);
                }

                s = end;
            }

            // A corner block where this edge turns away from the next, covering the gap between their columns
            if (cornerBlocks)
            {
                Vector2 next = points[(j + 1) % count] - b;

                if (next.sqrMagnitude > 1e-6f && Vector2.Dot(next.normalized, outward) < -0.05f)
                {
                    Vector2 mitre = (outward + Outward(b, points[(j + 1) % count])).normalized;
                    Vector2 side = new Vector2(-mitre.y, mitre.x);
                    float half = Mathf.Max(reach, 0.3f);
                    float height = outline.Heights[j];
                    float low = height * bottom;
                    float high = Mathf.Max(height * top + random.NextFloat(-0.5f, 1f) * heightJitter, low + 0.3f);
                    Box(b, side, mitre, half, half, low, high, 1f + random.NextFloat(-1f, 1f) * shadeVariation, low);
                }
            }
        }
    }

    // A box in the cliff's space, turned by rotation about origin, from low to high on its own up axis: four sides and
    // a top, the back (-z) and bottom only when asked. Its sides are a little darker than its front (crevices), and its
    // foot darker than its top (contact shadow).
    private void Box3D(Vector3 origin, Quaternion rotation, float halfX, float halfZ, float low, float high, float shade,
        bool back = true, bool bottom = false)
    {
        Vector3 Corner(float x, float y, float z) => origin + rotation * new Vector3(x, y, z);

        Vector3 b00 = Corner(-halfX, low, -halfZ);
        Vector3 b10 = Corner(halfX, low, -halfZ);
        Vector3 b11 = Corner(halfX, low, halfZ);
        Vector3 b01 = Corner(-halfX, low, halfZ);
        Vector3 t00 = Corner(-halfX, high, -halfZ);
        Vector3 t10 = Corner(halfX, high, -halfZ);
        Vector3 t11 = Corner(halfX, high, halfZ);
        Vector3 t01 = Corner(-halfX, high, halfZ);
        Vector3 right = rotation * Vector3.right;
        Vector3 forward = rotation * Vector3.forward;
        Vector3 up = rotation * Vector3.up;
        float sides = shade * (1f - sideDarkening);

        contactLow = Mathf.Min(Mathf.Min(b00.y, b10.y), Mathf.Min(b11.y, b01.y));
        contactSpan = Mathf.Min(high - low, 1.5f);

        AddQuad(sideTriangles, b01, b11, t11, t01, forward, sideColour, shade, false);
        AddQuad(sideTriangles, b11, b10, t10, t11, right, sideColour, sides, false);
        AddQuad(sideTriangles, b00, b01, t01, t00, -right, sideColour, sides, false);

        if (back)
            AddQuad(sideTriangles, b10, b00, t00, t10, -forward, sideColour, sides, false);

        if (bottom)
            AddQuad(sideTriangles, b00, b10, b11, b01, -up, sideColour, sides * 0.85f, false);

        contactSpan = 0f;
        AddQuad(topTriangles, t00, t10, t11, t01, up, topColour, shade, true);
        blocks++;
    }

    // One column of an edge from s0 to s1 along it (from a), sticking out by 'out', from low to high, turned and
    // leaning a little at random. Its back runs a way into the wall so turning never opens a gap, and is only built
    // when the column pokes up above wallTop (where its back can be seen).
    private void Column(Vector2 a, Vector2 along, Vector2 outward, float s0, float s1, float out1, float low, float high,
        float wallTop, float shade, bool bottom, ref Unity.Mathematics.Random random)
    {
        const float Back = 0.3f;
        float halfAlong = (s1 - s0) * 0.5f;
        float halfOut = (out1 + Back) * 0.5f;
        Vector2 centre = a + along * ((s0 + s1) * 0.5f) + outward * ((out1 - Back) * 0.5f);
        float turn = random.NextFloat(-1f, 1f) * turnJitter;
        float lean = random.NextFloat(-1f, 1f) * leanJitter;
        float sink = Mathf.Tan(Mathf.Abs(lean) * Mathf.Deg2Rad) * halfOut + 0.05f;
        Quaternion rotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y)) *
                              Quaternion.Euler(0f, turn, 0f) * Quaternion.Euler(lean, 0f, 0f);

        Box3D(new Vector3(centre.x, low, centre.y), rotation, halfAlong, halfOut, bottom ? 0f : -sink, high - low, shade,
            high > wallTop + 0.01f, bottom);
    }

    // A box standing on 'low', its sides along u and v: four sides and a top (bottoms are never seen). Its back (-v)
    // side only runs from backFrom up, so a column against a wall builds just the part above the wall.
    private void Box(Vector2 centre, Vector2 u, Vector2 v, float halfU, float halfV, float low, float high, float shade,
        float backFrom)
    {
        Vector2 du = u * halfU;
        Vector2 dv = v * halfV;
        Vector2 c00 = centre - du - dv;
        Vector2 c10 = centre + du - dv;
        Vector2 c11 = centre + du + dv;
        Vector2 c01 = centre - du + dv;
        float sides = shade * (1f - sideDarkening);
        contactLow = low;
        contactSpan = Mathf.Min(high - low, 1.5f);

        Side(c01, c11, v, low, high, shade);        // front
        Side(c10, c11, u, low, high, sides);
        Side(c00, c01, -u, low, high, sides);

        if (backFrom < high - 0.001f)
            Side(c00, c10, -v, backFrom, high, sides);

        contactSpan = 0f;

        AddQuad(topTriangles,
            new Vector3(c00.x, high, c00.y),
            new Vector3(c10.x, high, c10.y),
            new Vector3(c11.x, high, c11.y),
            new Vector3(c01.x, high, c01.y),
            Vector3.up, topColour, shade, true);

        blocks++;
    }

    private void Side(Vector2 a, Vector2 b, Vector2 facing, float low, float high, float shade)
    {
        AddQuad(sideTriangles,
            new Vector3(a.x, low, a.y),
            new Vector3(b.x, low, b.y),
            new Vector3(b.x, high, b.y),
            new Vector3(a.x, high, a.y),
            new Vector3(facing.x, 0f, facing.y), sideColour, shade, false);
    }

    // One of Depth Steps set depths, so neighbouring columns line up in a few planes and read as stacked blocks
    private float Depth(ref Unity.Mathematics.Random random, float reach)
    {
        return reach * (random.NextInt(0, depthSteps) + 1f) / depthSteps;
    }

    // Seeded per edge, so changing one edge leaves the rest of the cliff's blocks where they were
    private Unity.Mathematics.Random EdgeRandom(int outline, int tier, int edge)
    {
        uint hash = Unity.Mathematics.math.hash(new Unity.Mathematics.int4(seed, outline, tier, edge));
        return Unity.Mathematics.Random.CreateFromIndex(hash);
    }
    #endregion

    #region Mesh
    // A flat-shaded quad (its own four corners, so edges stay crisp), wound to face the given way
    private void AddQuad(List<int> output, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, Color colour,
        float shade, bool isTop)
    {
        int first = positions.Count;
        Vector3 tangent = (b - a).normalized;

        AddCorner(a, facing, colour, shade, isTop, tangent);
        AddCorner(b, facing, colour, shade, isTop, tangent);
        AddCorner(c, facing, colour, shade, isTop, tangent);
        AddCorner(d, facing, colour, shade, isTop, tangent);

        AddTriangle(output, first, first + 1, first + 2, facing);
        AddTriangle(output, first, first + 2, first + 3, facing);
    }

    private void AddCorner(Vector3 position, Vector3 normal, Color colour, float shade, bool isTop, Vector3 tangent)
    {
        positions.Add(position);
        normals.Add(normal);
        colours.Add(Shaded(colour, shade, position.y));

        // Tops are mapped from above, sides along the wall and up
        uvs.Add(isTop
            ? new Vector2(position.x, position.z) / textureSize
            : new Vector2(position.x * tangent.x + position.z * tangent.z, position.y) / textureSize);
    }

    // The colour at a height: this block's shade, darker toward the ground
    private Color Shaded(Color colour, float shade, float height)
    {
        float darken = Mathf.Lerp(1f - baseDarkening, 1f, Mathf.Clamp01(height / tallest));

        if (contactSpan > 0f)
            darken *= Mathf.Lerp(1f - contactDarkening, 1f, Mathf.Clamp01((height - contactLow) / contactSpan));
        Color result = colour * (shade * darken);
        result.a = 1f;
        return linear ? result.linear : result;
    }

    // Adds a triangle wound so it faces the given way (Unity's front face), whichever order the corners came in
    private static void AddTriangle(List<int> output, int a, int b, int c, Vector3 facing)
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
        Material side = sideMaterial != null ? sideMaterial : meshRenderer.sharedMaterial;
        Material top = topMaterial != null ? topMaterial : side;
        meshRenderer.sharedMaterials = new[] { side, top };
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
    #endregion

#if UNITY_EDITOR
    #region Editor rebuilds
    private static readonly string[] BuildOutputs = { "blocks", "vertices", "triangles", "buildMs", "mesh" };

    protected override string InputKey() => Level_BuildKey.Of(this, null, BuildOutputs);
    protected override bool HasSavedOutput => mesh != null;
    protected override void RebuildNow() => Build();

    // So a cliff can go inside a bake group's sources
    public void PrepareForBake()
    {
        if (mesh == null)
            Build();
    }

    public string BakeKey => Level_BuildKey.Stable(this, null, BuildOutputs);
    #endregion
#endif
}
