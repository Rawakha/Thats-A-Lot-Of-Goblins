using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[Serializable]
public class Level_RiverNode
{
    [Tooltip("Position in the river's space. Y is ignored.")]
    public Vector3 position;
    [Tooltip("Multiplier of the river's width at this node.")]
    [Range(0.3f, 3f)] public float width = 1f;
}

// A river carved through the ground. You draw its path with a few nodes (white squares move, green dots add, Ctrl/Cmd-click
// deletes, orange cubes set the width at a node); the path is smoothed into a curve, ends in rounded caps and has a ragged
// edge. Like a Level_Hole, it is a cut in the ground: Level_Ground cuts the river's outline out, and the river builds
// what's inside it, all as children:
//   Banks    the bank and bed, a few chunky steps down from the rim, with vertex colours (sand, wet sand, dark bed).
//   Water    a flat plane at the water level on a plain material for you to replace with a water shader. UV0 runs along
//            the river (x) and across it (y, 0 to 1). Vertex colour R is 0 at the shore and 1 mid-river, for foam or tint.
//   Blocker  an invisible wall along each bank on the Building layer, so the flow field bake treats the river as an obstacle
//            and goblins path round it (bridges later). Rebake obstacles after changing a river.
//
// The river's object must sit at the ground's height (only this object's x and z matter to the ground, like holes). Keep it
// well inside the ground: a river that touches the ground's edge, a hole or another river is left out with a warning.
// Tight bends narrow the river instead of folding it. A level tool: nothing here knows about goblins.
//
// Setup: GameObject > Level > Terrain > River. Raise Edge Noise for a more ragged bank, change Seed to reshuffle.
// GC: nothing at runtime (meshes are built in the editor and saved with the scene). No Update and no subscriptions.
[DisallowMultipleComponent]
public class Level_River : Level_BuiltTool
{
    private const int Cap = 8;                 // samples round each rounded end
    private const float CapAngle = 85f * Mathf.Deg2Rad;
    private const int MaxSamples = 1500;

    [Header("Shape")]
    [Tooltip("Width of the river at a node with Width 1, metres, rim to rim.")]
    [SerializeField, Min(1f)] private float width = 8f;
    [Tooltip("How deep the river bed is below the ground, metres.")]
    [SerializeField, Min(0.3f)] private float depth = 1.6f;
    [Tooltip("Width of the bank from the rim to where the bed starts, metres. Narrow stretches get a narrower bank.")]
    [SerializeField, Min(0.3f)] private float bankWidth = 2.5f;
    [Tooltip("How many chunky steps the bank has.")]
    [SerializeField, Range(1, 4)] private int steps = 2;
    [Tooltip("How much of each step is a slope rather than flat. 1 is a smooth slope.")]
    [SerializeField, Range(0.1f, 1f)] private float stepSlope = 0.35f;

    [Header("Ragged Edge")]
    [Tooltip("How far the rim wobbles in and out, metres.")]
    [SerializeField, Min(0f)] private float edgeNoise = 0.35f;
    [Tooltip("How far the bank's width varies, as a fraction of Bank Width.")]
    [SerializeField, Range(0f, 1f)] private float bankJitter = 0.3f;
    [Tooltip("Distance along the river over which the wobble changes, metres.")]
    [SerializeField, Min(1f)] private float noiseScale = 6f;
    [Tooltip("Change to reshuffle the wobble.")]
    [SerializeField] private int seed = 1;

    [Header("Water")]
    [Tooltip("How far the water surface sits below the ground, metres.")]
    [SerializeField, Min(0.05f)] private float waterLevel = 0.6f;
    [Tooltip("The water plane's material. Replace it with your water shader.")]
    [SerializeField] private Material waterMaterial;
    [Tooltip("Metres along the river for one repeat of the water's U coordinate.")]
    [SerializeField, Min(0.1f)] private float waterUvLength = 8f;

    [Header("Banks")]
    [Tooltip("Material for the bank and bed. Use one on Goblins/Level, which shows vertex colours.")]
    [SerializeField] private Material bankMaterial;
    [Tooltip("Bank colour at the rim.")]
    [SerializeField] private Color rimColour = new Color(0.86f, 0.74f, 0.52f);
    [Tooltip("Bank colour at the waterline.")]
    [SerializeField] private Color waterlineColour = new Color(0.55f, 0.42f, 0.3f);
    [Tooltip("Colour of the bed.")]
    [SerializeField] private Color bedColour = new Color(0.32f, 0.27f, 0.3f);
    [Tooltip("Give the banks a MeshCollider so shots and bodies hit the bed.")]
    [SerializeField] private bool bankCollider = true;

    [Header("Blocker")]
    [Tooltip("An invisible wall along each bank on the Building layer, so the flow field treats the river as an obstacle.")]
    [SerializeField] private bool blockGoblins = true;
    [Tooltip("How far the wall reaches in from the rim, metres. Keep it at least as wide as a flow cell.")]
    [SerializeField, Min(0.5f)] private float blockerWidth = 1.5f;
    [Tooltip("Height of the wall above the ground, metres. Keep it above the flow bake's ground clearance.")]
    [SerializeField, Min(0.5f)] private float blockerHeight = 2.5f;

    [Header("Editing")]
    [Tooltip("Handles snap to this grid, metres. 0 is off.")]
    [SerializeField, Min(0f)] private float snap = 0.5f;
    [Tooltip("Distance between samples along the curve, metres. Smaller is smoother but heavier.")]
    [SerializeField, Min(0.25f)] private float curveSpacing = 1f;

    [SerializeField] private List<Level_RiverNode> nodes = new List<Level_RiverNode>
    {
        new Level_RiverNode { position = new Vector3(-24f, 0f, -4f) },
        new Level_RiverNode { position = new Vector3(-8f, 0f, 6f) },
        new Level_RiverNode { position = new Vector3(8f, 0f, -6f) },
        new Level_RiverNode { position = new Vector3(24f, 0f, 4f) }
    };

    [Header("Stats")]
    [SerializeField, ReadOnly] private int vertices;
    [SerializeField, ReadOnly] private float buildMs;

    [SerializeField, HideInInspector] private GameObject banks, water, blocker;
    [SerializeField, HideInInspector] private Mesh bankMesh, waterMesh, blockerMesh;

    public List<Level_RiverNode> Nodes => nodes;
    public float Width => width;
    public float Snap => snap;

    // One row of the bank's cross-section: how far in from the rim (0 to 1 of the bank's width) and how far down
    private struct Row
    {
        public float inset, y;
    }

    // The river sampled along its curve: centre, left normal, and for each side how far the rim and the bank reach from it
    private sealed class Frame
    {
        public Vector2[] centre, normal;
        public float[] arc, left, right, bankLeft, bankRight;
        public int Count => centre.Length;
    }

    private static Vector2 Flat(Level_RiverNode node) => new Vector2(node.position.x, node.position.z);

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private float Noise(float distance, float channel)
    {
        float x = distance / Mathf.Max(1f, noiseScale) + (seed * 0.7131f) % 97f;
        float y = channel * 7.31f + (seed * 0.3917f) % 89f;
        return (Mathf.PerlinNoise(x, y) * 0.7f + Mathf.PerlinNoise(x * 2.3f + 11f, y + 3f) * 0.3f) * 2f - 1f;
    }

    // The curve through the nodes, rounded ends, and each sample's rim and bank reach. False (with why) if it can't be made.
    private bool Compute(out Frame frame, out string problem)
    {
        frame = null;
        problem = null;

        if (nodes == null || nodes.Count < 2 || nodes.Contains(null))
        {
            problem = "A river needs at least two nodes.";
            return false;
        }

        List<Vector2> points = new List<Vector2>();
        List<float> halves = new List<float>();
        int count = nodes.Count;

        for (int segment = 0; segment + 1 < count; segment++)
        {
            Vector2 p0 = Flat(nodes[Mathf.Max(0, segment - 1)]);
            Vector2 p1 = Flat(nodes[segment]);
            Vector2 p2 = Flat(nodes[segment + 1]);
            Vector2 p3 = Flat(nodes[Mathf.Min(count - 1, segment + 2)]);
            float length = Vector2.Distance(p1, p2);

            if (length < 0.5f)
            {
                problem = $"Nodes {segment} and {segment + 1} are too close together.";
                return false;
            }

            int pieces = Mathf.Max(2, Mathf.CeilToInt(length / curveSpacing));

            for (int s = 0; s < pieces; s++)
            {
                float t = (float)s / pieces;
                points.Add(CatmullRom(p0, p1, p2, p3, t));
                halves.Add(Mathf.Lerp(nodes[segment].width, nodes[segment + 1].width, t * t * (3f - 2f * t)) * width * 0.5f);
            }

            if (points.Count > MaxSamples)
            {
                problem = "The river is too long for its Curve Spacing. Raise the spacing.";
                return false;
            }
        }

        points.Add(Flat(nodes[count - 1]));
        halves.Add(nodes[count - 1].width * width * 0.5f);

        // Rounded ends: samples along a quarter circle past each end, so the outline closes in a semicircle
        Vector2 startOut = (points[0] - points[1]).normalized;
        Vector2 endOut = (points[points.Count - 1] - points[points.Count - 2]).normalized;
        float startHalf = halves[0];
        float endHalf = halves[halves.Count - 1];
        List<Vector2> all = new List<Vector2>();
        List<float> allHalf = new List<float>();

        for (int k = Cap; k >= 1; k--)
        {
            float angle = k * CapAngle / Cap;
            all.Add(points[0] + startOut * (startHalf * Mathf.Sin(angle)));
            allHalf.Add(startHalf * Mathf.Cos(angle));
        }

        all.AddRange(points);
        allHalf.AddRange(halves);

        for (int k = 1; k <= Cap; k++)
        {
            float angle = k * CapAngle / Cap;
            all.Add(points[points.Count - 1] + endOut * (endHalf * Mathf.Sin(angle)));
            allHalf.Add(endHalf * Mathf.Cos(angle));
        }

        int n = all.Count;
        frame = new Frame
        {
            centre = all.ToArray(), normal = new Vector2[n], arc = new float[n], left = new float[n], right = new float[n],
            bankLeft = new float[n], bankRight = new float[n]
        };

        // Tangents, normals and distance along
        for (int i = 0; i < n; i++)
        {
            Vector2 tangent = (all[Mathf.Min(n - 1, i + 1)] - all[Mathf.Max(0, i - 1)]).normalized;
            frame.normal[i] = new Vector2(-tangent.y, tangent.x);

            if (i > 0)
                frame.arc[i] = frame.arc[i - 1] + Vector2.Distance(all[i], all[i - 1]);
        }

        // A bend tighter than the river is wide would fold its inside edge over itself, so the river narrows there
        float[] raw = new float[n];
        float[] factor = new float[n];

        for (int i = 0; i < n; i++)
        {
            raw[i] = 1f;

            if (i == 0 || i == n - 1)
                continue;

            Vector2 before = all[i] - all[i - 1];
            Vector2 after = all[i + 1] - all[i];
            float run = (before.magnitude + after.magnitude) * 0.5f;

            if (run < 1e-4f || allHalf[i] < 1e-3f)
                continue;

            float curvature = Vector2.Angle(before, after) * Mathf.Deg2Rad / run;

            if (curvature > 1e-3f)
                raw[i] = Mathf.Min(1f, 0.9f / (curvature * allHalf[i]));
        }

        for (int i = 0; i < n; i++)
        {
            float lowest = 1f;

            for (int j = Mathf.Max(0, i - 2); j <= Mathf.Min(n - 1, i + 2); j++)
                lowest = Mathf.Min(lowest, raw[j]);

            factor[i] = lowest;
        }

        for (int i = 0; i < n; i++)
        {
            float blur = 0.25f * factor[Mathf.Max(0, i - 1)] + 0.5f * factor[i] + 0.25f * factor[Mathf.Min(n - 1, i + 1)];
            float half = allHalf[i] * Mathf.Min(raw[i], blur, factor[i]);

            // The wobble fades out towards the rounded tips
            float fade = Mathf.Clamp01(half / 1.5f);
            float arc = frame.arc[i];
            frame.left[i] = Mathf.Max(0.1f, half + edgeNoise * fade * Noise(arc, 0f));
            frame.right[i] = Mathf.Max(0.1f, half + edgeNoise * fade * Noise(arc, 1f));
            frame.bankLeft[i] = Mathf.Clamp(bankWidth * (1f + bankJitter * Noise(arc * 1.7f, 2f)), 0.2f, frame.left[i] * 0.85f);
            frame.bankRight[i] = Mathf.Clamp(bankWidth * (1f + bankJitter * Noise(arc * 1.7f, 3f)), 0.2f, frame.right[i] * 0.85f);
        }

        return true;
    }

    // The rim as a closed polygon in the river's own space
    private static void BuildRim(Frame frame, List<Vector2> rim)
    {
        rim.Clear();

        for (int i = 0; i < frame.Count; i++)
            rim.Add(frame.centre[i] + frame.normal[i] * frame.left[i]);

        for (int i = frame.Count - 1; i >= 0; i--)
            rim.Add(frame.centre[i] - frame.normal[i] * frame.right[i]);
    }

    private static float SignedArea(List<Vector2> polygon)
    {
        float area = 0f;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            area += a.x * b.y - b.x * a.y;
        }

        return area * 0.5f;
    }

    // The rim in another object's space (the ground's), anticlockwise, ready for the ground to cut out. False with the reason
    // if the river can't be made or its edges cross.
    public bool TryGetRim(Transform space, List<Vector2> rim, out string problem, bool validate = true)
    {
        rim.Clear();

        if (!Compute(out Frame frame, out problem))
            return false;

        List<Vector2> local = new List<Vector2>();
        BuildRim(frame, local);
        Matrix4x4 toSpace = space.worldToLocalMatrix * transform.localToWorldMatrix;

        foreach (Vector2 point in local)
        {
            Vector3 mapped = toSpace.MultiplyPoint3x4(new Vector3(point.x, 0f, point.y));
            rim.Add(new Vector2(mapped.x, mapped.z));
        }

        if (SignedArea(rim) < 0f)
            rim.Reverse();

        if (validate && !Level_SurfaceShape.Valid(rim.ToArray()))
        {
            problem = "The river's edges cross themselves. Straighten the bends or narrow the river.";
            return false;
        }

        return true;
    }

    // The bank's cross-section from the rim to the bed: chunky steps, each a slope down then a flat
    private Row[] Profile()
    {
        int count = Mathf.Clamp(steps, 1, 4);
        List<Row> rows = new List<Row> { new Row { inset = 0f, y = 0f } };
        float share = 1f / count;

        for (int s = 1; s <= count; s++)
        {
            rows.Add(new Row { inset = (s - 1) * share + stepSlope * share, y = -depth * s / count });

            if (s < count)
                rows.Add(new Row { inset = s * share, y = -depth * s / count });
        }

        // Scaled so the foot of the last slope is exactly the bank width
        float last = rows[rows.Count - 1].inset;

        for (int i = 0; i < rows.Count; i++)
            rows[i] = new Row { inset = rows[i].inset / last, y = rows[i].y };

        return rows.ToArray();
    }

    private Color ColourAt(float y)
    {
        float below = -y;
        float level = Mathf.Min(waterLevel, depth);

        if (below <= level)
            return Color.Lerp(rimColour, waterlineColour, level > 0.001f ? below / level : 1f);

        return Color.Lerp(waterlineColour, bedColour, Mathf.Clamp01((below - level) / Mathf.Max(0.001f, depth - level)));
    }

    // A point on a bank row: the sample's centre pushed out sideways, left or right
    private static Vector3 Position(Frame frame, int i, bool left, float inset, float y)
    {
        float reach = (left ? frame.left[i] : frame.right[i]) - inset;
        Vector2 p = frame.centre[i] + frame.normal[i] * (left ? reach : -reach);
        return new Vector3(p.x, y, p.y);
    }

    // A quad of two triangles over four points, whichever way they wind
    private static void Quad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }

#if UNITY_EDITOR
    private const string BankMaterialPath = "Assets/Art/Level/Cliff_Mat.mat";

    public bool ColliderStale { get; private set; }

    // Why the river can't be made, or null if it can (for the inspector)
    public string Problem()
    {
        TryGetRim(transform, new List<Vector2>(), out string problem);
        return problem;
    }

    // A fingerprint of the rim in another object's space, so the ground rebuilds when the river changes shape or moves
    public string RimKey(Transform space)
    {
        List<Vector2> rim = new List<Vector2>();

        if (!TryGetRim(space, rim, out _, false))
            return "invalid";

        System.Text.StringBuilder key = new System.Text.StringBuilder();

        foreach (Vector2 point in rim)
            key.Append(point.x.ToString("F2")).Append(',').Append(point.y.ToString("F2")).Append(';');

        return Hash128.Compute(key.ToString()).ToString();
    }

    protected override string InputKey()
        => Level_BuildKey.Of(this, null, "banks", "water", "blocker", "bankMesh", "waterMesh", "blockerMesh", "vertices", "buildMs");

    protected override bool HasSavedOutput => bankMesh != null;
    protected override void RebuildNow() => Build();

    private void OnEnable() => Level_Ground.RequestRebuild();
    private void OnDisable() => Level_Ground.RequestRebuild();

    protected override void OnValidate()
    {
        base.OnValidate();
        Level_Ground.RequestRebuild();
    }

    [InspectorButton("Rebuild")]
    private void Rebuild() => Build();

    // Builds the banks, water and blocker. The two colliders are slow to cook, so a drag can leave them for the release.
    public void Build(bool colliders = true)
    {
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

        if (!Compute(out Frame frame, out string problem))
        {
            Debug.LogWarning($"{name}: {problem} The last build is kept.", this);
            return;
        }

        Row[] rows = Profile();
        int count = frame.Count;
        int total = 0;

        // Banks and bed, flat-shaded so the steps stay chunky
        List<Vector3> positions = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Color> colours = new List<Color>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        int columns = rows.Length * 2;
        Vector3[,] grid = new Vector3[count, columns];

        for (int i = 0; i < count; i++)
        {
            for (int k = 0; k < rows.Length; k++)
            {
                grid[i, k] = Position(frame, i, true, rows[k].inset * frame.bankLeft[i], rows[k].y);
                grid[i, columns - 1 - k] = Position(frame, i, false, rows[k].inset * frame.bankRight[i], rows[k].y);
            }
        }

        for (int i = 0; i + 1 < count; i++)
        {
            for (int j = 0; j + 1 < columns; j++)
            {
                Vector3 a = grid[i, j];
                Vector3 b = grid[i + 1, j];
                Vector3 c = grid[i + 1, j + 1];
                Vector3 d = grid[i, j + 1];
                Vector3 normal = Vector3.Cross(b - a, d - a);

                if (normal.sqrMagnitude < 1e-10f)
                    continue;

                normal.Normalize();

                if (normal.y < 0f)
                    normal = -normal;

                int first = positions.Count;
                Vector3[] corner = { a, b, c, d };

                for (int v = 0; v < 4; v++)
                {
                    positions.Add(corner[v]);
                    normals.Add(normal);
                    colours.Add(ColourAt(corner[v].y));
                    uvs.Add(new Vector2(corner[v].x, corner[v].z) * 0.25f);
                }

                // Wind it to face the way the normal points
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f)
                    Quad(triangles, first, first + 1, first + 2, first + 3);
                else
                    Quad(triangles, first, first + 3, first + 2, first + 1);
            }
        }

        bankMesh = Fill(bankMesh, "River Banks", positions, normals, colours, uvs, triangles);
        total += positions.Count;

        // Water: a strip down the middle at the waterline, with a vertex between the shores
        float wl = Mathf.Min(waterLevel, depth - 0.05f);
        float waterInset = WaterInset(rows, wl);
        positions.Clear();
        normals.Clear();
        colours.Clear();
        uvs.Clear();
        triangles.Clear();

        for (int i = 0; i < count; i++)
        {
            float reachLeft = frame.left[i] - waterInset * frame.bankLeft[i] + 0.06f;
            float reachRight = frame.right[i] - waterInset * frame.bankRight[i] + 0.06f;
            Vector2 centre = frame.centre[i];
            Vector2 leftPoint = centre + frame.normal[i] * reachLeft;
            Vector2 rightPoint = centre - frame.normal[i] * reachRight;
            float u = frame.arc[i] / waterUvLength;

            positions.Add(new Vector3(leftPoint.x, -wl, leftPoint.y));
            positions.Add(new Vector3(centre.x, -wl, centre.y));
            positions.Add(new Vector3(rightPoint.x, -wl, rightPoint.y));
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
            colours.Add(new Color(0f, 0f, 0f, 1f));
            colours.Add(Color.white);
            colours.Add(new Color(0f, 0f, 0f, 1f));
            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, 0.5f));
            uvs.Add(new Vector2(u, 1f));
        }

        for (int i = 0; i + 1 < count; i++)
        {
            int a = i * 3;
            int b = (i + 1) * 3;

            // Up-facing whichever way the river runs
            Quad(triangles, a, b, b + 1, a + 1);
            Quad(triangles, a + 1, b + 1, b + 2, a + 2);
        }

        FaceUp(positions, triangles);
        waterMesh = Fill(waterMesh, "River Water", positions, normals, colours, uvs, triangles);
        total += positions.Count;

        // Blocker: a wall along each bank, from just under the ground up to the blocker height
        Mesh blockerData = null;

        if (blockGoblins)
        {
            positions.Clear();
            normals.Clear();
            colours.Clear();
            uvs.Clear();
            triangles.Clear();

            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                int start = positions.Count;

                for (int i = 0; i < count; i++)
                {
                    float rim = left ? frame.left[i] : frame.right[i];
                    float inner = Mathf.Max(0f, rim - blockerWidth);
                    float sign = left ? 1f : -1f;
                    Vector2 outer = frame.centre[i] + frame.normal[i] * (rim * sign);
                    Vector2 inside = frame.centre[i] + frame.normal[i] * (inner * sign);

                    positions.Add(new Vector3(outer.x, -0.3f, outer.y));
                    positions.Add(new Vector3(outer.x, blockerHeight, outer.y));
                    positions.Add(new Vector3(inside.x, blockerHeight, inside.y));
                    positions.Add(new Vector3(inside.x, -0.3f, inside.y));
                }

                for (int i = 0; i + 1 < count; i++)
                {
                    int a = start + i * 4;
                    int b = start + (i + 1) * 4;
                    Quad(triangles, a, b, b + 1, a + 1);         // outer wall
                    Quad(triangles, a + 1, b + 1, b + 2, a + 2); // top
                    Quad(triangles, a + 2, b + 2, b + 3, a + 3); // inner wall
                }
            }

            for (int v = 0; v < positions.Count; v++)
            {
                normals.Add(Vector3.up);
                colours.Add(Color.white);
                uvs.Add(Vector2.zero);
            }

            blockerMesh = Fill(blockerMesh, "River Blocker", positions, normals, colours, uvs, triangles);
            blockerData = blockerMesh;
            total += positions.Count;
        }

        // The children that show and collide with them
        Level_Ground ground = FindFirstObjectByType<Level_Ground>();
        int groundLayer = ground != null ? ground.gameObject.layer : gameObject.layer;
        int buildingLayer = LayerMask.NameToLayer("Building");

        banks = EnsureChild(banks, "Banks", groundLayer);
        Place(banks, bankMesh, bankMaterial != null ? bankMaterial : LoadBankMaterial(), true);
        ApplyCollider(banks, bankCollider ? bankMesh : null, colliders);

        water = EnsureChild(water, "Water", gameObject.layer);
        Place(water, waterMesh, waterMaterial != null ? waterMaterial : DefaultMaterial(), false);

        if (blockerData != null)
        {
            blocker = EnsureChild(blocker, "Blocker", buildingLayer >= 0 ? buildingLayer : gameObject.layer);
            ApplyCollider(blocker, blockerData, colliders);
        }
        else if (blocker != null)
        {
            DestroyImmediate(blocker);
            blocker = null;
            blockerMesh = null;
        }

        vertices = total;
        buildMs = (float)watch.Elapsed.TotalMilliseconds;
        RememberBuild();

        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }

    // Where the waterline falls on the bank rows, as 0 to 1 in from the rim
    private static float WaterInset(Row[] rows, float level)
    {
        for (int k = 0; k + 1 < rows.Length; k++)
        {
            if (rows[k].y >= -level && -level >= rows[k + 1].y)
            {
                float span = rows[k].y - rows[k + 1].y;
                return Mathf.Lerp(rows[k].inset, rows[k + 1].inset, span > 1e-5f ? (rows[k].y + level) / span : 0f);
            }
        }

        return 1f;
    }

    // Makes every triangle face up (the water is a flat sheet), whichever way it was wound
    private static void FaceUp(List<Vector3> positions, List<int> triangles)
    {
        for (int t = 0; t < triangles.Count; t += 3)
        {
            Vector3 cross = Vector3.Cross(positions[triangles[t + 1]] - positions[triangles[t]], positions[triangles[t + 2]] - positions[triangles[t]]);

            if (cross.y < 0f)
                (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
        }
    }

    private static Mesh Fill(Mesh mesh, string meshName, List<Vector3> positions, List<Vector3> normals, List<Color> colours,
        List<Vector2> uvs, List<int> triangles)
    {
        if (mesh == null)
            mesh = new Mesh { name = meshName };

        mesh.Clear();
        mesh.indexFormat = positions.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetColors(colours);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private GameObject EnsureChild(GameObject existing, string childName, int layer)
    {
        GameObject child = existing;

        if (child == null)
        {
            child = new GameObject(childName);
            child.transform.SetParent(transform, false);
        }

        child.layer = layer;
        return child;
    }

    private static T Get<T>(GameObject target) where T : Component
        => target.TryGetComponent(out T found) ? found : target.AddComponent<T>();

    private static void Place(GameObject target, Mesh mesh, Material material, bool shadows)
    {
        Get<MeshFilter>(target).sharedMesh = mesh;
        MeshRenderer meshRenderer = Get<MeshRenderer>(target);
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Colliders cook slowly, so while dragging they're left for the release
    private void ApplyCollider(GameObject target, Mesh mesh, bool now)
    {
        if (mesh == null)
        {
            if (target.TryGetComponent(out MeshCollider existing))
                DestroyImmediate(existing);

            return;
        }

        if (!now)
        {
            ColliderStale = true;
            return;
        }

        MeshCollider meshCollider = Get<MeshCollider>(target);
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
        ColliderStale = false;
    }

    // The shared bank material (the cliffs'), made the first time
    public static Material LoadBankMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BankMaterialPath);

        if (material != null)
            return material;

        Shader shader = Shader.Find("Goblins/Level");

        if (shader == null)
            return DefaultMaterial();

        material = new Material(shader) { name = "Cliff_Mat" };
        AssetDatabase.CreateAsset(material, BankMaterialPath);
        return material;
    }

    private static Material DefaultMaterial()
        => UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null
            ? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.defaultMaterial
            : null;
#endif
}
