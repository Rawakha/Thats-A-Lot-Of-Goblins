using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// One node of an Egyptian wall: where it stands, how tall and thick the wall is there (multipliers, from Level_PathNode),
// and whether the section leading to the next node is left open (for an archway or a pillar pair later).
[Serializable]
public class Level_EgyptWallNode : Level_PathNode
{
    // Saved so hand-moved stones stay attached to their node when nodes are inserted or removed. Filled in on generate.
    [HideInInspector] public string id;
    [Tooltip("Leave the stretch from this node to the next one empty, with no wall. The last node ignores it.")]
    public bool gapAfter;
}

public enum Level_EgyptDamageKind
{
    [Tooltip("A hole through the wall, with wall left above it and below it.")]
    Hole,
    [Tooltip("The top of the wall is broken off, leaving ragged stumps.")]
    BrokenTop
}

// One piece of damage on a wall. It sits on the wall's line (the Scene handles keep it there) and removes stone.
[Serializable]
public class Level_EgyptWallDamage
{
    public Level_EgyptDamageKind kind;
    [Tooltip("Where it is, in the wall's space. The Scene handle keeps this on the wall.")]
    public Vector3 position;
    [Tooltip("How wide the damage is along the wall, metres.")]
    [Min(0.3f)] public float width = 2.2f;
    [Tooltip("Hole only: height of the bottom edge of the hole above the ground, metres. 0 makes a doorway-like gap.")]
    [Min(0f)] public float bottom = 1f;
    [Tooltip("Hole: height of the top edge of the hole. Broken Top: how tall the wall still stands here. Metres.")]
    [Min(0f)] public float top = 2.6f;
    [Tooltip("Broken Top only: how ragged the break is. 0 is a clean cut, 1 has stumps from nothing up to the full height.")]
    [Range(0f, 1f)] public float roughness = 0.4f;
}

// An Egyptian ruin wall: a line of nodes, built as columns of stacked sandstone blocks with a terracotta band low down
// and an overhanging cap on every column. Each column gets its own random height (and width), so the top edge steps up
// and down like a ruin instead of one flat line. Pillars standing in the wall (see Level_EgyptPillar) cut it: the blocks
// either side are shortened to meet them, so nothing overlaps. Damage (holes and broken tops) removes stone in a chosen
// spot; the blocks there are split at its edges, so it can be any width. Built on the level tool foundation
// (Level_Tool): handles, editable stones, one-mesh bake and Undo all come from there.
//
// Setup: GameObject > Level > Egyptian > Wall. White squares move nodes, green dots add, Ctrl/Cmd-click deletes, yellow cones
// set wall height at a node, orange cubes set thickness. Raise "Lower Variation" / "Raise Variation" for more ruined
// tops, change "Seed" to reshuffle. Damage: in the inspector press Add Hole or Add Broken Top (it lands on the wall nearest
// where the Scene view looks), then drag its handles (see Level_EgyptWallEditor). Move or scale any stone by hand under
// Editable Sources; it keeps its offset. Bake Into One Mesh when finished, then save the scene and rebake navigation.
// GC: nothing at runtime, no ticks or subscriptions. Generating allocates working lists and objects in the editor only.
public class Level_EgyptWall : Level_Tool
{
    private const int MaxColumns = 1024;
    private const int MaxRows = 64;
    private const float MinStretch = 0.4f;
    private const float MinCell = 0.15f; // narrowest piece a column is split into at damage edges
    private const float ShaftRatio = 0.7f; // shaft width / pillar width, as built in Level_EgyptPillar
    private const float ShaftEmbed = 0.05f; // how far the wall's ends sink into the shaft, metres

    [Header("Size")]
    [Tooltip("Wall height at a node with Height 1, metres, before each column's variation.")]
    [SerializeField, Min(0.5f)] private float height = 3.6f;
    [Tooltip("Wall thickness at a node with Thickness 1, metres.")]
    [SerializeField, Min(0.3f)] private float thickness = 1.3f;
    [Tooltip("Roughly how wide each column is along the wall, metres.")]
    [SerializeField, Min(0.5f)] private float blockLength = 2.6f;
    [Tooltip("Height of each stacked stone course, metres.")]
    [SerializeField, Min(0.3f)] private float courseHeight = 1.2f;
    [Tooltip("The small gap between stones, metres.")]
    [SerializeField, Range(0f, 0.1f)] private float jointGap = 0.025f;

    [Header("Ruin Variation")]
    [Tooltip("How much lower a column can be than the wall height. 0.5 means down to half height.")]
    [SerializeField, Range(0f, 0.9f)] private float lowerVariation = 0.45f;
    [Tooltip("How much taller a column can be than the wall height. 0.25 means up to a quarter taller.")]
    [SerializeField, Range(0f, 1f)] private float raiseVariation = 0.2f;
    [Tooltip("Round column heights to whole courses, so the top steps like missing stones. Off gives any height.")]
    [SerializeField] private bool snapToCourses = true;
    [Tooltip("How much column widths differ. 0 is all equal.")]
    [SerializeField, Range(0f, 0.6f)] private float widthVariation = 0.25f;

    [Header("Trim")]
    [Tooltip("Height of the cap on top of each column, metres.")]
    [SerializeField, Min(0.05f)] private float capHeight = 0.25f;
    [Tooltip("How far the cap sticks out past the column on every side, metres.")]
    [SerializeField, Range(0f, 0.4f)] private float capOverhang = 0.12f;
    [Tooltip("Height above the ground where the coloured band starts, metres.")]
    [SerializeField, Min(0f)] private float bandStart = 0.4f;
    [Tooltip("Height of the band, metres. 0 removes it.")]
    [SerializeField, Min(0f)] private float bandHeight = 0.3f;

    [Header("Damage")]
    [Tooltip("Holes and broken tops. Add them with the buttons in the inspector, then drag their handles in the Scene view.")]
    [SerializeField] private List<Level_EgyptWallDamage> damages = new List<Level_EgyptWallDamage>();

    [SerializeField] private List<Level_EgyptWallNode> nodes = new List<Level_EgyptWallNode>
    {
        new Level_EgyptWallNode { position = new Vector3(-8f, 0f, 0f) },
        new Level_EgyptWallNode { position = new Vector3(8f, 0f, 0f) }
    };

    public List<Level_EgyptWallNode> Nodes => nodes;
    public List<Level_EgyptWallDamage> Damages => damages;
    public float Height => height;
    public float Thickness => thickness;

    // Damage placed on one section: how far along it the damage reaches
    private struct DamageSpan
    {
        public int edge;
        public float from, to;
        public Level_EgyptWallDamage damage;
    }

    protected override void BuildPieces(List<Level_ToolPiece> pieces)
    {
        if (nodes == null || nodes.Count < 2 || nodes.Contains(null))
            throw new InvalidOperationException("A wall needs at least two nodes.");

        EnsureIds();
        List<Footprint> pillars = new List<Footprint>();
        GatherPillars(pillars);

        // Each piece of damage belongs to the section nearest it
        List<DamageSpan> spans = new List<DamageSpan>();

        if (damages != null)
        {
            foreach (Level_EgyptWallDamage damage in damages)
            {
                if (damage != null && Closest(damage.position, out int closestEdge, out float along, out _, out _))
                    spans.Add(new DamageSpan { edge = closestEdge, from = along - damage.width * 0.5f, to = along + damage.width * 0.5f, damage = damage });
            }
        }

        for (int edge = 0; edge + 1 < nodes.Count; edge++)
        {
            Level_EgyptWallNode a = nodes[edge];
            Level_EgyptWallNode b = nodes[edge + 1];

            if (a.gapAfter)
                continue;

            Vector3 start = new Vector3(a.position.x, 0f, a.position.z);
            Vector3 delta = new Vector3(b.position.x, 0f, b.position.z) - start;
            float length = delta.magnitude;

            if (length < 0.25f)
                throw new InvalidOperationException($"Nodes {edge} and {edge + 1} are less than 0.25 metres apart.");

            Vector3 direction = delta / length;
            Quaternion rotation = Quaternion.LookRotation(direction);

            // The stretches of this section that pillars don't stand in. The wall is built in each one separately, so the
            // blocks beside a pillar are shortened to meet it instead of overlapping it.
            List<Vector2> free = FreeStretches(start, direction, length, thickness * Mathf.Max(a.thickness, b.thickness), pillars);

            for (int stretch = 0; stretch < free.Count; stretch++)
            {
                float from = free[stretch].x;
                float span = free[stretch].y - from;
                string prefix = stretch == 0 ? a.id : $"{a.id}/s{stretch}";
                int columns = Mathf.Max(1, Mathf.RoundToInt(span / blockLength));

                if (columns > MaxColumns)
                    throw new InvalidOperationException("That section is too long for its Block Length. Make the blocks longer.");

                // Column widths: equal shares nudged by the seed, then scaled so they fill the stretch exactly
                float[] widths = new float[columns];
                float total = 0f;

                for (int c = 0; c < columns; c++)
                {
                    widths[c] = 1f + widthVariation * (Random(prefix, c, 1) * 2f - 1f);
                    total += widths[c];
                }

                float cursor = from;

                for (int c = 0; c < columns; c++)
                {
                    float width = span * widths[c] / total;
                    BuildColumn(pieces, a, b, edge, prefix, c, start, direction, rotation, cursor, cursor + width, length, spans);
                    cursor += width;
                }
            }
        }
    }

    // A pillar's footprint on the ground, in the wall's space: where it stands, which way it faces and how wide it is
    private struct Footprint
    {
        public Vector3 position;
        public float yaw;
        public float width;
    }

    private void GatherPillars(List<Footprint> list)
    {
#if UNITY_EDITOR
        if (sources == null)
            return;

        foreach (Level_EgyptPillar pillar in sources.GetComponentsInChildren<Level_EgyptPillar>(true))
        {
            // A pillar lying on its side no longer stands in the wall's line. Its own switch, not its parents': a bake
            // hides Editable Sources, and a pillar that vanished then would change the layout, regenerate the wall and
            // put it back into Editing, so the bake never finished.
            if (!pillar.gameObject.activeSelf || Mathf.Abs(pillar.Roll) > 20f)
                continue;

            float turned = (Quaternion.Inverse(transform.rotation) * pillar.transform.rotation).eulerAngles.y;
            list.Add(new Footprint
            {
                position = transform.InverseTransformPoint(pillar.transform.position),
                yaw = turned + pillar.Yaw,
                width = pillar.Width
            });
        }
#endif
    }

    // Distances along the section (from, to) with no pillar in the way. Pillars that touch the wall's line cut a gap, which is
    // widened to their rotated footprint; bits too short to hold a block are dropped.
    private List<Vector2> FreeStretches(Vector3 start, Vector3 direction, float length, float wallThickness, List<Footprint> pillars)
    {
        List<Vector2> cuts = new List<Vector2>();
        Vector3 side = new Vector3(direction.z, 0f, -direction.x);

        foreach (Footprint pillar in pillars)
        {
            float radians = pillar.yaw * Mathf.Deg2Rad;
            Vector3 facing = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            Vector3 across = new Vector3(facing.z, 0f, -facing.x);
            // The wall meets the narrower shaft (the base and capital are wider and sit below and above it), pushed a
            // little into it so no gap shows
            float half = pillar.width * 0.5f * ShaftRatio - ShaftEmbed;
            float alongHalf = half * (Mathf.Abs(Vector3.Dot(facing, direction)) + Mathf.Abs(Vector3.Dot(across, direction)));
            float sideHalf = half * (Mathf.Abs(Vector3.Dot(facing, side)) + Mathf.Abs(Vector3.Dot(across, side)));
            Vector3 offset = new Vector3(pillar.position.x - start.x, 0f, pillar.position.z - start.z);
            float along = Vector3.Dot(offset, direction);

            if (Mathf.Abs(Vector3.Dot(offset, side)) >= sideHalf + wallThickness * 0.5f)
                continue;

            if (along + alongHalf <= 0f || along - alongHalf >= length)
                continue;

            cuts.Add(new Vector2(along - alongHalf, along + alongHalf));
        }

        cuts.Sort((x, y) => x.x.CompareTo(y.x));
        List<Vector2> free = new List<Vector2>();
        float cursor = 0f;

        foreach (Vector2 cut in cuts)
        {
            if (cut.x - cursor >= MinStretch)
                free.Add(new Vector2(cursor, cut.x));

            cursor = Mathf.Max(cursor, cut.y);
        }

        if (length - cursor >= MinStretch)
            free.Add(new Vector2(cursor, length));

        return free;
    }

    // The point on the wall's line nearest a point (wall space), for keeping damage on the wall. Gaps have no wall, so
    // they're skipped. Returns false if there's no wall at all.
    public bool SnapToWall(Vector3 local, out Vector3 point, out Vector3 direction)
        => Closest(local, out _, out _, out point, out direction);

    private bool Closest(Vector3 local, out int edge, out float along, out Vector3 point, out Vector3 direction)
    {
        edge = -1;
        along = 0f;
        point = default;
        direction = Vector3.right;
        float best = float.MaxValue;

        if (nodes == null)
            return false;

        for (int i = 0; i + 1 < nodes.Count; i++)
        {
            if (nodes[i] == null || nodes[i + 1] == null || nodes[i].gapAfter)
                continue;

            Vector3 a = new Vector3(nodes[i].position.x, 0f, nodes[i].position.z);
            Vector3 line = new Vector3(nodes[i + 1].position.x, 0f, nodes[i + 1].position.z) - a;
            float length = line.magnitude;

            if (length < 0.25f)
                continue;

            Vector3 unit = line / length;
            float reach = Mathf.Clamp(Vector3.Dot(new Vector3(local.x, 0f, local.z) - a, unit), 0f, length);
            Vector3 near = a + unit * reach;
            float distance = (new Vector3(local.x, 0f, local.z) - near).sqrMagnitude;

            if (distance >= best)
                continue;

            best = distance;
            edge = i;
            along = reach;
            point = near;
            direction = unit;
        }

        return edge >= 0;
    }

#if UNITY_EDITOR
    // Pillars are part of the layout: adding, moving, turning or removing one regenerates the wall
    protected override string InputKey() => base.InputKey() + PillarKey();

    public string PillarKey()
    {
        List<Footprint> pillars = new List<Footprint>();
        GatherPillars(pillars);
        System.Text.StringBuilder key = new System.Text.StringBuilder();

        foreach (Footprint pillar in pillars)
            key.Append('|').Append(pillar.position.x.ToString("F3")).Append(',').Append(pillar.position.z.ToString("F3"))
                .Append(',').Append(pillar.yaw.ToString("F1")).Append(',').Append(pillar.width.ToString("F2"));

        return key.ToString();
    }
#endif

    // Builds one of a section's columns. Damage that overlaps it splits it at the damage's edges into cells; each cell is
    // built whole, or (a broken top) as a row of ragged stumps.
    private void BuildColumn(List<Level_ToolPiece> pieces, Level_EgyptWallNode a, Level_EgyptWallNode b, int edge, string prefix,
        int column, Vector3 start, Vector3 direction, Quaternion rotation, float from, float to, float length, List<DamageSpan> spans)
    {
        List<float> marks = new List<float> { from, to };

        foreach (DamageSpan span in spans)
        {
            if (span.edge != edge || span.to <= from + MinCell || span.from >= to - MinCell)
                continue;

            marks.Add(Mathf.Clamp(span.from, from, to));
            marks.Add(Mathf.Clamp(span.to, from, to));
        }

        marks.Sort();

        // Cells between the marks, dropping any too narrow to be a stone
        List<float> edges = new List<float> { from };

        for (int i = 1; i < marks.Count - 1; i++)
        {
            if (marks[i] - edges[edges.Count - 1] >= MinCell && to - marks[i] >= MinCell)
                edges.Add(marks[i]);
        }

        edges.Add(to);

        for (int cell = 0; cell + 1 < edges.Count; cell++)
        {
            float lo = edges[cell];
            float hi = edges[cell + 1];
            float middle = (lo + hi) * 0.5f;
            Level_EgyptWallDamage damage = null;

            foreach (DamageSpan span in spans)
            {
                if (span.edge == edge && middle > span.from && middle < span.to)
                {
                    damage = span.damage;
                    break;
                }
            }

            string id = edges.Count == 2 ? $"{prefix}/{column}" : $"{prefix}/{column}.{cell}";

            if (damage != null && damage.kind == Level_EgyptDamageKind.BrokenTop)
            {
                int stumps = Mathf.Max(1, Mathf.RoundToInt((hi - lo) / (blockLength * 0.5f)));

                for (int s = 0; s < stumps; s++)
                {
                    float stumpLo = Mathf.Lerp(lo, hi, (float)s / stumps);
                    float stumpHi = Mathf.Lerp(lo, hi, (float)(s + 1) / stumps);
                    BuildCell(pieces, a, b, $"{id}.{s}", prefix, column, start, direction, rotation, stumpLo, stumpHi, length, damage, true);
                }
            }
            else
            {
                BuildCell(pieces, a, b, id, prefix, column, start, direction, rotation, lo, hi, length, damage, false);
            }
        }
    }

    private void BuildCell(List<Level_ToolPiece> pieces, Level_EgyptWallNode a, Level_EgyptWallNode b, string id, string heightKey,
        int column, Vector3 start, Vector3 direction, Quaternion rotation, float lo, float hi, float length,
        Level_EgyptWallDamage damage, bool stump)
    {
        float middle = (lo + hi) * 0.5f;
        float width = hi - lo;
        float t = middle / length;
        Vector3 position = start + direction * middle;
        float nominal = height * Mathf.Lerp(a.height, b.height, t);
        float wallThickness = thickness * Mathf.Lerp(a.thickness, b.thickness, t);

        // The column's own height (shared by all its cells): somewhere between the lowest and tallest allowed
        float factor = Mathf.Lerp(1f - lowerVariation, 1f + raiseVariation, Random(heightKey, column, 0));
        float columnHeight = nominal * factor;
        columnHeight = snapToCourses
            ? Mathf.Max(1, Mathf.RoundToInt(columnHeight / courseHeight)) * courseHeight
            : Mathf.Max(courseHeight * 0.5f, columnHeight);

        bool hole = false;
        float holeBottom = 0f;
        float holeTop = 0f;

        if (damage != null)
        {
            // A broken top, or a hole reaching the top of the wall, leaves a lower wall standing
            float remaining = -1f;

            if (damage.kind == Level_EgyptDamageKind.BrokenTop)
                remaining = damage.top;
            else if (damage.top >= columnHeight - 0.05f)
                remaining = damage.bottom;
            else
            {
                hole = true;
                holeBottom = damage.bottom;
                holeTop = damage.top;
            }

            if (remaining >= 0f)
            {
                if (stump)
                    remaining = Mathf.Lerp(remaining * (1f - damage.roughness), remaining, Random(id, 0, 2));

                if (snapToCourses)
                    remaining = Mathf.Round(remaining / courseHeight) * courseHeight;

                columnHeight = Mathf.Min(columnHeight, remaining);

                if (columnHeight < 0.15f)
                    return;
            }
        }

        // Row edges: every course line, the hole's two edges and the band's two edges, sorted
        List<float> levels = new List<float> { 0f, columnHeight };

        for (float y = courseHeight; y < columnHeight - 0.05f; y += courseHeight)
        {
            if (levels.Count > MaxRows)
                throw new InvalidOperationException("A column is too tall for its Course Height. Make the courses taller.");

            levels.Add(y);
        }

        float bandEnd = bandStart + bandHeight;

        if (bandHeight > 0f)
        {
            if (bandStart > 0.05f && bandStart < columnHeight - 0.05f) levels.Add(bandStart);
            if (bandEnd < columnHeight - 0.05f) levels.Add(bandEnd);
        }

        if (hole)
        {
            if (holeBottom > 0.05f && holeBottom < columnHeight - 0.05f) levels.Add(holeBottom);
            if (holeTop > 0.05f && holeTop < columnHeight - 0.05f) levels.Add(holeTop);
        }

        levels.Sort();

        int row = 0;

        for (int i = 0; i + 1 < levels.Count; i++)
        {
            float low = levels[i];
            float high = levels[i + 1];

            if (high - low < 0.04f)
                continue;

            float centre = (low + high) * 0.5f;

            if (hole && centre > holeBottom && centre < holeTop)
            {
                row++;
                continue;
            }

            bool inBand = bandHeight > 0f && centre >= bandStart && centre <= bandEnd;
            float gap = Mathf.Min(jointGap, (high - low) * 0.2f);
            Vector3 size = new Vector3(wallThickness, high - low - gap, Mathf.Max(0.1f, width - jointGap));
            pieces.Add(new Level_ToolPiece($"{id}/{row}", position + Vector3.up * (low + gap * 0.5f), rotation, size,
                inBand ? "Band" : "Main", "Wall Stone"));
            row++;
        }

        pieces.Add(new Level_ToolPiece($"{id}/Cap", position + Vector3.up * columnHeight, rotation,
            new Vector3(wallThickness + capOverhang * 2f, capHeight, width - jointGap + capOverhang * 2f), "Trim", "Wall Cap"));
    }

    // Stable random in 0..1 for a column and purpose, from the seed and a key (not the order nodes are in)
    private float Random(string key, int column, int channel)
    {
        unchecked
        {
            int value = seed * 73856093 ^ Animator.StringToHash(key) * 31 + column * 19349663 + channel * 83492791;
            return (float)new System.Random(value).NextDouble();
        }
    }

    private void EnsureIds()
    {
        HashSet<string> used = new HashSet<string>();

        foreach (Level_EgyptWallNode node in nodes)
        {
            if (!string.IsNullOrEmpty(node.id) && used.Add(node.id))
                continue;

            node.id = Guid.NewGuid().ToString("N");
            used.Add(node.id);
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}
