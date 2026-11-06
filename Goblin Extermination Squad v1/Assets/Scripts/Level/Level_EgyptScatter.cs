using System;
using System.Collections.Generic;
using UnityEngine;

// A zone you draw with a few corners (like a Tree Area) that fills itself with scattered Egyptian bricks, so a spot looks
// like ruins have crumbled there. Each brick is a real editable stone on the level tool foundation (Level_Tool): they are
// seeded, so a rebuild gives the same layout, and you can still move, turn, delete or add any brick by hand under
// Editable Sources. Bake Into One Mesh when finished.
//
// Bricks vary in size, are turned at random and tilted a little, sink slightly into the ground and sometimes carry a second
// brick on top. They are spaced out, and gather into clumps with clearings if Patchiness is raised. Colours come from the
// Egyptian palette: mostly sandstone, with some lighter trim stones and the odd terracotta one.
//
// Setup: GameObject > Level > Egyptian > Ruin Scatter. White squares move corners, green dots add, Ctrl/Cmd-click deletes.
// Raise Density, change Seed to reshuffle. Bricks sit flat on this object's floor (set its Y); they aren't dropped onto
// terrain. It starts off the Building layer, so scattered bricks don't block the flow field; move it there if they should.
// GC: nothing at runtime, no ticks or subscriptions. Generating allocates working lists and objects in the editor only.
public class Level_EgyptScatter : Level_Tool
{
    [Header("Amount")]
    [Tooltip("Bricks per 100 square metres, before patches thin them out.")]
    [SerializeField, Range(0f, 40f)] private float density = 6f;
    [Tooltip("No two bricks closer than this, metres.")]
    [SerializeField, Min(0.1f)] private float spacing = 1.2f;
    [Tooltip("Keep bricks this far in from the zone's edge, metres.")]
    [SerializeField, Min(0f)] private float edgeMargin = 0.3f;
    [Tooltip("Most bricks one zone makes, so a big zone with a high density can't freeze the editor.")]
    [SerializeField, Min(1)] private int maxBricks = 400;

    [Header("Patches")]
    [Tooltip("0 spreads bricks evenly; higher gathers them into clumps with clearings between.")]
    [SerializeField, Range(0f, 1f)] private float patchiness = 0.4f;
    [Tooltip("Size of the clumps and clearings, metres.")]
    [SerializeField, Min(1f)] private float patchSize = 8f;

    [Header("Bricks")]
    [Tooltip("Smallest and largest length of a brick, metres.")]
    [SerializeField] private Vector2 length = new Vector2(0.7f, 1.7f);
    [Tooltip("Smallest and largest height of a brick, metres.")]
    [SerializeField] private Vector2 height = new Vector2(0.3f, 0.7f);
    [Tooltip("Smallest and largest depth of a brick, metres.")]
    [SerializeField] private Vector2 depth = new Vector2(0.5f, 1.1f);
    [Tooltip("Chance a brick has a second brick sitting on it.")]
    [SerializeField, Range(0f, 1f)] private float stackChance = 0.2f;
    [Tooltip("Most a brick tilts off flat, degrees.")]
    [SerializeField, Range(0f, 30f)] private float lean = 8f;
    [Tooltip("How far bricks sit into the ground, metres, so tilted ones don't float.")]
    [SerializeField, Min(0f)] private float sink = 0.08f;

    [Header("Colour")]
    [Tooltip("Chance a brick is the lighter trim colour.")]
    [SerializeField, Range(0f, 1f)] private float trimChance = 0.2f;
    [Tooltip("Chance a brick is the terracotta band colour.")]
    [SerializeField, Range(0f, 1f)] private float bandChance = 0.08f;

    [SerializeField] private List<Vector3> nodes = new List<Vector3>
    {
        new Vector3(-8f, 0f, -8f), new Vector3(8f, 0f, -8f), new Vector3(8f, 0f, 8f), new Vector3(-8f, 0f, 8f)
    };

    public List<Vector3> Nodes => nodes;

    protected override void BuildPieces(List<Level_ToolPiece> pieces)
    {
        if (nodes == null || nodes.Count < 3)
            throw new InvalidOperationException("A zone needs at least three corners.");

        Vector2[] outline = new Vector2[nodes.Count];
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = -min;
        float area = 0f;

        for (int i = 0; i < nodes.Count; i++)
        {
            outline[i] = new Vector2(nodes[i].x, nodes[i].z);
            min = Vector2.Min(min, outline[i]);
            max = Vector2.Max(max, outline[i]);
        }

        // Shoelace formula for the zone's area
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 a = outline[i];
            Vector2 b = outline[(i + 1) % outline.Length];
            area += a.x * b.y - b.x * a.y;
        }

        int target = Mathf.Min(maxBricks, Mathf.RoundToInt(Mathf.Abs(area) * 0.5f * density / 100f));
        Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex((uint)Mathf.Abs(seed) + 1u);
        List<Vector2> placed = new List<Vector2>();
        float minimumGap = spacing * spacing;
        float noiseOffset = (Mathf.Abs(seed) % 1000) * 0.37f;

        // Darts thrown at the zone: kept if inside, clear of the edge, away from other bricks and not in a clearing
        for (int attempt = 0; attempt < target * 30 && placed.Count < target; attempt++)
        {
            Vector2 point = new Vector2(random.NextFloat(min.x, max.x), random.NextFloat(min.y, max.y));

            if (Level_SurfaceShape.SignedDistance(point, outline) <= edgeMargin)
                continue;

            float clump = Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(point.x / patchSize + noiseOffset, point.y / patchSize + noiseOffset));

            if (random.NextFloat() > Mathf.Lerp(1f, clump, patchiness))
                continue;

            bool crowded = false;

            for (int i = 0; i < placed.Count && !crowded; i++)
                crowded = (placed[i] - point).sqrMagnitude < minimumGap;

            if (!crowded)
                placed.Add(point);
        }

        for (int i = 0; i < placed.Count; i++)
        {
            Vector3 size = new Vector3(random.NextFloat(length.x, length.y), random.NextFloat(height.x, height.y),
                random.NextFloat(depth.x, depth.y));
            Quaternion rotation = Quaternion.Euler(random.NextFloat(-lean, lean), random.NextFloat(0f, 360f), random.NextFloat(-lean, lean));
            Vector3 position = new Vector3(placed[i].x, -sink, placed[i].y);
            float colour = random.NextFloat();
            string band = colour < bandChance ? "Band" : colour < bandChance + trimChance ? "Trim" : "Main";
            pieces.Add(new Level_ToolPiece($"Brick {i}", position, rotation, size, band, "Scattered Brick"));

            if (random.NextFloat() < stackChance)
            {
                // A smaller brick on top, a little off-centre and turned
                Vector3 topSize = size * random.NextFloat(0.6f, 0.9f);
                topSize.y = Mathf.Max(0.2f, size.y * random.NextFloat(0.6f, 1f));
                Vector3 offset = rotation * new Vector3(random.NextFloat(-0.2f, 0.2f) * size.x, size.y, random.NextFloat(-0.2f, 0.2f) * size.z);
                pieces.Add(new Level_ToolPiece($"Brick {i} Top", position + offset,
                    rotation * Quaternion.Euler(0f, random.NextFloat(-40f, 40f), 0f), topSize, band, "Scattered Brick"));
            }
        }
    }
}
