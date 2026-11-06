using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[Serializable]
public struct Level_TreeChoice
{
    public GameObject prefab;

    [Tooltip("How often this one is picked compared with the others. 2 is twice as often as 1; 0 counts as 1.")]
    [Min(0f)] public float weight;
}

// An area you draw with a few nodes (like a Cliff Block) that fills itself with trees from a list of prefabs: spaced
// out, in natural patches, turned and sized at random. Edit it in the Scene view: drag the white squares, click the
// green dots to add corners, hold Ctrl and click a corner to delete it. The trees are rebuilt when you let go.
//
// Each tree is a real prefab instance under a "Trees" child, so prefab edits carry through and they batch or
// instance like any other mesh (give the tree material GPU Instancing). They're generated: don't edit them by hand,
// Rebuild replaces them. Change Seed (or press Reshuffle) for a different layout.
//
// Trees are dropped onto whatever is below on Ground Layers, and skipped where that is something on Avoid Layers
// (cliffs) or the inside of a hole. To make trees block goblins, give the prefab a collider on a layer the
// Flow_System bakes as an obstacle and Bake Obstacles again.
// A level tool: nothing here knows about goblins.
//
// GC: none at runtime (everything is made in the editor). No Update and no subscriptions in play.
[DisallowMultipleComponent]
public class Level_TreeArea : Level_BuiltTool
{
    [Header("Area")]
    [Tooltip("Corners of the area in this object's space. Height is ignored.")]
    [SerializeField] private List<Vector3> corners = new List<Vector3>();

    [Header("Trees")]
    [SerializeField] private List<Level_TreeChoice> prefabs = new List<Level_TreeChoice>();

    [Tooltip("Trees per 100 square metres, before patches thin them out.")]
    [SerializeField, Range(0f, 20f)] private float density = 3f;
    [Tooltip("No two trees closer than this, metres.")]
    [SerializeField, Min(0.1f)] private float spacing = 3f;
    [Tooltip("Keep trees this far in from the area's edge, metres.")]
    [SerializeField, Min(0f)] private float edgeMargin = 1f;
    [Tooltip("Most trees one area makes, so a big area with a high density can't freeze the editor.")]
    [SerializeField, Min(1)] private int maxTrees = 1500;

    [Header("Patches")]
    [Tooltip("0 spreads trees evenly; higher gathers them into clumps with clearings between.")]
    [SerializeField, Range(0f, 1f)] private float patchiness = 0.4f;
    [Tooltip("Size of the clumps and clearings, metres.")]
    [SerializeField, Min(1f)] private float patchSize = 25f;

    [Header("Variation")]
    [Tooltip("Smallest and largest size, as a multiple of the prefab's own scale.")]
    [SerializeField] private Vector2 scale = new Vector2(0.8f, 1.25f);
    [Tooltip("Random turn round the up axis.")]
    [SerializeField] private bool randomTurn = true;
    [Tooltip("Most a tree leans off upright, degrees.")]
    [SerializeField, Range(0f, 20f)] private float lean = 3f;
    [Tooltip("How far trees sit into the ground, metres, so their bases don't float on slopes.")]
    [SerializeField] private float sink = 0.1f;
    [Tooltip("Change to reshuffle every tree.")]
    [SerializeField] private int seed = 1;

    [Header("Ground")]
    [Tooltip("What trees stand on. Rays are cast down onto these.")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [Tooltip("No trees where the ray hits these first (cliffs, buildings).")]
    [SerializeField] private LayerMask avoidLayers;
    [Tooltip("Place trees on this object's floor where nothing is hit at all (no ground collider yet).")]
    [SerializeField] private bool placeWithoutGround = true;

    [Header("Editing")]
    [Tooltip("Corners snap to this grid while dragged, metres. 0 is off.")]
    [SerializeField, Min(0f)] private float snap = 0.5f;
    [Tooltip("Mark the trees Batching Static. Off by default: with the GPU Resident Drawer on, static batching gets in the way of it instancing the trees.")]
    [SerializeField] private bool markStatic;

    [Header("Stats")]
    [SerializeField, ReadOnly] private int trees;
    [SerializeField, ReadOnly] private float buildMs;

    private const string ContainerName = "Trees";

    public List<Vector3> Corners => corners;
    public float Snap => snap;

    private void Reset()
    {
        seed = UnityEngine.Random.Range(1, 100000);
        corners.Clear();
        corners.Add(new Vector3(-12f, 0f, -12f));
        corners.Add(new Vector3(12f, 0f, -12f));
        corners.Add(new Vector3(12f, 0f, 12f));
        corners.Add(new Vector3(-12f, 0f, 12f));

        int ground = LayerMask.NameToLayer("Ground");
        int building = LayerMask.NameToLayer("Building");

        if (ground >= 0)
            groundLayers = 1 << ground;

        if (building >= 0)
            avoidLayers = 1 << building;
    }

    [InspectorButton("Rebuild")]
    private void Rebuild() => Build();

    [InspectorButton("Reshuffle")]
    private void Reshuffle()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Reshuffle Trees");
#endif
        seed = UnityEngine.Random.Range(1, 100000);
        Build();
    }

    [InspectorButton("Clear Trees")]
    private void ClearTrees()
    {
        Transform container = transform.Find(ContainerName);

        if (container != null)
            DestroyImmediate(container.gameObject);

        trees = 0;
    }

    // Throws away the trees and places them again from the corners, prefabs and seed. Editor only: in play the
    // trees saved with the scene are used as they are.
    public void Build()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        ClearTrees();

        float totalWeight = 0f;

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i].prefab != null)
                totalWeight += Weight(prefabs[i]);
        }

        RememberBuild();

        if (corners.Count < 3 || totalWeight <= 0f)
        {
            buildMs = (float)watch.Elapsed.TotalMilliseconds;
            MarkDirty();
            return;
        }

        Transform container = new GameObject(ContainerName).transform;
        container.SetParent(transform, false);

        List<Vector2> outline = new List<Vector2>(corners.Count);

        for (int i = 0; i < corners.Count; i++)
            outline.Add(new Vector2(corners[i].x, corners[i].z));

        Physics.SyncTransforms();

        Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex((uint)seed);
        Vector2 min = outline[0];
        Vector2 max = outline[0];

        for (int i = 1; i < outline.Count; i++)
        {
            min = Vector2.Min(min, outline[i]);
            max = Vector2.Max(max, outline[i]);
        }

        float area = Mathf.Abs(SignedArea(outline));
        int wanted = Mathf.Min(maxTrees, Mathf.RoundToInt(area / 100f * density));
        int attempts = wanted * 15;

        // Placed trees in a grid of Spacing-sized cells, so the spacing check only looks at neighbours
        Dictionary<Vector2Int, List<Vector2>> placed = new Dictionary<Vector2Int, List<Vector2>>();
        Vector2 noiseOffset = new Vector2(random.NextFloat(0f, 1000f), random.NextFloat(0f, 1000f));
        trees = 0;

        for (int attempt = 0; attempt < attempts && trees < wanted; attempt++)
        {
            Vector2 point = new Vector2(random.NextFloat(min.x, max.x), random.NextFloat(min.y, max.y));
            float keep = random.NextFloat();
            float pick = random.NextFloat(0f, totalWeight);
            float size = random.NextFloat(Mathf.Min(scale.x, scale.y), Mathf.Max(scale.x, scale.y));
            float turn = randomTurn ? random.NextFloat(0f, 360f) : 0f;
            Vector2 tilt = random.NextFloat2Direction() * random.NextFloat(0f, lean);

            if (!Inside(outline, point) || NearEdge(outline, point, edgeMargin) || Crowded(placed, point))
                continue;

            // Patches: thin trees out where the noise is low
            if (patchiness > 0f)
            {
                float noise = Mathf.PerlinNoise((point.x + noiseOffset.x) / patchSize, (point.y + noiseOffset.y) / patchSize);

                if (keep > Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, (noise - 0.3f) / 0.4f), patchiness))
                    continue;
            }

            if (!Ground(point, out Vector3 position))
                continue;

            GameObject prefab = Pick(pick);
            GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
            Transform t = tree.transform;
            t.position = position - Vector3.up * sink;
            t.rotation = Quaternion.Euler(tilt.x, 0f, tilt.y) * Quaternion.Euler(0f, turn, 0f) * prefab.transform.rotation;
            t.localScale = prefab.transform.localScale * size;

            if (markStatic)
                GameObjectUtility.SetStaticEditorFlags(tree, GameObjectUtility.GetStaticEditorFlags(tree) | StaticEditorFlags.BatchingStatic);

            Vector2Int cell = Cell(point);

            if (!placed.TryGetValue(cell, out List<Vector2> list))
                placed[cell] = list = new List<Vector2>(2);

            list.Add(point);
            trees++;
        }

        buildMs = (float)watch.Elapsed.TotalMilliseconds;
        MarkDirty();
#endif
    }

#if UNITY_EDITOR
    // A new list entry starts at 0, which would never be picked; treat it as 1
    private static float Weight(Level_TreeChoice choice) => choice.weight > 0f ? choice.weight : 1f;

    private GameObject Pick(float pick)
    {
        GameObject last = null;

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i].prefab == null)
                continue;

            last = prefabs[i].prefab;
            pick -= Weight(prefabs[i]);

            if (pick <= 0f)
                return last;
        }

        return last;
    }

    // Where a tree at this point (in the area's space) stands: on the ground below, unless that's something to avoid
    // or the inside of a hole
    private bool Ground(Vector2 point, out Vector3 position)
    {
        Vector3 floor = transform.TransformPoint(new Vector3(point.x, 0f, point.y));
        Vector3 from = floor + Vector3.up * 500f;
        position = floor;

        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 1000f, groundLayers | avoidLayers, QueryTriggerInteraction.Ignore))
        {
            if ((avoidLayers.value & (1 << hit.collider.gameObject.layer)) != 0)
                return false;

            // Fell through an opening: a hole's wall or bottom, not the ground
            if (hit.point.y < floor.y - 0.5f)
                return false;

            position = hit.point;
            return true;
        }

        return placeWithoutGround;
    }

    private Vector2Int Cell(Vector2 point) => new Vector2Int(Mathf.FloorToInt(point.x / spacing), Mathf.FloorToInt(point.y / spacing));

    private bool Crowded(Dictionary<Vector2Int, List<Vector2>> placed, Vector2 point)
    {
        Vector2Int cell = Cell(point);
        float limit = spacing * spacing;

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (!placed.TryGetValue(new Vector2Int(cell.x + x, cell.y + y), out List<Vector2> list))
                    continue;

                for (int i = 0; i < list.Count; i++)
                {
                    if ((list[i] - point).sqrMagnitude < limit)
                        return true;
                }
            }
        }

        return false;
    }

    private void MarkDirty()
    {
        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

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

    private static bool NearEdge(List<Vector2> polygon, Vector2 point, float margin)
    {
        if (margin <= 0f)
            return false;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 ab = polygon[(i + 1) % polygon.Count] - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;

            if (Vector2.Distance(point, a + ab * t) < margin)
                return true;
        }

        return false;
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

#if UNITY_EDITOR
    #region Editor rebuilds
    protected override string InputKey() => Level_BuildKey.Of(this, null, "trees", "buildMs");

    // Saved trees are adopted rather than re-placed: rebuilding would make every tree again, with new file IDs in the
    // scene, every time a script compiled or play stopped.
    protected override bool HasSavedOutput => transform.Find(ContainerName) != null || !HasTreesToPlace();
    protected override void RebuildNow() => Build();

    private bool HasTreesToPlace()
    {
        if (corners.Count < 3)
            return false;

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i].prefab != null)
                return true;
        }

        return false;
    }
    #endregion
#endif
}
