using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FlowField : MonoBehaviour
{
    [SerializeField] private Transform goal;
    [SerializeField] private FlowFieldCosts costData;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Grid")]
    [SerializeField] private Grid grid;

    [Header("Debug")]
    [SerializeField] private bool drawGrid = true;
    [SerializeField] private bool drawIntegration = false;
    [SerializeField] private bool drawCostHandles = false;
    [SerializeField] private bool drawDirections = false;

    [Header("Build")]
    [SerializeField] private byte[] cost;
    [SerializeField] private ushort[] integration;
    [SerializeField] private Vector2[] directions;

    const ushort Unreachable = ushort.MaxValue;

    static readonly Vector2Int[] CardinalOffsets =
    {
        new Vector2Int( 1,  0),
        new Vector2Int( 0,  1),
        new Vector2Int(-1,  0),
        new Vector2Int( 0, -1)
    };

    static readonly Vector2Int[] AllOffsets =
    {
        new Vector2Int( 1,  0),
        new Vector2Int(-1,  0),
        new Vector2Int( 0,  1),
        new Vector2Int( 0, -1),
        new Vector2Int( 1,  1),
        new Vector2Int( 1, -1),
        new Vector2Int(-1,  1),
        new Vector2Int(-1, -1),
    };

    [InspectorButton()]
    public void BakeField()
    {
        if (!BakeCost())
            return;

        if (goal == null)
        {
            Debug.LogError("FlowField: no goal Transfrom assigned.", this);
            return;
        }

        BuildIntegration(grid.ToCell(goal.position));
        BuildDirections();
    }

    private bool BakeCost()
    {
        if (costData == null)
        {
            Debug.LogError("FlowField: no costs asset assigned.", this);
            return false;
        }

        if (cost == null || cost.Length != grid.CellCount)
            cost = new byte[grid.CellCount];

        Vector3 halfExtents = new Vector3(grid.cellSize, 2f, grid.cellSize) * 0.5f;

        // Find Impassable
        for (int i = 0; i < cost.Length; i++)
        {
            Vector3 centre = grid.CellCentre(i);

            bool blocked = Physics.CheckBox(centre, halfExtents, Quaternion.identity, obstacleLayer);
            if (blocked)
            {
                cost[i] = costData.Impassable;
                continue;
            }

            cost[i] = costData.Default;
        }

        // Inflate cells adjacent to walls
        for (int i = 0; i < cost.Length; i++)
        {
            if (cost[i] == costData.Impassable)
                continue;

            Vector2Int cell = new Vector2Int(i % grid.width, i / grid.width);

            for (int n = 0; n < 4; n++)
            {
                Vector2Int nb = cell + CardinalOffsets[n];
                if (!grid.InBounds(nb))
                    continue;

                if (cost[grid.ToIndex(nb)] == costData.Impassable)
                {
                    cost[i] = costData.NearWall;
                    break;
                }
            }
        }

        return true;
    }

    private void BuildIntegration(Vector2Int goal)
    {
        if (integration == null || integration.Length != grid.CellCount)
            integration = new ushort[grid.CellCount];

        for (int i = 0; i < integration.Length; i++)
            integration[i] = Unreachable;

        if (!grid.InBounds(goal))
            return;

        int goalIndex = grid.ToIndex(goal);
        integration[goalIndex] = 0;

        if (cost[goalIndex] == costData.Impassable)
        {
            Debug.LogWarning("FlowField: goal cell is impassable.", this);
            return;
        }

        Queue<int> open = new Queue<int>(grid.CellCount);
        open.Enqueue(goalIndex);

        while (open.Count > 0)
        {
            int current = open.Dequeue();
            Vector2Int cell = new Vector2Int(current % grid.width, current / grid.width);

            for (int n = 0; n < 4; n++)
            {
                Vector2Int nb = cell + CardinalOffsets[n];
                if (!grid.InBounds(nb))
                    continue;

                int ni = grid.ToIndex(nb);
                if (cost[ni] == costData.Impassable) continue;

                int newCost = integration[current] + cost[ni];
                if (newCost >= integration[ni]) continue;

                integration[ni] = (ushort)newCost;
                open.Enqueue(ni);
            }
        }
    }

    private void BuildDirections()
    {
        if (directions == null || directions.Length != grid.CellCount)
            directions = new Vector2[grid.CellCount];

        // Base Directions
        for (int i = 0; i < directions.Length; i++)
        {
            directions[i] = Vector2.zero;

            if (integration[i] == Unreachable || integration[i] == 0)
                continue;

            Vector2Int cell = new Vector2Int(i % grid.width, i / grid.width);

            ushort best = integration[i];
            Vector2Int bestOffset = Vector2Int.zero;

            for (int n = 0; n < 8; n++)
            {
                Vector2Int offset = AllOffsets[n];
                Vector2Int nb = cell + offset;
                if (!grid.InBounds(nb)) continue;

                if (offset.x != 0 && offset.y != 0)
                {
                    Vector2Int sideA = new Vector2Int(cell.x + offset.x, cell.y);
                    Vector2Int sideB = new Vector2Int(cell.x, cell.y + offset.y);

                    if (!grid.InBounds(sideA) || cost[grid.ToIndex(sideA)] == costData.Impassable)
                        continue;

                    if (!grid.InBounds(sideB) || cost[grid.ToIndex(sideB)] == costData.Impassable)
                        continue;
                }

                int ni = grid.ToIndex(nb);
                if (integration[ni] >= best) continue;

                best = integration[ni];
                bestOffset = offset;
            }

            if (bestOffset != Vector2Int.zero)
                directions[i] = new Vector2(bestOffset.x, bestOffset.y).normalized;
        }

        // Escape Directions
        for (int i = 0; i < directions.Length; i++)
        {
            if (directions[i] != Vector2.zero) continue;
            if (integration[i] == 0) continue; // goal cell

            Vector2Int cell = new Vector2Int(i % grid.width, i / grid.width);

            ushort best = Unreachable;
            Vector2Int bestOffset = Vector2Int.zero;

            for (int n = 0; n < 8; n++)
            {
                Vector2Int nb = cell + AllOffsets[n];
                if (!grid.InBounds(nb)) continue;

                int ni = grid.ToIndex(nb);
                if (integration[ni] >= best) continue;

                best = integration[ni];
                bestOffset = AllOffsets[n];
            }

            if (bestOffset != Vector2Int.zero)
                directions[i] = new Vector2(bestOffset.x, bestOffset.y).normalized;
        }
    }

    public Vector3 Sample(Vector3 worldPos)
    {
        if (directions == null || !grid.InBounds(worldPos))
            return Vector3.zero;

        Vector2 d = directions[grid.ToIndex(worldPos)];
        return new Vector3(d.x, 0f, d.y);
    }

    [InspectorButton]
    public void CentraliseGrid()
    {
        float x = (grid.width / 2f) * grid.cellSize;
        float z = (grid.height / 2f) * grid.cellSize;
        grid.origin = new Vector3(-x, grid.origin.y, -z);
    }

    #region Gizmos
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        DrawGrid();
        DrawCost();
        DrawIntegration();
        DrawIntegrationLabels();
        DrawDirections();
    }

    private void DrawGrid()
    {
        if (drawGrid)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);

            float w = grid.width * grid.cellSize;
            float h = grid.height * grid.cellSize;

            for (int x = 0; x <= grid.width; x++)
            {
                Vector3 a = grid.origin + new Vector3(x * grid.cellSize, 0f, 0f);
                Gizmos.DrawLine(a, a + new Vector3(0f, 0f, h));
            }

            for (int y = 0; y <= grid.height; y++)
            {
                Vector3 a = grid.origin + new Vector3(0f, 0f, y * grid.cellSize);
                Gizmos.DrawLine(a, a + new Vector3(w, 0f, 0f));
            }
        }
    }

    private void DrawCost()
    {
        if (cost != null && costData != null)
        {
            for (int i = 0; i < cost.Length; i++)
            {
                if (cost[i] != costData.Impassable)
                    continue;

                Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
                Gizmos.DrawCube(grid.CellCentre(i), new Vector3(grid.cellSize, 0.02f, grid.cellSize) * 0.9f);
            }
        }
    }

    private void DrawIntegration()
    {
        if (integration == null || !drawIntegration) 
            return;

        // find the max reachable value so the ramp scales to the field
        int max = 1;
        for (int i = 0; i < integration.Length; i++)
            if (integration[i] != Unreachable && integration[i] > max)
                max = integration[i];

        Vector3 size = new Vector3(grid.cellSize, 0.02f, grid.cellSize) * 0.9f;

        for (int i = 0; i < integration.Length; i++)
        {
            if (integration[i] == Unreachable) continue;

            float t = integration[i] / (float)max;
            Gizmos.color = new Color(t, 1f - t, 0f, 0.35f);   // green near goal → red far
            Gizmos.DrawCube(grid.CellCentre(i) + Vector3.up * 0.02f, size);
        }
    }

    private void DrawIntegrationLabels()
    {
        if (integration == null || !drawCostHandles)
            return;

        Camera cam = SceneView.currentDrawingSceneView?.camera;
        if (cam == null) return;

        GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < integration.Length; i++)
        {
            if (integration[i] == Unreachable) continue;

            Vector3 p = grid.CellCentre(i);
            if ((p - cam.transform.position).sqrMagnitude > 400f) continue;  // 20m cull

            Handles.Label(p + Vector3.up * 0.1f, integration[i].ToString(), style);
        }
    }

    private void DrawDirections()
    {
        if (directions == null || !drawDirections)
            return;

        for (int i = 0; i < directions.Length; i++)
        {
            if (directions[i] == Vector2.zero) continue;

            Vector3 p = grid.CellCentre(i);
            Vector3 d = new Vector3(directions[i].x, 0f, directions[i].y) * grid.cellSize * 0.4f;

            bool isImpassable = cost[i] == costData.Impassable;

            Gizmos.color = isImpassable ? Color.red : Color.yellow;
            Gizmos.DrawLine(p - d, p + d);
            Gizmos.DrawSphere(p + d, grid.cellSize * 0.08f);   // arrowhead
        }
    }
#endif
    #endregion
}