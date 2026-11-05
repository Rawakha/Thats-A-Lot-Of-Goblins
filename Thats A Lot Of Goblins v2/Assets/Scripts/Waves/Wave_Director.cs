using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Runs the waves from a Wave_Settings asset over the scene's Wave_Holes:
//   Each wave picks how many holes spawn (between the settings' Min Holes and the wave's max), picks those holes at
//   random, and gives each a random other hole as its target. The wave's goblins are split between them; each hole
//   sends its share out one by one (Spawn Interval apart), hopping out of its bottom to the rim, and they walk to the
//   target hole and drop in.
//   A wave is over once its goblins and any survivors from skipped waves have been killed or dropped in. Time Between Waves later the
//   next starts, or (Wait For Input) waits for the player to press Next Wave; until the Last Wave, or for ever.
// WaveStarted / WaveCleared / WaitingForInput are for the UI and the juice to come.
//
// GC: none per frame. Lists are sized once; picking holes shuffles a list it keeps. One UpdateManager subscription
// (OnUpdate), plus a Killed subscription on Horde_System and a Reached subscription on each hole.
// One line of a debug wave: goblins out of one hole, heading for another
[Serializable]
public struct Wave_DebugSpawn
{
    [Tooltip("The hole they come out of.")]
    public Wave_Hole spawn;

    [Tooltip("The hole they walk to.")]
    public Wave_Hole target;

    [Tooltip("How many come out. 0 keeps them coming until Stop Debug Wave.")]
    [Min(0)] public int count;
}

// A debug wave's settings, grouped so the whole lot hides with Debug Waves off
[Serializable]
public class Wave_DebugWave
{
    [Tooltip("Each line is a hole to come out of, a hole to walk to and how many (0: endless).")]
    public Wave_DebugSpawn[] spawns;

    [Tooltip("Seconds between goblins from each hole. 0 uses the settings' Spawn Interval.")]
    [Min(0f)] public float interval;

    [Tooltip("Start the debug wave as soon as play starts (otherwise press Next Wave / Space).")]
    public bool onPlay;
}

public class Wave_Director : MonoBehaviour
{
    private enum Phase
    {
        Idle,
        Waiting,
        Ready,
        Spawning,
        Running,
        Finished,
        Stopped
    }

    private struct Source
    {
        public Wave_Hole Hole;
        public Wave_Hole Target;
        public int Left;
        public float Next;
        public float Interval;
        public bool Endless;    // debug waves: never runs out
    }

    // From the Area_Root on this object
    private Wave_Settings settings;

    [Tooltip("The holes waves use. Empty: every Wave_Hole in the scene.")]
    [SerializeField] private List<Wave_Hole> holes = new List<Wave_Hole>();

    [Tooltip("Start wave 1 (after its delay) as soon as play starts.")]
    [SerializeField] private bool startOnPlay = true;

    [Tooltip("Starts the next wave when the settings' Wait For Input is on: Game / Next Wave (Space, or the gamepad's " +
             "south button) in the InputActions asset.")]
    [SerializeField] private InputActionReference nextWaveAction;

    [Header("Debug Wave")]
    [Tooltip("For recording clips: send your own Debug Spawns instead of the normal waves. Next Wave (Space) then starts " +
             "and stops the debug wave, and the normal waves don't start on play.")]
    [SerializeField] private bool debugWaves;

    [ShowIf(nameof(debugWaves), true)]
    [SerializeField] private Wave_DebugWave debugWave = new Wave_DebugWave();

    [Header("Stats")]
    [SerializeField, ReadOnly] private int wave;
    // Shown in the inspector only, so nothing reads it in code
#pragma warning disable 0414
    [SerializeField, ReadOnly] private string phase = "Idle";
#pragma warning restore 0414
    [SerializeField, ReadOnly] private int goblinsThisWave;
    [SerializeField, ReadOnly] private int holesThisWave;
    [Tooltip("Goblins of this wave still to come out of a hole.")]
    [SerializeField, ReadOnly] private int toSpawn;
    [Tooltip("Living goblins heading to holes, including survivors from skipped waves.")]
    [SerializeField, ReadOnly] private int alive;
    [SerializeField, ReadOnly] private int killedThisWave;
    [SerializeField, ReadOnly] private int reachedThisWave;
    [SerializeField, ReadOnly] private int totalKilled;
    [SerializeField, ReadOnly] private int totalReached;

    private readonly List<Source> sources = new List<Source>(8);
    private readonly List<Wave_Hole> shuffle = new List<Wave_Hole>(16);
    private Phase state = Phase.Idle;
    private float timer;
    private float nextCheck;
    private bool subscribed;
    private bool started;
    private bool debugRunning;

    public int Wave => wave;
    public int Alive => alive;
    public int ToSpawn => toSpawn;
    public Wave_Settings Settings => settings;
    public bool DebugWaves => debugWaves;
    public InputActionReference NextWaveAction => nextWaveAction;

    // The Next Wave button does nothing while the player has no control of the plane
    public bool InputLocked { get; set; }

    // Ready for the player to start this wave (the number), with Next Wave
    public event Action<int> WaitingForInput;
    public event Action<int> WaveStarted;
    public event Action<int> WaveCleared;
    public event Action AreaCleared;
    public event Action<Horde_Goblin> GoblinKilled;
    public event Action<Wave_Hole, Horde_Goblin> GoblinReachedHole;

    private void Awake()
    {
        Area_Settings area = TryGetComponent(out Area_Root root) ? root.Settings : null;
        settings = area != null ? area.waves : null;
    }

    private void Start()
    {
        if (settings == null || settings.goblinType == null)
        {
            Debug.LogError($"{name}: Wave_Director needs an Area_Root on this object, with Area Settings whose Wave Settings have a Goblin Type.", this);
            enabled = false;
            return;
        }

        if (holes.Count == 0)
            holes.AddRange(FindObjectsByType<Wave_Hole>(FindObjectsInactive.Exclude));

        // Sanitise before subscribing: duplicates would also count every arrival more than once.
        for (int i = holes.Count - 1; i >= 0; i--)
            if (!Usable(holes[i]) || holes.IndexOf(holes[i]) != i)
                holes.RemoveAt(i);

        if (holes.Count < 2)
        {
            Debug.LogError($"{name}: waves need at least two distinct, enabled Wave_Holes with enabled Flow_Goals.", this);
            enabled = false;
            return;
        }

        if (Horde_System.Instance == null)
        {
            Debug.LogError($"{name}: No Horde_System in the scene.", this);
            enabled = false;
            return;
        }

        if (settings.waitForInput && nextWaveAction == null)
            Debug.LogWarning($"{name}: Wait For Input is on but no Next Wave Action is set, so waves only start from " +
                             "the Next Wave button. Assign Game / Next Wave from the InputActions asset.", this);

        Horde_System.Instance.Prewarm(settings.goblinType);
        started = true;
        Subscribe();

        if (debugWaves)
        {
            if (debugWave.onPlay)
                StartDebugWave();
        }
        else if (startOnPlay)
        {
            StartWaves();
        }
    }

    private void OnEnable() => Subscribe();

    private void OnDisable()
    {
        if (!subscribed)
            return;

        subscribed = false;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnUpdate -= Tick;

        if (nextWaveAction != null)
            nextWaveAction.action.Disable();

        if (Horde_System.Instance != null)
            Horde_System.Instance.Killed -= OnKilled;

        for (int i = 0; i < holes.Count; i++)
        {
            if (holes[i] != null)
                holes[i].Reached -= OnReached;
        }
    }

    // Start does the first subscribe, once it has the holes
    private void Subscribe()
    {
        if (!started || subscribed || holes.Count < 2 || UpdateManager.Instance == null || Horde_System.Instance == null)
            return;

        subscribed = true;
        UpdateManager.Instance.OnUpdate += Tick;
        Horde_System.Instance.Killed += OnKilled;

        if (nextWaveAction != null)
            nextWaveAction.action.Enable();

        for (int i = 0; i < holes.Count; i++)
        {
            if (holes[i] != null)
                holes[i].Reached += OnReached;
        }
    }

    [InspectorButton("Start Waves", true)]
    public void StartWaves()
    {
        wave = 0;
        totalKilled = 0;
        totalReached = 0;
        Wait(settings.firstWaveDelay);
    }

    // Skips straight to the next wave (the current one's goblins carry on)
    [InspectorButton("Next Wave", true)]
    public void NextWave()
    {
        if (state != Phase.Idle && state != Phase.Finished && state != Phase.Stopped)
            BeginWave();
    }

    // Ends the waves for good (the run failed): nothing more spawns and no wave clears. Goblins already out carry on.
    [InspectorButton("Stop Waves", true)]
    public void StopWaves()
    {
        sources.Clear();
        toSpawn = 0;
        debugRunning = false;
        SetPhase(Phase.Stopped);
    }

    // Sends the Debug Spawns instead of the normal waves (the goblins already out carry on). When a debug wave with
    // no endless lines is cleared, nothing more comes until a button is pressed.
    [InspectorButton("Start Debug Wave", true, ShowIf = nameof(debugWaves))]
    public void StartDebugWave()
    {
        // Stopped means the run failed: the debug wave's Space toggle must not restart spawning
        if (state == Phase.Stopped)
            return;

        if (!started || debugWave.spawns == null || debugWave.spawns.Length == 0)
        {
            Debug.LogWarning($"{name}: no Debug Spawns to send.", this);
            return;
        }

        sources.Clear();
        toSpawn = 0;
        float now = Time.time;
        float interval = debugWave.interval > 0f ? debugWave.interval : settings.spawnInterval;

        for (int i = 0; i < debugWave.spawns.Length; i++)
        {
            Wave_DebugSpawn line = debugWave.spawns[i];

            if (!Usable(line.spawn) || !Usable(line.target) || line.spawn == line.target)
            {
                Debug.LogWarning($"{name}: Debug Spawn {i} needs two different, enabled holes (each with a Flow_Goal); skipped.", this);
                continue;
            }

            bool endless = line.count <= 0;

            sources.Add(new Source
            {
                Hole = line.spawn,
                Target = line.target,
                Left = endless ? 1 : line.count,
                Next = now + i * interval / debugWave.spawns.Length,
                Interval = interval,
                Endless = endless
            });

            if (!endless)
                toSpawn += line.count;
        }

        if (sources.Count == 0)
        {
            SetPhase(Phase.Idle);
            return;
        }

        debugRunning = true;
        killedThisWave = 0;
        reachedThisWave = 0;
        alive = CountWaveGoblins();
        nextCheck = now + 1f;
        SetPhase(Phase.Spawning);
    }

    // Stops a debug wave's spawning (endless lines too); goblins already out carry on
    [InspectorButton("Stop Debug Wave", true, ShowIf = nameof(debugWaves))]
    public void StopDebugWave()
    {
        if (!debugRunning)
            return;

        sources.Clear();
        toSpawn = 0;
        debugRunning = false;
        SetPhase(Phase.Idle);
    }

    private void Tick()
    {
        // Debug waves: Next Wave (Space) starts the debug wave, and stops it while it's running
        if (debugWaves && !InputLocked && nextWaveAction != null && nextWaveAction.action.WasPressedThisFrame())
        {
            if (debugRunning)
                StopDebugWave();
            else
                StartDebugWave();
        }

        switch (state)
        {
            case Phase.Waiting:
                timer -= Time.deltaTime;

                if (timer > 0f)
                    break;

                if (settings.waitForInput)
                {
                    SetPhase(Phase.Ready);
                    WaitingForInput?.Invoke(wave + 1);
                }
                else
                {
                    BeginWave();
                }

                break;

            case Phase.Ready:
                if (!debugWaves && !InputLocked && nextWaveAction != null && nextWaveAction.action.WasPressedThisFrame())
                    BeginWave();

                break;

            case Phase.Spawning:
                SpawnDue();
                CheckCleared();
                break;

            case Phase.Running:
                CheckCleared();
                break;
        }
    }

    private void BeginWave()
    {
        // Recheck before every wave in case a hole was disabled or destroyed during play.
        shuffle.Clear();
        for (int i = 0; i < holes.Count; i++)
            if (Usable(holes[i])) shuffle.Add(holes[i]);
        if (shuffle.Count < 2)
        {
            Debug.LogError($"{name}: cannot start a wave without two usable Wave_Holes. Restore them and Start Waves again.", this);
            sources.Clear();
            toSpawn = 0;
            SetPhase(Phase.Idle);
            return;
        }

        wave++;
        goblinsThisWave = settings.GoblinsForWave(wave);
        killedThisWave = 0;
        reachedThisWave = 0;
        alive = CountWaveGoblins();
        nextCheck = Time.time + 1f;
        toSpawn = goblinsThisWave;

        PickSources();
        SetPhase(Phase.Spawning);
        WaveStarted?.Invoke(wave);
    }

    // How many holes this wave (between the floor and the wave's max, capped to the holes there are), which ones, and
    // a random other hole for each to send its goblins to. The wave's goblins are shared out between them.
    private void PickSources()
    {
        int max = Mathf.Min(settings.MaxHolesForWave(wave), shuffle.Count);
        int min = Mathf.Min(settings.minHoles, max);
        holesThisWave = UnityEngine.Random.Range(min, max + 1);

        for (int i = shuffle.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (shuffle[i], shuffle[j]) = (shuffle[j], shuffle[i]);
        }

        sources.Clear();
        debugRunning = false;
        float now = Time.time;

        for (int i = 0; i < holesThisWave; i++)
        {
            Wave_Hole hole = shuffle[i];
            Wave_Hole target = PickTarget(hole);

            // Even shares, the first few holes taking one more each if it doesn't divide evenly
            int share = goblinsThisWave / holesThisWave + (i < goblinsThisWave % holesThisWave ? 1 : 0);

            sources.Add(new Source
            {
                Hole = hole,
                Target = target,
                Left = share,
                Next = now + i * settings.spawnInterval / holesThisWave,    // staggered so holes don't fire in step
                Interval = settings.spawnInterval
            });
        }
    }

    // Each hole launches its next goblin once its interval is up (several if a frame ran long)
    private void SpawnDue()
    {
        float now = Time.time;
        Flow_System flow = Flow_System.Instance;
        Horde_System horde = Horde_System.Instance;

        for (int i = 0; i < sources.Count; i++)
        {
            Source source = sources[i];

            if (source.Left <= 0)
                continue;

            // A removed source/target cannot finish its pending spawns; cancel them so the wave can still clear.
            if (!Usable(source.Hole) || !Usable(source.Target))
            {
                if (!source.Endless)
                    toSpawn -= source.Left;

                source.Endless = false;
                source.Left = 0;
                sources[i] = source;
                Debug.LogWarning($"{name}: a wave hole became unavailable; its pending spawns were cancelled.", this);
                continue;
            }

            // The target's field is taken in its own Start; wait for it
            if (flow == null || !flow.IsValid(source.Target.Field))
                continue;

            while (source.Left > 0 && now >= source.Next)
            {
                Launch(horde, source.Hole, source.Target, !source.Endless);

                if (!source.Endless)
                    source.Left--;

                source.Next += source.Interval;
            }

            sources[i] = source;
        }

        if (toSpawn <= 0 && !AnyEndless())
            SetPhase(Phase.Running);
    }

    private bool AnyEndless()
    {
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i].Endless && sources[i].Left > 0)
                return true;
        }

        return false;
    }

    // Hops out of the hole's bottom to a spot round the rim (all round, leaning toward the target), sometimes flipping
    // Counted: one of the wave's goblins still to come (an endless debug line's aren't)
    private void Launch(Horde_System horde, Wave_Hole hole, Wave_Hole target, bool counted = true)
    {
        Vector3 from = hole.SpawnPoint();
        Vector3 toTarget = target.Centre - hole.Centre;
        toTarget.y = 0f;
        toTarget = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.forward;

        Vector2 random = UnityEngine.Random.insideUnitCircle.normalized;
        Vector3 outward = Vector3.Lerp(new Vector3(random.x, 0f, random.y), toTarget, settings.hopTowardTarget);
        outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : toTarget;

        float distance = hole.MouthRadius + Random(settings.hopDistance);
        Vector3 landing = hole.Centre + outward * distance;
        float flip = UnityEngine.Random.value < settings.flipChance ? settings.flipDegrees : 0f;

        Horde_Goblin goblin = horde.SpawnEmerging(settings.goblinType, from, landing, Quaternion.LookRotation(outward),
            target.Field, Mathf.Max(0.05f, Random(settings.hopDuration)), Random(settings.hopHeight), flip, settings.hopEase);

        if (counted)
            toSpawn--;

        if (goblin == null)
            return;

        goblin.Destination = target;
        hole.CountSent();
        alive++;
    }

    // A random other hole at least Min Target Distance away (flat distance), or the furthest if none is that far
    private Wave_Hole PickTarget(Wave_Hole from)
    {
        float minSqr = settings.minTargetDistance * settings.minTargetDistance;
        Wave_Hole furthest = null;
        float furthestSqr = -1f;
        Wave_Hole picked = null;
        int farEnough = 0;

        for (int i = 0; i < shuffle.Count; i++)
        {
            Wave_Hole hole = shuffle[i];

            if (hole == from || hole == null)
                continue;

            Vector3 offset = hole.Centre - from.Centre;
            float distanceSqr = offset.x * offset.x + offset.z * offset.z;

            if (distanceSqr > furthestSqr)
            {
                furthestSqr = distanceSqr;
                furthest = hole;
            }

            // Reservoir pick: each far-enough hole has an equal chance, in one pass, with no list
            if (distanceSqr >= minSqr && UnityEngine.Random.Range(0, ++farEnough) == 0)
                picked = hole;
        }

        return picked != null ? picked : furthest;
    }

    private static float Random(Vector2 range) => UnityEngine.Random.Range(range.x, range.y);

    private static bool Usable(Wave_Hole hole) => hole != null && hole.isActiveAndEnabled
        && hole.TryGetComponent(out Flow_Goal goal) && goal.isActiveAndEnabled;

    private void OnKilled(Horde_Goblin goblin)
    {
        if (!(goblin.Destination is Wave_Hole))
            return;

        alive = Mathf.Max(0, alive - 1);
        killedThisWave++;
        totalKilled++;
        GoblinKilled?.Invoke(goblin);
    }

    private void OnReached(Wave_Hole hole, Horde_Goblin goblin)
    {
        alive = Mathf.Max(0, alive - 1);
        reachedThisWave++;
        totalReached++;
        GoblinReachedHole?.Invoke(hole, goblin);
    }

    // Over once everything's out and nothing's left walking. Once a second it also counts the goblins itself, so a
    // wave can't hang if some were removed another way (Despawn All).
    private void CheckCleared()
    {
        if (toSpawn > 0 || AnyEndless())
            return;

        // Verify zero before clearing too, so event counts can never clear a wave with survivors.
        if (alive <= 0 || Time.time >= nextCheck)
        {
            nextCheck = Time.time + 1f;
            alive = CountWaveGoblins();
        }

        if (alive > 0)
            return;

        // A debug wave just stops when it's cleared
        if (debugRunning)
        {
            debugRunning = false;
            SetPhase(Phase.Idle);
            return;
        }

        WaveCleared?.Invoke(wave);

        if (settings.IsLastWave(wave))
        {
            SetPhase(Phase.Finished);
            AreaCleared?.Invoke();
        }
        else
            Wait(settings.timeBetweenWaves);
    }

    private int CountWaveGoblins()
    {
        Horde_System horde = Horde_System.Instance;
        int count = 0;

        if (horde == null)
            return count;

        for (int i = 0; i < horde.Count; i++)
        {
            Horde_Goblin goblin = horde[i];

            if (goblin.IsActive && !goblin.Dead && !goblin.FallingIn && goblin.Destination is Wave_Hole)
                count++;
        }

        return count;
    }

    private void Wait(float seconds)
    {
        timer = seconds;
        SetPhase(Phase.Waiting);
    }

    private void SetPhase(Phase next)
    {
        state = next;

        switch (next)
        {
            case Phase.Idle: phase = "Idle"; break;
            case Phase.Waiting: phase = "Waiting"; break;
            case Phase.Ready: phase = "Ready: press Next Wave"; break;
            case Phase.Spawning: phase = "Spawning"; break;
            case Phase.Running: phase = "Running"; break;
            case Phase.Stopped: phase = "Stopped"; break;
            default: phase = "Finished"; break;
        }
    }
}
