using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Every flow field in the game, from one place.
//
//   Map: obstacles are baked in the editor (Bake Obstacles) into a Flow_Map asset and loaded once. Runtime changes
//     (rubble, placed buildings) go in a cost overlay through SetCost; fields re-solve only where they're affected.
//   Fields: a fixed pool of slots. A system asks for one (RequestField), gives it one or more goal cells (SetGoal /
//     SetGoals; several goals lead each cell to the nearest), and gets a handle. Every field's directions live in one
//     shared array, Directions: field slot * CellCount + cell, one byte each (Flow_Direction). The horde job reads
//     that one array with a per-goblin field offset.
//   Solving: Burst jobs (Flow_Jobs) in solver slots with their own copies of everything, so readers never wait.
//     A solve is spread over frames (Cells Per Frame), finished results are copied into Directions between frames,
//     only a few solves start per frame, moving goals re-solve at most every Min Interval, and when the overlay only
//     makes cells cheaper (a building turning to rubble) fields are repaired from the change instead of re-solved.
//
// Readers: a job reading Directions must be passed to AddReader, so results are never copied in under it.
//
// GC: none per frame. Native arrays are allocated once in Awake for the grid, fields and solvers. Stats strings are
// set once per field. One UpdateManager subscription (OnLateUpdate).
[DefaultExecutionOrder(Utilities.ExecutionOrder.DependantSingletons)]
public class Flow_System : MonoBehaviour
{
    [Serializable]
    public struct SolveStats
    {
        public string field;
        public string kind;         // Full or Repair
        [Tooltip("Frames the solve was spread over.")]
        public int frames;
        public int cellsSettled;
        [Tooltip("Total job time across all its frames.")]
        public float totalMs;
        public float averageMs;
        public float topMs;
        public float bottomMs;
    }

    private sealed class Field
    {
        public bool Used;
        public int Version;
        public string Name;
        public readonly List<int> Goals = new List<int>(8);
        public NativeArray<int> Distance;   // last finished result, main thread only
        public bool HasResult;
        public bool DirtyFull;
        public bool DirtyRepair;
        public RectInt RepairArea;
        public float MinInterval;
        public float LastStart = float.NegativeInfinity;
        public int Solver = -1;
    }

    private sealed class Solver
    {
        public NativeArray<byte> Cost;
        public NativeArray<int> Distance;
        public NativeArray<long> Heap;
        public NativeArray<int> State;
        public NativeArray<int> Seeds;
        public NativeArray<byte> Directions;
        public NativeArray<long> Timing;
        public JobHandle Handle;
        public int Field = -1;
        public int FieldVersion;
        public bool Repair;
        public bool Running;

        // This solve's stats so far
        public int Frames;
        public double TotalMs;
        public double TopMs;
        public double BottomMs;
    }

    [Header("Map")]
    [SerializeField] private Flow_Map map;

    [Header("Bake")]
    [Tooltip("The grid Bake Obstacles writes into the map. Cover the whole level.")]
    [SerializeField] private Flow_Grid bakeGrid = new Flow_Grid
    {
        origin = new Vector3(-150f, 0f, -150f),
        cellSize = 2f,
        width = 150,
        height = 150
    };

    [Tooltip("What goblins can't walk through: buildings, walls, rocks.")]
    [SerializeField] private LayerMask obstacleLayers;

    [Tooltip("Height above the grid that's checked for obstacles. Low things (fences) under it still block.")]
    [SerializeField, Min(0.1f)] private float obstacleHeight = 3f;

    [Tooltip("Skips this much above the grid, so the ground itself isn't an obstacle.")]
    [SerializeField, Min(0f)] private float groundClearance = 0.2f;

    [Tooltip("How much of a cell the obstacle check covers. Lower lets goblins squeeze through narrower gaps.")]
    [SerializeField, Range(0.1f, 1f)] private float cellFill = 0.8f;

    [SerializeField, Range(1, 254)] private int openCost = 1;

    [Tooltip("Cost next to obstacles, so paths keep off walls instead of scraping along them.")]
    [SerializeField, Range(1, 254)] private int nearWallCost = 3;

    [Header("Fields")]
    [SerializeField, Min(1)] private int maxFields = 16;

    [Tooltip("Solves running at once. Each has its own working memory (about 10 bytes x 10 per cell).")]
    [SerializeField, Min(1)] private int solverSlots = 2;

    [Tooltip("Most solves started per frame; the rest wait their turn.")]
    [SerializeField, Min(1)] private int maxStartsPerFrame = 1;

    [Tooltip("Cells finished per solve per frame, so a solve spreads over frames. 0 lets each solve run as one job, " +
             "however many frames it takes. Lower for steadier frames on weak CPUs.")]
    [SerializeField, Min(0)] private int cellsPerFrame = 8000;

    [Header("Stats")]
    [SerializeField, ReadOnly] private float obstacleBakeMs;
    [SerializeField, ReadOnly] private int blockedCells;
    [Tooltip("The last solve of each field: job time per frame, and how many frames it was spread over.")]
    [SerializeField, ReadOnly] private SolveStats[] lastSolves = new SolveStats[0];

    [Header("Gizmos")]
    [SerializeField] private bool drawObstacles = true;
    [SerializeField] private bool drawField = true;
    [Tooltip("Field slot to draw in play mode, or Flow_Goal in the edit-mode preview. Selecting a Flow_Goal draws its own.")]
    [SerializeField, Min(0)] private int drawFieldIndex;
    [Tooltip("Colour cells by distance to the goal as well as arrows.")]
    [SerializeField] private bool drawDistance;
    [Tooltip("Only cells this close to the Scene view camera's focus are drawn.")]
    [SerializeField, Min(4f)] private float drawRadius = 50f;

    private Flow_Grid grid;
    private int cellCount;
    private byte[] baked;
    private byte[] overlay;
    private NativeArray<byte> cost;
    private NativeArray<byte> directions;

    private Field[] fields;
    private Solver[] solvers;
    private JobHandle readers;
    private int nextStart;
    private double ticksToMs;
    private bool ready;

    public static Flow_System Instance { get; private set; }

    public bool IsReady => ready;
    public Flow_Grid Grid => grid;
    public int CellCount => cellCount;

    // Every field's directions, field slot * CellCount + cell. Read it from jobs passed to AddReader.
    public NativeArray<byte> Directions => directions;

    #region Lifetime
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"{name}: A second Flow_System was found, this one is disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (map == null || !map.IsBaked)
        {
            Debug.LogError($"{name}: Flow_System has no baked Flow_Map. Press Bake Obstacles.", this);
            enabled = false;
            return;
        }

        if (!map.grid.Matches(bakeGrid))
            Debug.LogWarning($"{name}: the bake grid has changed since the map was baked. Using the map's; bake again " +
                             "to use the new one.", this);

        grid = map.grid;
        cellCount = grid.CellCount;
        baked = map.costs;
        overlay = new byte[cellCount];

        cost = new NativeArray<byte>(baked, Allocator.Persistent);
        directions = new NativeArray<byte>(maxFields * cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        Fill(directions, 0, directions.Length, Flow_Direction.None);

        fields = new Field[maxFields];

        for (int i = 0; i < maxFields; i++)
            fields[i] = new Field { Distance = new NativeArray<int>(cellCount, Allocator.Persistent) };

        solvers = new Solver[solverSlots];

        for (int i = 0; i < solverSlots; i++)
            solvers[i] = CreateSolver(cellCount, Allocator.Persistent);

        lastSolves = new SolveStats[maxFields];
        ticksToMs = TicksToMs();
        ready = true;
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance == null)
        {
            Debug.LogError($"{name}: No UpdateManager in the scene, flow fields won't solve.", this);
            return;
        }

        UpdateManager.Instance.OnLateUpdate += Tick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (!ready)
            return;

        readers.Complete();

        for (int i = 0; i < solvers.Length; i++)
        {
            solvers[i].Handle.Complete();
            DisposeSolver(solvers[i]);
        }

        for (int i = 0; i < fields.Length; i++)
            fields[i].Distance.Dispose();

        cost.Dispose();
        directions.Dispose();
        ready = false;
    }
    #endregion

    #region Fields
    // Takes a free field slot. Min Interval caps how often it re-solves when its goals keep moving.
    public Flow_FieldHandle RequestField(string fieldName, float minInterval = 0f)
    {
        if (!ready)
            return Flow_FieldHandle.Invalid;

        for (int i = 0; i < fields.Length; i++)
        {
            Field field = fields[i];

            if (field.Used || field.Solver >= 0)
                continue;

            field.Used = true;
            field.Version++;
            field.Name = string.IsNullOrEmpty(fieldName) ? $"Field {i}" : fieldName;
            field.Goals.Clear();
            field.HasResult = false;
            field.DirtyFull = false;
            field.DirtyRepair = false;
            field.MinInterval = minInterval;
            field.LastStart = float.NegativeInfinity;
            lastSolves[i] = new SolveStats { field = field.Name };
            return new Flow_FieldHandle(i, field.Version);
        }

        Debug.LogWarning($"{name}: All {maxFields} flow fields are in use; raise Max Fields.", this);
        return Flow_FieldHandle.Invalid;
    }

    public void Release(Flow_FieldHandle handle)
    {
        if (!TryGet(handle, out Field field))
            return;

        field.Used = false;
        field.Version++;
        field.HasResult = false;
        field.Goals.Clear();
        lastSolves[handle.Index] = default;

        // Readers of a released field see "no direction". A solve still running is thrown away when it ends.
        readers.Complete();
        Fill(directions, handle.Index * cellCount, cellCount, Flow_Direction.None);
    }

    public bool IsValid(Flow_FieldHandle handle) => TryGet(handle, out _);

    // True once the field has a finished solve to read. Until then its directions are all None.
    public bool HasResult(Flow_FieldHandle handle) => TryGet(handle, out Field field) && field.HasResult;

    // Where this field starts in Directions
    public int FieldOffset(Flow_FieldHandle handle) => handle.Index * cellCount;

    public void SetGoal(Flow_FieldHandle handle, Vector3 world)
    {
        if (!TryGet(handle, out Field field))
            return;

        int cell = grid.CellIndex(world);

        if (field.Goals.Count == 1 && field.Goals[0] == cell)
            return;

        field.Goals.Clear();

        if (cell >= 0)
            field.Goals.Add(cell);

        field.DirtyFull = true;
    }

    // Several goals: every cell leads to the nearest of them
    public void SetGoals(Flow_FieldHandle handle, IReadOnlyList<Vector3> world)
    {
        if (!TryGet(handle, out Field field))
            return;

        field.Goals.Clear();

        for (int i = 0; i < world.Count; i++)
        {
            int cell = grid.CellIndex(world[i]);

            if (cell >= 0 && !field.Goals.Contains(cell))
                field.Goals.Add(cell);
        }

        field.DirtyFull = true;
    }

    public void AddGoal(Flow_FieldHandle handle, Vector3 world)
    {
        if (!TryGet(handle, out Field field))
            return;

        int cell = grid.CellIndex(world);

        if (cell < 0 || field.Goals.Contains(cell))
            return;

        field.Goals.Add(cell);
        field.DirtyFull = true;
    }

    // True if the cell under this world point is an obstacle (baked, or set blocked with SetCost). Off the grid counts
    // as blocked. Reads the managed copies, so it's safe beside running solves; no allocation.
    public bool IsBlocked(Vector3 world)
    {
        int cell = ready ? grid.CellIndex(world) : -1;

        if (cell < 0)
            return true;

        byte value = overlay[cell] != 0 ? overlay[cell] : baked[cell];
        return value == Flow_Map.Blocked;
    }

    // Main-thread lookup, for one-off queries. Jobs should read Directions directly.
    public byte DirectionAt(Flow_FieldHandle handle, Vector3 world)
    {
        int cell = grid.CellIndex(world);
        return cell >= 0 && TryGet(handle, out _) ? directions[handle.Index * cellCount + cell] : Flow_Direction.None;
    }

    // A job that reads Directions. It's completed before new results are copied in.
    public void AddReader(JobHandle reader)
    {
        readers = JobHandle.CombineDependencies(readers, reader);
    }

    private bool TryGet(Flow_FieldHandle handle, out Field field)
    {
        field = null;

        if (!ready || handle.Version == 0 || handle.Index < 0 || handle.Index >= fields.Length)
            return false;

        field = fields[handle.Index];
        return field.Used && field.Version == handle.Version;
    }
    #endregion

    #region Cost Overlay
    // Overrides the cost of every cell in a world box (x and z): Flow_Map.Blocked to block, 1-254 to make it walkable
    // at that cost, 0 to go back to the baked cost. Fields re-solve: repaired from the change if cells only got
    // cheaper, solved fully if any got dearer.
    public void SetCost(Vector3 centre, Vector3 size, byte newCost)
    {
        if (!ready)
            return;

        Vector2Int min = grid.CellCoords(centre - size * 0.5f);
        Vector2Int max = grid.CellCoords(centre + size * 0.5f);
        min = Vector2Int.Max(min, Vector2Int.zero);
        max = Vector2Int.Min(max, new Vector2Int(grid.width - 1, grid.height - 1));

        if (min.x > max.x || min.y > max.y)
            return;

        bool changed = false;
        bool dearer = false;

        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                int cell = y * grid.width + x;
                byte before = cost[cell];
                overlay[cell] = newCost;
                byte after = newCost != 0 ? newCost : baked[cell];

                if (after == before)
                    continue;

                cost[cell] = after;
                changed = true;
                dearer |= after > before;
            }
        }

        if (!changed)
            return;

        RectInt area = new RectInt(min, max - min + Vector2Int.one);

        for (int i = 0; i < fields.Length; i++)
        {
            Field field = fields[i];

            if (!field.Used)
                continue;

            // No result yet, or anything got dearer: a repair can't fix that, solve it all again
            if (dearer || !field.HasResult || field.DirtyFull)
            {
                field.DirtyFull = true;
                field.DirtyRepair = false;
                continue;
            }

            field.RepairArea = field.DirtyRepair ? Union(field.RepairArea, area) : area;
            field.DirtyRepair = true;
        }
    }
    #endregion

    #region Solving
    private void Tick()
    {
        if (!ready)
            return;

        float time = Time.time;

        // Finished slices: record the frame, then either finish the solve or carry it on
        for (int s = 0; s < solvers.Length; s++)
        {
            Solver solver = solvers[s];

            if (!solver.Running || !solver.Handle.IsCompleted)
                continue;

            solver.Handle.Complete();
            RecordFrame(solver, ticksToMs);

            if (solver.State[2] != 0)
                Finish(solver);
            else
                ScheduleSlice(solver, default);
        }

        // New solves, a few per frame, round-robin so one busy field can't starve the rest
        int starts = 0;

        for (int n = 0; n < fields.Length && starts < maxStartsPerFrame; n++)
        {
            int index = (nextStart + n) % fields.Length;
            Field field = fields[index];

            if (!field.Used || field.Solver >= 0 || (!field.DirtyFull && !field.DirtyRepair))
                continue;

            if (time - field.LastStart < field.MinInterval)
                continue;

            int free = FreeSolver();

            if (free < 0)
                break;

            StartSolve(index, free, time);
            starts++;
            nextStart = (index + 1) % fields.Length;
        }

        JobHandle.ScheduleBatchedJobs();
    }

    private void StartSolve(int index, int solverIndex, float time)
    {
        Solver solver = solvers[solverIndex];
        Field field = fields[index];
        bool repair = field.DirtyRepair && !field.DirtyFull && field.HasResult;
        int seedCount = 0;

        NativeArray<byte>.Copy(cost, solver.Cost);

        if (repair)
        {
            NativeArray<int>.Copy(field.Distance, solver.Distance);
            RectInt area = field.RepairArea;

            for (int y = area.yMin; y < area.yMax; y++)
            for (int x = area.xMin; x < area.xMax; x++)
                solver.Seeds[seedCount++] = y * grid.width + x;
        }
        else
        {
            for (int i = 0; i < field.Goals.Count; i++)
                solver.Seeds[seedCount++] = field.Goals[i];
        }

        field.DirtyFull = false;
        field.DirtyRepair = false;
        field.LastStart = time;
        field.Solver = solverIndex;

        solver.Field = index;
        solver.FieldVersion = field.Version;
        solver.Repair = repair;
        solver.Running = true;
        solver.Frames = 0;
        solver.TotalMs = 0;
        solver.TopMs = 0;
        solver.BottomMs = double.MaxValue;
        solver.Timing[0] = 0;

        JobHandle init = new Flow_InitJob
        {
            Cost = solver.Cost,
            Seeds = solver.Seeds,
            Distance = solver.Distance,
            Heap = solver.Heap,
            State = solver.State,
            Timing = solver.Timing,
            SeedCount = seedCount,
            Repair = repair,
            Width = grid.width,
            Height = grid.height
        }.Schedule();

        ScheduleSlice(solver, init);
    }

    // One frame of work: a slice of the integration, then directions if that finished it
    private void ScheduleSlice(Solver solver, JobHandle dependency)
    {
        solver.Handle = ScheduleSliceJobs(solver, grid, cellsPerFrame, dependency);
    }

    private static JobHandle ScheduleSliceJobs(Solver solver, Flow_Grid grid, int budget, JobHandle dependency)
    {
        JobHandle slice = new Flow_SliceJob
        {
            Cost = solver.Cost,
            Distance = solver.Distance,
            Heap = solver.Heap,
            State = solver.State,
            Timing = solver.Timing,
            Budget = budget,
            Width = grid.width,
            Height = grid.height
        }.Schedule(dependency);

        return new Flow_DirectionsJob
        {
            Cost = solver.Cost,
            Distance = solver.Distance,
            State = solver.State,
            Directions = solver.Directions,
            Timing = solver.Timing,
            Width = grid.width,
            Height = grid.height
        }.Schedule(slice);
    }

    // Copies the result in for readers, if the field still wants it
    private void Finish(Solver solver)
    {
        Field field = fields[solver.Field];
        field.Solver = -1;
        solver.Running = false;

        if (field.Used && field.Version == solver.FieldVersion)
        {
            readers.Complete();
            readers = default;

            NativeArray<byte>.Copy(solver.Directions, 0, directions, solver.Field * cellCount, cellCount);
            NativeArray<int>.Copy(solver.Distance, field.Distance);
            field.HasResult = true;

            lastSolves[solver.Field] = Stats(field.Name, solver);
        }

        solver.Field = -1;
    }

    private int FreeSolver()
    {
        for (int i = 0; i < solvers.Length; i++)
        {
            if (!solvers[i].Running)
                return i;
        }

        return -1;
    }

    // Adds the frame's job time (summed by the jobs) to the solve's stats
    private static void RecordFrame(Solver solver, double toMs)
    {
        double ms = solver.Timing[0] * toMs;
        solver.Timing[0] = 0;
        solver.Frames++;
        solver.TotalMs += ms;
        solver.TopMs = Math.Max(solver.TopMs, ms);
        solver.BottomMs = Math.Min(solver.BottomMs, ms);
    }

    private static SolveStats Stats(string fieldName, Solver solver)
    {
        return new SolveStats
        {
            field = fieldName,
            kind = solver.Repair ? "Repair" : "Full",
            frames = solver.Frames,
            cellsSettled = solver.State[1],
            totalMs = (float)solver.TotalMs,
            averageMs = solver.Frames > 0 ? (float)(solver.TotalMs / solver.Frames) : 0f,
            topMs = (float)solver.TopMs,
            bottomMs = solver.Frames > 0 ? (float)solver.BottomMs : 0f
        };
    }

    private static Solver CreateSolver(int cells, Allocator allocator)
    {
        return new Solver
        {
            Cost = new NativeArray<byte>(cells, allocator, NativeArrayOptions.UninitializedMemory),
            Distance = new NativeArray<int>(cells, allocator, NativeArrayOptions.UninitializedMemory),

            // Each cell is settled once and relaxes at most 8 neighbours, plus up to one seed each
            Heap = new NativeArray<long>(cells * 9 + 8, allocator, NativeArrayOptions.UninitializedMemory),
            State = new NativeArray<int>(3, allocator),
            Seeds = new NativeArray<int>(cells, allocator, NativeArrayOptions.UninitializedMemory),
            Directions = new NativeArray<byte>(cells, allocator, NativeArrayOptions.UninitializedMemory),
            Timing = new NativeArray<long>(1, allocator)
        };
    }

    private static void DisposeSolver(Solver solver)
    {
        solver.Cost.Dispose();
        solver.Distance.Dispose();
        solver.Heap.Dispose();
        solver.State.Dispose();
        solver.Seeds.Dispose();
        solver.Directions.Dispose();
        solver.Timing.Dispose();
    }

    private static double TicksToMs()
    {
        ProfilerUnsafeUtility.TimestampConversionRatio ratio = ProfilerUnsafeUtility.TimestampToNanosecondsConversionRatio;
        return (double)ratio.Numerator / ratio.Denominator / 1000000.0;
    }

    private static void Fill(NativeArray<byte> array, int start, int length, byte value)
    {
        for (int i = start; i < start + length; i++)
            array[i] = value;
    }

    private static RectInt Union(RectInt a, RectInt b)
    {
        Vector2Int min = Vector2Int.Min(a.min, b.min);
        Vector2Int max = Vector2Int.Max(a.max, b.max);
        return new RectInt(min, max - min);
    }
    #endregion

#if UNITY_EDITOR
    #region Editor
    // Edit-mode preview of every Flow_Goal's field, for the gizmos. Not saved; re-run after moving goals or baking.
    [NonSerialized] private readonly List<byte[]> previewDirections = new List<byte[]>();
    [NonSerialized] private readonly List<int[]> previewDistances = new List<int[]>();
    [NonSerialized] private readonly List<Flow_Goal> previewGoals = new List<Flow_Goal>();

    // Checks every cell of the bake grid for obstacles and writes the costs into the map (making one if needed)
    [InspectorButton("Bake Obstacles")]
    private void BakeObstacles()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning($"{name}: Bake Obstacles in edit mode; the running game uses the map it loaded.", this);
            return;
        }

        if (map == null)
            map = CreateMap();

        if (map == null)
            return;

        // Edit-mode physics only sees moved objects once transforms are synced
        Physics.SyncTransforms();

        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        Flow_Grid g = bakeGrid;
        int count = g.CellCount;
        byte[] costs = new byte[count];
        float half = g.cellSize * cellFill * 0.5f;
        Vector3 halfExtents = new Vector3(half, obstacleHeight * 0.5f, half);
        int blocked = 0;

        for (int i = 0; i < count; i++)
        {
            Vector3 centre = g.CellCentre(i) + Vector3.up * (groundClearance + obstacleHeight * 0.5f);
            bool hit = Physics.CheckBox(centre, halfExtents, Quaternion.identity, obstacleLayers, QueryTriggerInteraction.Ignore);
            costs[i] = hit ? Flow_Map.Blocked : (byte)openCost;
            blocked += hit ? 1 : 0;
        }

        // Next to a wall costs more, so paths keep a little off them
        for (int y = 0; y < g.height; y++)
        {
            for (int x = 0; x < g.width; x++)
            {
                int cell = y * g.width + x;

                if (costs[cell] == Flow_Map.Blocked)
                    continue;

                for (int d = 0; d < 8; d++)
                {
                    Flow_Direction.Offset(d, out int dx, out int dy);

                    if (g.InBounds(x + dx, y + dy) && costs[(y + dy) * g.width + x + dx] == Flow_Map.Blocked)
                    {
                        costs[cell] = (byte)Mathf.Max(openCost, nearWallCost);
                        break;
                    }
                }
            }
        }

        watch.Stop();

        Undo.RecordObject(map, "Bake Flow Obstacles");
        map.grid = g;
        map.costs = costs;
        map.blockedCells = blocked;
        map.bakeMs = (float)watch.Elapsed.TotalMilliseconds;
        map.bakedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        EditorUtility.SetDirty(map);
        AssetDatabase.SaveAssetIfDirty(map);

        Undo.RecordObject(this, "Bake Flow Obstacles");
        obstacleBakeMs = map.bakeMs;
        blockedCells = blocked;
        EditorUtility.SetDirty(this);

        if (obstacleLayers.value == 0)
            Debug.LogWarning($"{name}: Obstacle Layers is empty, so nothing is blocked.", this);

        Debug.Log($"{name}: baked {count} cells ({blocked} blocked) in {map.bakeMs:0.0} ms.", this);
        PreviewFields();
    }

    // Solves every Flow_Goal's field in edit mode with the same jobs and frame slicing as the game, so the gizmos
    // show it and the stats show what it would cost
    [InspectorButton("Preview Fields")]
    private void PreviewFields()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning($"{name}: in play mode the live fields are drawn; Preview Fields is for edit mode.", this);
            return;
        }

        if (map == null || !map.IsBaked)
        {
            Debug.LogWarning($"{name}: bake obstacles first.", this);
            return;
        }

        previewGoals.Clear();
        previewDirections.Clear();
        previewDistances.Clear();
        previewGoals.AddRange(FindObjectsByType<Flow_Goal>(FindObjectsInactive.Exclude));

        Flow_Grid g = map.grid;
        int cells = g.CellCount;
        double toMs = TicksToMs();
        Solver solver = CreateSolver(cells, Allocator.TempJob);
        List<SolveStats> stats = new List<SolveStats>();

        try
        {
            foreach (Flow_Goal goal in previewGoals)
            {
                int cell = g.CellIndex(goal.transform.position);

                if (cell < 0)
                {
                    Debug.LogWarning($"{goal.name}: outside the flow grid.", goal);
                    previewDirections.Add(null);
                    previewDistances.Add(null);
                    continue;
                }

                NativeArray<byte>.Copy(map.costs, solver.Cost);
                solver.Seeds[0] = cell;
                solver.Timing[0] = 0;
                solver.Repair = false;
                solver.Frames = 0;
                solver.TotalMs = 0;
                solver.TopMs = 0;
                solver.BottomMs = double.MaxValue;

                JobHandle handle = new Flow_InitJob
                {
                    Cost = solver.Cost,
                    Seeds = solver.Seeds,
                    Distance = solver.Distance,
                    Heap = solver.Heap,
                    State = solver.State,
                    Timing = solver.Timing,
                    SeedCount = 1,
                    Width = g.width,
                    Height = g.height
                }.Schedule();

                // The same frames the game would take, run back to back
                do
                {
                    handle = ScheduleSliceJobs(solver, g, cellsPerFrame, handle);
                    handle.Complete();
                    RecordFrame(solver, toMs);
                    handle = default;
                }
                while (solver.State[2] == 0);

                previewDirections.Add(solver.Directions.ToArray());
                previewDistances.Add(solver.Distance.ToArray());
                stats.Add(Stats(goal.name, solver));
            }
        }
        finally
        {
            DisposeSolver(solver);
        }

        Undo.RecordObject(this, "Preview Flow Fields");
        lastSolves = stats.ToArray();
        EditorUtility.SetDirty(this);
        SceneView.RepaintAll();

        if (previewGoals.Count == 0)
            Debug.Log($"{name}: no Flow_Goal in the scene to preview. Add one to see a field.", this);
    }

    private Flow_Map CreateMap()
    {
        const string folder = "Assets/Resources/Flow";

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Resources", "Flow");

        string sceneName = string.IsNullOrEmpty(gameObject.scene.name) ? "Level" : gameObject.scene.name;
        Flow_Map created = ScriptableObject.CreateInstance<Flow_Map>();
        AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath($"{folder}/Flow_Map {sceneName}.asset"));

        Undo.RecordObject(this, "Create Flow Map");
        EditorUtility.SetDirty(this);
        return created;
    }

    // Draws obstacles and one field near the Scene view camera. Called for this object, and by a selected Flow_Goal.
    private void OnDrawGizmosSelected()
    {
        DrawGizmos(null);
    }

    public void DrawGizmos(Flow_Goal goal)
    {
        Flow_Grid g = ready ? grid : map != null && map.IsBaked ? map.grid : bakeGrid;

        // The grid's outline
        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawWireCube(g.Centre + Vector3.up * 0.05f, g.Size);

        byte[] costs = ready ? baked : map != null && map.IsBaked ? map.costs : null;

        if (costs == null)
            return;

        GetFieldToDraw(goal, out byte[] fieldDirections, out int[] fieldDistances, out int fieldOffset);

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 focus = view != null ? view.pivot : g.Centre;
        Vector2Int min = g.CellCoords(focus - new Vector3(drawRadius, 0f, drawRadius));
        Vector2Int max = g.CellCoords(focus + new Vector3(drawRadius, 0f, drawRadius));
        min = Vector2Int.Max(min, Vector2Int.zero);
        max = Vector2Int.Min(max, new Vector2Int(g.width - 1, g.height - 1));

        float furthest = 1f;

        if (drawDistance && fieldDistances != null)
        {
            foreach (int d in fieldDistances)
            {
                if (d != Flow_Solve.Unreached)
                    furthest = Mathf.Max(furthest, d);
            }
        }

        // Arrows are grouped into colour bands by distance so they're drawn in a few calls, not one per cell
        const int Bands = 6;
        List<Vector3>[] arrows = new List<Vector3>[Bands];

        for (int b = 0; b < Bands; b++)
            arrows[b] = new List<Vector3>();

        Vector3 flat = new Vector3(g.cellSize * 0.92f, 0.02f, g.cellSize * 0.92f);
        float lift = 0.15f;

        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                int cell = y * g.width + x;
                Vector3 centre = g.CellCentre(x, y) + Vector3.up * lift;
                byte c = ready ? cost[cell] : costs[cell];

                if (drawObstacles && c == Flow_Map.Blocked)
                {
                    Gizmos.color = new Color(1f, 0.2f, 0.15f, 0.55f);
                    Gizmos.DrawCube(centre, flat);
                }
                else if (drawObstacles && c > 1)
                {
                    Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.18f);
                    Gizmos.DrawCube(centre, flat);
                }

                if (!drawField || fieldDirections == null)
                    continue;

                byte direction = fieldDirections[fieldOffset + cell];
                int distance = fieldDistances != null ? fieldDistances[cell] : 0;
                float t = fieldDistances != null && distance != Flow_Solve.Unreached ? distance / furthest : 0f;

                if (drawDistance && distance != Flow_Solve.Unreached && c != Flow_Map.Blocked)
                {
                    Gizmos.color = Color.Lerp(new Color(0.2f, 1f, 0.4f, 0.25f), new Color(0.3f, 0.3f, 1f, 0.25f), t);
                    Gizmos.DrawCube(centre, flat);
                }

                if (direction == Flow_Direction.Goal)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireCube(centre, flat);
                    continue;
                }

                if (direction == Flow_Direction.None)
                    continue;

                Vector3 along = Flow_Direction.ToWorld(direction) * (g.cellSize * 0.38f);
                Vector3 side = new Vector3(-along.z, 0f, along.x) * 0.45f;
                Vector3 tip = centre + along;
                List<Vector3> band = arrows[Mathf.Clamp((int)(t * Bands), 0, Bands - 1)];

                band.Add(centre - along);
                band.Add(tip);
                band.Add(tip);
                band.Add(tip - along * 0.5f + side);
                band.Add(tip);
                band.Add(tip - along * 0.5f - side);
            }
        }

        for (int b = 0; b < Bands; b++)
        {
            if (arrows[b].Count == 0)
                continue;

            // Near the goal white, far from it blue
            Gizmos.color = Color.Lerp(Color.white, new Color(0.35f, 0.6f, 1f), b / (float)(Bands - 1));
            Gizmos.DrawLineList(arrows[b].ToArray());
        }
    }

    private void GetFieldToDraw(Flow_Goal goal, out byte[] fieldDirections, out int[] fieldDistances, out int offset)
    {
        fieldDirections = null;
        fieldDistances = null;
        offset = 0;

        if (ready)
        {
            int index = goal != null && IsValid(goal.Field) ? goal.Field.Index : drawFieldIndex;

            if (index < 0 || index >= fields.Length || !fields[index].HasResult)
                return;

            // Live data: the shared directions array and the field's last distances
            fieldDirections = gizmoDirections ??= new byte[cellCount];
            readers.Complete();
            NativeArray<byte>.Copy(directions, index * cellCount, gizmoDirections, 0, cellCount);
            fieldDistances = gizmoDistances ??= new int[cellCount];
            fields[index].Distance.CopyTo(gizmoDistances);
            return;
        }

        int preview = goal != null ? previewGoals.IndexOf(goal) : drawFieldIndex;

        if (preview < 0 || preview >= previewDirections.Count)
            return;

        fieldDirections = previewDirections[preview];
        fieldDistances = previewDistances[preview];
    }

    [NonSerialized] private byte[] gizmoDirections;
    [NonSerialized] private int[] gizmoDistances;
    #endregion
#endif
}
