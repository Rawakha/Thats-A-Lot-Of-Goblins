using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public partial class EnemyGrid : MonoBehaviour
{
    public static EnemyGrid Instance;

    [Header("Grid")]
    [SerializeField] private Grid grid;
    [SerializeField] private int rebuildEveryNFrames = 3;

    private int[] cellStart;
    private int[] cellCount;
    private int[] entries;
    private Enemy[] enemyRefs;
    private int entryCount;

    public int RebuildInterval => rebuildEveryNFrames;

    public void Initialize(EnemyManager manager)
    {
        Utilities.CreateInstance(ref Instance, this);
    }

    public void Rebuild(List<Enemy> active)
    {
        int cells = grid.CellCount;

        if (cellStart == null || cellStart.Length != cells + 1)
        {
            cellStart = new int[cells + 1];
            cellCount = new int[cells];
        }

        if (enemyRefs == null || enemyRefs.Length < active.Count)
        {
            enemyRefs = new Enemy[Mathf.NextPowerOfTwo(active.Count)];
            entries = new int[enemyRefs.Length];
        }

        System.Array.Clear(cellCount, 0, cells);
        entryCount = 0;

        // pass 1: snapshot
        for (int i = 0; i < active.Count; i++)
        {
            Enemy e = active[i];
            if (e == null || e.state == EnemyState.Pooled || e.state == EnemyState.Dying)
                continue;

            Vector2Int cell = grid.ToCell(e.body.position);
            if (!grid.InBounds(cell)) continue;

            e.gridCell = grid.ToIndex(cell);
            enemyRefs[entryCount++] = e;
            cellCount[e.gridCell]++;
        }

        // pass 2: prefix sum
        int running = 0;
        for (int i = 0; i < cells; i++)
        {
            cellStart[i] = running;
            running += cellCount[i];
            cellCount[i] = 0;
        }
        cellStart[cells] = running;

        // pass 3: scatter
        for (int i = 0; i < entryCount; i++)
        {
            int cell = enemyRefs[i].gridCell;
            entries[cellStart[cell] + cellCount[cell]] = i;
            cellCount[cell]++;
        }
    }

    public int QueryRadius(Vector3 pos, float radius, List<Enemy> results)
    {
        results.Clear();

        Vector2Int centre = grid.ToCell(pos);
        int r = Mathf.CeilToInt(radius / grid.cellSize);
        float radiusSqr = radius * radius;

        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                Vector2Int c = centre + new Vector2Int(dx, dy);
                if (!grid.InBounds(c)) continue;

                int cell = grid.ToIndex(c);

                for (int i = cellStart[cell]; i < cellStart[cell + 1]; i++)
                {
                    Enemy e = enemyRefs[entries[i]];
                    if ((e.body.position - pos).sqrMagnitude <= radiusSqr)
                        results.Add(e);
                }
            }
        }

        return results.Count;
    }
}