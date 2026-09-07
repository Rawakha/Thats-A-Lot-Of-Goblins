using System.Collections.Generic;
using UnityEngine;

// Gizmos
public partial class EnemyGrid
{
    [Header("Gizmos")]
    [SerializeField] private bool drawGrid = false;
    [SerializeField] private bool drawOccupied = false;
    [SerializeField] private bool drawCounts = false;

    private void OnDrawGizmos()
    {
        DrawGridLines();
        DrawOccupiedCells();
#if UNITY_EDITOR
        DrawQueries();
#endif
    }

    private void DrawGridLines()
    {
        if (!drawGrid)
            return;

        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);

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

    private void DrawOccupiedCells()
    {
        if (!drawOccupied || cellStart == null) return;

        Vector3 size = new Vector3(grid.cellSize, 0.02f, grid.cellSize) * 0.9f;

        // find max occupancy for the colour ramp
        int max = 1;
        for (int i = 0; i < grid.CellCount; i++)
        {
            int count = cellStart[i + 1] - cellStart[i];
            if (count > max) max = count;
        }

        for (int i = 0; i < grid.CellCount; i++)
        {
            int count = cellStart[i + 1] - cellStart[i];
            if (count == 0) continue;

            float t = count / (float)max;
            Gizmos.color = new Color(t, 1f - t, 0f, 0.35f);

            Vector3 centre = grid.CellCentre(i);
            Gizmos.DrawCube(centre + Vector3.up * 0.02f, size);

            if (drawCounts)
                UnityEditor.Handles.Label(centre + Vector3.up * 0.1f, count.ToString());
        }
    }

    public void DebugDrawQuery(Vector3 pos, float radius, float duration = 0.2f)
    {
#if UNITY_EDITOR
        debugQueries.Add(new DebugQuery
        {
            position = pos,
            radius = radius,
            expiry = Time.time + duration
        });
#endif
    }

#if UNITY_EDITOR
    private struct DebugQuery
    {
        public Vector3 position;
        public float radius;
        public float expiry;
    }

    private readonly List<DebugQuery> debugQueries = new(16);

    private void DrawQueries()
    {
        for (int i = debugQueries.Count - 1; i >= 0; i--)
        {
            DebugQuery q = debugQueries[i];

            if (Time.time > q.expiry)
            {
                debugQueries.RemoveAt(i);
                continue;
            }

            // the query radius
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.25f);
            Gizmos.DrawWireSphere(q.position, q.radius);

            Vector2Int centre = grid.WorldToCell(q.position);
            int r = Mathf.CeilToInt(q.radius / grid.cellSize);
            Vector3 size = new Vector3(grid.cellSize, 0.02f, grid.cellSize) * 0.85f;

            float half = grid.cellSize * 0.5f;

            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    Vector2Int c = centre + new Vector2Int(dx, dy);
                    if (!grid.InBounds(c)) continue;

                    int cell = grid.ToIndex(c);
                    Vector3 cellCentre = grid.CellCentre(cell);

                    // closest point on the cell's AABB to the query centre
                    float cx = Mathf.Clamp(q.position.x, cellCentre.x - half, cellCentre.x + half);
                    float cz = Mathf.Clamp(q.position.z, cellCentre.z - half, cellCentre.z + half);

                    float dxs = q.position.x - cx;
                    float dzs = q.position.z - cz;
                    bool inRadius = (dxs * dxs + dzs * dzs) <= q.radius * q.radius;

                    bool occupied = cellStart != null && cellStart[cell + 1] > cellStart[cell];

                    Gizmos.color = GetQueryCellColor(inRadius, occupied);
                    Gizmos.DrawCube(cellCentre + Vector3.up * 0.04f, size);
                }
        }
    }

    private Color GetQueryCellColor(bool inRadius, bool occupied)
    {
        if (inRadius && occupied) return new Color(1f, 0f, 0f, 0.45f);      // red — scanned, has enemies
        if (inRadius) return new Color(0f, 0.6f, 1f, 0.25f);    // blue — scanned, empty
        if (occupied) return new Color(1f, 0.6f, 0f, 0.25f);    // orange — wasted, has enemies
        return new Color(0.5f, 0.5f, 0.5f, 0.12f);                            // grey — wasted, empty
    }
#endif
}