using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(Utilities.ExecutionOrder.Singletons)]
public class TowerManager : MonoBehaviour
{
    public static TowerManager Instance;

    [SerializeField] private FlowField flowField;

    [Header("Gizmos")]
    [SerializeField] private bool drawOccupied = true;

    [Header("Testing")]
    [SerializeField] private TowerDefinition testTower;
    [SerializeField] private Vector2Int testCell;

    private List<Tower> towers = new List<Tower>(64);
    private bool[] terrainOccupied;
    private bool[] towerOccupied;

    public Grid Grid => flowField.Grid;

    private void Awake()
    {
        Utilities.CreateInstance(ref Instance, this);
        EnsureOccupancyArray();
    }

    [InspectorButton]
    public void TestPlace()
    {
        Place(testTower, testCell);
    }

    [InspectorButton]
    public void TestSell()
    {
        Sell(towers.Count > 0 ? towers[towers.Count - 1] : null);
    }

    public Tower Place(TowerDefinition def, Vector2Int origin)
    {
        if (def == null || def.prefab == null)
            return null;

        if (!IsFootprintFree(origin, def.size))
            return null;

        EnsureOccupancyArray();

        Vector3 worldPos = GetFootprintCenter(origin, def.size);
        Tower tower = Instantiate(def.prefab, worldPos, Quaternion.identity, transform);
        tower.Initialize(def, origin, towers.Count);        
        towers.Add(tower);

        SetFootprintOccupied(origin, def.size, true);
        flowField.SetCellsBlocked(origin, def.size, true);
        return tower;
    }

    public void Sell(Tower tower)
    {
        if (tower == null)
            return;

        SetFootprintOccupied(tower.GridPosition, tower.Definition.size, false);
        flowField.SetCellsBlocked(tower.GridPosition, tower.Definition.size, false);

        RemoveAtSwap(tower.ManagerIndex);
        Destroy(tower.gameObject);
    }

    private void EnsureOccupancyArray()
    {
        int cellCount = Grid.CellCount;

        if (towerOccupied == null || towerOccupied.Length != cellCount)
        {
            towerOccupied = new bool[cellCount];
        }

        if (terrainOccupied == null || terrainOccupied.Length != cellCount)
        {
            terrainOccupied = new bool[cellCount];

            // Bake Impassable
            for (int i = 0; i < cellCount; i++)
            {
                bool isImpassable = flowField.IsImpassable(i);
                terrainOccupied[i] = isImpassable;
            }
        }
    }

    public bool IsCellOccupied(int index)
    {
        if (!Grid.InBounds(index))
            return true;

        return towerOccupied[index] || terrainOccupied[index];
    }

    public bool IsFootprintFree(Vector2Int origin, Vector2Int size)
    {
        EnsureOccupancyArray();

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Vector2Int cell = new Vector2Int(origin.x + x, origin.y + y);
                if (!Grid.InBounds(cell))
                    return false;

                int index = Grid.ToIndex(cell);
                if (IsCellOccupied(index))
                    return false;
            }
        }

        return true;
    }

    private void SetFootprintOccupied(Vector2Int origin, Vector2Int size, bool value)
    {
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Vector2Int cell = new Vector2Int(origin.x + x, origin.y + y);

                if (Grid.InBounds(cell))
                {
                    int index = Grid.ToIndex(cell);
                    towerOccupied[index] = value;
                }
            }
        }
    }

    public Vector3 GetFootprintCenter(Vector2Int origin, Vector2Int size)
    {
        float x = Grid.origin.x + (origin.x + size.x * 0.5f) * Grid.cellSize;
        float z = Grid.origin.z + (origin.y + size.y * 0.5f) * Grid.cellSize;
        return new Vector3(x, Grid.origin.y, z);
    }

    private void RemoveAtSwap(int index)
    {
        int last = towers.Count - 1;

        if (index < 0 || index > last)
            return;

        if (index != last)
        {
            towers[index] = towers[last];
            towers[index].SetManagerIndex(index);
        }

        towers.RemoveAt(last);
    }

    private void OnDrawGizmos()
    {
        if (flowField == null)
            return;

        Grid g = Grid;

        if (drawOccupied && towerOccupied != null)
        {
            Vector3 size = new Vector3(g.cellSize, 0.04f, g.cellSize) * 0.9f;
            Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.3f);
            for (int i = 0; i < towerOccupied.Length; i++)
            {
                if (!towerOccupied[i]) continue;
                Gizmos.DrawCube(g.CellCentre(i) + Vector3.up * 0.04f, size);
            }
        }

        // Draw test cell
        int testCellIndex = g.ToIndex(testCell);
        Vector3 testCellCentre = g.CellCentre(testCellIndex);
        Gizmos.color = Color.cyan;
        Gizmos.DrawCube(testCellCentre + Vector3.up * 0.05f, new Vector3(g.cellSize, 0.05f, g.cellSize) * 0.9f);
    }
}