using UnityEngine;

// Counts things per square of ground, to find packed spots (Horde_DebrisBudget's per-square limits). Squares hash into a
// fixed set of counters, so it works on any size of map; a rare collision only makes a square look a bit fuller.
//
// GC: one array, allocated once. No subscriptions.
public sealed class Horde_CrowdGrid
{
    private const int Cells = 4096;     // a power of two

    private readonly int[] counts = new int[Cells];
    private readonly float size;

    public Horde_CrowdGrid(float cellSize)
    {
        size = Mathf.Max(cellSize, 0.01f);
    }

    public int CellAt(Vector3 at)
    {
        int x = Mathf.FloorToInt(at.x / size);
        int z = Mathf.FloorToInt(at.z / size);
        return (x * 73856093 ^ z * 19349663) & (Cells - 1);
    }

    public void Add(int cell) => counts[cell]++;
    public void Remove(int cell) => counts[cell]--;
    public int Count(int cell) => counts[cell];
}
