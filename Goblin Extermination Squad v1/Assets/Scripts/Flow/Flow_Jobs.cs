using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling.LowLevel.Unsafe;

// The flow field solve, in Burst jobs. One solve is: Init (seed the queue) -> Slice, repeated across frames until the
// queue is empty -> Directions (each cell points at its cheapest neighbour). Flow_System chains Slice + Directions
// every frame; Directions does nothing until the integration has finished, so the frame it finishes, it's done.
//
// Integration is Dijkstra on the grid with a binary heap: 8 neighbours, diagonals cost 1.4x (integer 10 / 14 times
// the cell's cost), no cutting past blocked corners. A heap rather than a bucket queue so local repairs, which seed
// cells at all sorts of distances, are just as correct as full solves.
//
// Every job adds the time it ran to Timing[0], read by Flow_System for the stats.
// [Unity.Collections.ReadOnly] is written out in full: the project also has an inspector ReadOnly attribute.
//
// GC: none. Everything is in native arrays owned by Flow_System's solver slots.

public static class Flow_Solve
{
    public const int Unreached = int.MaxValue;
    public const int StraightCost = 10;
    public const int DiagonalCost = 14;

    // Cost of stepping from (x, y) in a direction, or -1 if the step leaves the grid, enters a blocked cell or cuts a
    // blocked corner. Cost is of the cell entered.
    public static int Step(in NativeArray<byte> cost, int width, int height, int x, int y, int direction, out int neighbour)
    {
        Flow_Direction.Offset(direction, out int dx, out int dy);
        int nx = x + dx;
        int ny = y + dy;
        neighbour = -1;

        if (nx < 0 || ny < 0 || nx >= width || ny >= height)
            return -1;

        neighbour = ny * width + nx;
        byte c = cost[neighbour];

        if (c == Flow_Map.Blocked)
            return -1;

        if (dx == 0 || dy == 0)
            return c * StraightCost;

        // Diagonal: both sides of the corner must be open, so nothing squeezes between two touching walls
        if (cost[y * width + nx] == Flow_Map.Blocked || cost[ny * width + x] == Flow_Map.Blocked)
            return -1;

        return c * DiagonalCost;
    }

    // Heap entries pack distance (high) and cell (low), so comparing longs compares distances
    public static long Key(int distance, int cell) => ((long)distance << 32) | (uint)cell;

    public static void Push(ref NativeArray<long> heap, ref int count, long key)
    {
        int i = count++;

        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            long above = heap[parent];

            if (above <= key)
                break;

            heap[i] = above;
            i = parent;
        }

        heap[i] = key;
    }

    public static long Pop(ref NativeArray<long> heap, ref int count)
    {
        long top = heap[0];
        long last = heap[--count];
        int i = 0;
        int half = count >> 1;

        while (i < half)
        {
            int child = 2 * i + 1;
            long smallest = heap[child];
            int right = child + 1;

            if (right < count && heap[right] < smallest)
            {
                child = right;
                smallest = heap[right];
            }

            if (last <= smallest)
                break;

            heap[i] = smallest;
            i = child;
        }

        if (count > 0)
            heap[i] = last;

        return top;
    }
}

// Seeds a solve. Full: every distance unknown, goal cells at 0. Repair: Distance already holds the field's last
// result; the changed cells (which only got cheaper) are given their new best distance from a neighbour and queued.
[BurstCompile]
public struct Flow_InitJob : IJob
{
    [Unity.Collections.ReadOnly] public NativeArray<byte> Cost;
    [Unity.Collections.ReadOnly] public NativeArray<int> Seeds;
    public NativeArray<int> Distance;
    public NativeArray<long> Heap;
    public NativeArray<int> State;      // [0] heap count, [1] cells settled, [2] integration finished
    public NativeArray<long> Timing;

    public int SeedCount;
    public bool Repair;
    public int Width;
    public int Height;

    public void Execute()
    {
        long start = ProfilerUnsafeUtility.Timestamp;
        int count = 0;

        if (!Repair)
        {
            for (int i = 0; i < Distance.Length; i++)
                Distance[i] = Flow_Solve.Unreached;

            for (int s = 0; s < SeedCount; s++)
            {
                int cell = Seeds[s];

                if (Distance[cell] == 0)
                    continue;

                Distance[cell] = 0;
                Flow_Solve.Push(ref Heap, ref count, Flow_Solve.Key(0, cell));
            }
        }
        else
        {
            for (int s = 0; s < SeedCount; s++)
            {
                int cell = Seeds[s];
                int best = Distance[cell];
                int x = cell % Width;
                int y = cell / Width;

                // Reached more cheaply from a neighbour now? (stepping back from the neighbour into this cell)
                for (int d = 0; d < 8; d++)
                {
                    Flow_Direction.Offset(d, out int dx, out int dy);
                    int nx = x + dx;
                    int ny = y + dy;

                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                        continue;

                    int from = ny * Width + nx;

                    if (Distance[from] == Flow_Solve.Unreached)
                        continue;

                    int step = Flow_Solve.Step(Cost, Width, Height, nx, ny, (d + 4) & 7, out int into);

                    if (step >= 0 && into == cell && Distance[from] + step < best)
                        best = Distance[from] + step;
                }

                if (best < Distance[cell])
                {
                    Distance[cell] = best;
                    Flow_Solve.Push(ref Heap, ref count, Flow_Solve.Key(best, cell));
                }
            }
        }

        State[0] = count;
        State[1] = 0;
        State[2] = count == 0 ? 1 : 0;
        Timing[0] += ProfilerUnsafeUtility.Timestamp - start;
    }
}

// Settles up to Budget cells (0 or less: all of them), carrying on from where the last slice stopped
[BurstCompile]
public struct Flow_SliceJob : IJob
{
    [Unity.Collections.ReadOnly] public NativeArray<byte> Cost;
    public NativeArray<int> Distance;
    public NativeArray<long> Heap;
    public NativeArray<int> State;
    public NativeArray<long> Timing;

    public int Budget;
    public int Width;
    public int Height;

    public void Execute()
    {
        if (State[2] != 0)
            return;

        long start = ProfilerUnsafeUtility.Timestamp;
        int count = State[0];
        int settled = 0;

        while (count > 0 && (Budget <= 0 || settled < Budget))
        {
            long key = Flow_Solve.Pop(ref Heap, ref count);
            int distance = (int)(key >> 32);
            int cell = (int)(key & 0xFFFFFFFFL);

            // A cell is queued again each time it improves; only its latest entry counts
            if (distance != Distance[cell])
                continue;

            settled++;
            int x = cell % Width;
            int y = cell / Width;

            for (int d = 0; d < 8; d++)
            {
                int step = Flow_Solve.Step(Cost, Width, Height, x, y, d, out int neighbour);

                if (step < 0)
                    continue;

                int through = distance + step;

                if (through < Distance[neighbour])
                {
                    Distance[neighbour] = through;
                    Flow_Solve.Push(ref Heap, ref count, Flow_Solve.Key(through, neighbour));
                }
            }
        }

        State[0] = count;
        State[1] += settled;

        if (count == 0)
            State[2] = 1;

        Timing[0] += ProfilerUnsafeUtility.Timestamp - start;
    }
}

// Points every cell at its cheapest neighbour, once the integration has finished. Blocked cells point at their
// cheapest open neighbour, so anything pushed into a wall finds its way out.
// One job rather than a parallel one: it's a single fast pass (well under a millisecond in Burst), several fields'
// solves already run side by side, and it keeps the timing exact.
[BurstCompile]
public struct Flow_DirectionsJob : IJob
{
    [Unity.Collections.ReadOnly] public NativeArray<byte> Cost;
    [Unity.Collections.ReadOnly] public NativeArray<int> Distance;
    [Unity.Collections.ReadOnly] public NativeArray<int> State;
    public NativeArray<byte> Directions;
    public NativeArray<long> Timing;

    public int Width;
    public int Height;

    public void Execute()
    {
        if (State[2] == 0)
            return;

        long start = ProfilerUnsafeUtility.Timestamp;

        for (int cell = 0; cell < Directions.Length; cell++)
        {
            int own = Distance[cell];

            if (own == 0)
            {
                Directions[cell] = Flow_Direction.Goal;
                continue;
            }

            int x = cell % Width;
            int y = cell / Width;
            bool blocked = Cost[cell] == Flow_Map.Blocked;
            int best = blocked ? Flow_Solve.Unreached : own;
            byte direction = Flow_Direction.None;

            for (int d = 0; d < 8; d++)
            {
                int neighbour;

                if (blocked)
                {
                    // Escaping a wall: any open neighbour, corners or not
                    Flow_Direction.Offset(d, out int dx, out int dy);
                    int nx = x + dx;
                    int ny = y + dy;

                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                        continue;

                    neighbour = ny * Width + nx;

                    if (Cost[neighbour] == Flow_Map.Blocked)
                        continue;
                }
                else if (Flow_Solve.Step(Cost, Width, Height, x, y, d, out neighbour) < 0)
                {
                    continue;
                }

                if (Distance[neighbour] < best)
                {
                    best = Distance[neighbour];
                    direction = (byte)d;
                }
            }

            Directions[cell] = direction;
        }

        Timing[0] += ProfilerUnsafeUtility.Timestamp - start;
    }
}
