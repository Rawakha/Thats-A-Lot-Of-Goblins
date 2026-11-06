using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

// Every goblin in the world, and the pools they come from.
//   Pool: one per Goblin Type, filled up front (Prewarm, called by spawners as they start) so spawning in play just
//     reuses a GameObject. Spawn takes one out and starts it walking; Despawn puts it back.
//   Movement: each physics step, every walking goblin reads its flow field at the cell it's in and its velocity is
//     moved toward that direction at its speed (capped by acceleration). No direction (no field yet, walled in, at
//     the goal) means it slows to a stop. Three Rigidbody calls per goblin: position, velocity, velocity.
//   Ground: the world is flat, so walking goblins are held at Ground Height with gravity off and Y frozen: PhysX only
//     pushes them apart and off walls, with no ground contacts. Launch throws one (gravity on); it walks again once
//     it comes back down to the ground.
//   Facing: only ever data. The Rigidbody never rotates; the render group's animation job turns each goblin toward
//     where it's actually going, every rendered frame.
//   Rendering: one Horde_RenderGroup per type draws its goblins instanced from data (the goblins have no mesh
//     GameObjects); matrices are built in Burst jobs in LateUpdate, after physics interpolation has moved the roots.
//   Damage: bullets hit a goblin's collider (Horde_Goblin is a Combat_IDamageable); impacts that miss hurt every
//     goblin within their radius (this is a Combat_IAreaDamageable). Each hit plays the hit reaction and flash; at
//     no health the goblin is killed: knocked back, shrunk along the type's death ease (baked, no tweens) and
//     returned to the pool when the shrink ends.
// Per-goblin movement data lives in native lists indexed like the registry (swap-back on removal), ready to move
// into a Burst job; Rigidbody calls stay on the main thread.
//
// For now each goblin still has one GameObject (the root, with its Rigidbody).
//
// Stats: movement and render time are measured here; Physics.Simulate is read from the profiler (one recorder).
//
// GC: none per frame or per hit. Instantiate only when filling a pool (or if one runs dry, warned once). Two
// UpdateManager subscriptions (OnFixedUpdate for movement, OnLateUpdate for rendering and finishing deaths), plus
// one Combat_Damage area registration.
[DefaultExecutionOrder(Utilities.ExecutionOrder.DependantSingletons)]
public class Horde_System : Core_RegistryManager<Horde_Goblin>, Combat_IAreaDamageable, Combat_ITargetSource,
    Combat_IExplosionReceiver
{
    [Header("Ground")]
    [Tooltip("Height of the flat ground goblins walk on. Walking goblins are held here with no gravity, so they never " +
             "touch the ground collider: untick Goblin against the ground's layer in the Physics collision matrix.")]
    [SerializeField] private float groundHeight;

    [Header("Thrown")]
    [Tooltip("Layer a thrown goblin moves to while it flies, so it collides with the ground and buildings (which walking " +
             "goblins don't). Make it in Tags and Layers and tick it against the ground and obstacle layers.")]
    [SerializeField] private string airborneLayer = "GoblinAirborne";

    [Tooltip("A thrown goblin counts as at rest below this speed, m/s ...")]
    [SerializeField, Min(0f)] private float restSpeed = 0.6f;

    [Tooltip("... and below this spin, degrees/second ...")]
    [SerializeField, Min(0f)] private float restSpin = 90f;

    [Tooltip("... for this long, seconds. Then it gets up.")]
    [SerializeField, Min(0f)] private float restTime = 0.15f;

    [Tooltip("It gets up after this long whatever it's doing, seconds, so nothing rolls about for ever.")]
    [SerializeField, Min(0.1f)] private float maxAirTime = 4f;

    [Tooltip("A thrown goblin counts as hitting the ground when its falling speed suddenly drops by this much, m/s.")]
    [SerializeField, Min(0.1f)] private float impactSpeedJump = 2f;

    [Tooltip("Seconds it takes to get up, from however it lay to upright.")]
    [SerializeField, Min(0.01f)] private float getUpDuration = 0.35f;

    [Tooltip("Coming to rest this far below the ground (down a hole), it's removed instead of getting up, metres.")]
    [SerializeField, Min(0f)] private float lostBelow = 0.75f;

    [Header("Scream Particles")]
    [Tooltip("Played from a screaming goblin's mouth while it flies (v1's Scream Particles prefab). Empty: no particles.")]
    [SerializeField] private ParticleSystem screamParticlesPrefab;

    [Tooltip("Most goblins with scream particles at once. Past this the one that started screaming first gives its up.")]
    [SerializeField, Min(0)] private int maxScreamParticles = 12;

    [Header("Sound")]
    [Tooltip("Most goblin damage sounds started per frame, across the whole horde.")]
    [SerializeField, Min(0)] private int maxDamageSoundsPerFrame = 2;
    [Tooltip("Most extra damage sounds (the chance ones: groans) started per frame, across the whole horde. " +
             "Counted apart from the damage sounds so they still get heard in a busy fight.")]
    [SerializeField, Min(0)] private int maxExtraDamageSoundsPerFrame = 1;
    [Tooltip("Most goblin death sounds started per frame, across the whole horde.")]
    [SerializeField, Min(0)] private int maxDeathSoundsPerFrame = 2;

    [Header("Rendering")]
    [Tooltip("Goblins are only drawn when this box is in view. Make it cover the level.")]
    [SerializeField] private Bounds renderBounds = new Bounds(Vector3.zero, new Vector3(2000f, 200f, 2000f));

    [Header("Stats")]
    [SerializeField, ReadOnly] private int active;
    [SerializeField, ReadOnly] private int pooled;
    [SerializeField, ReadOnly] private int created;
    [SerializeField, ReadOnly] private int peakActive;
    [SerializeField, ReadOnly] private int killed;
    [Tooltip("Main-thread time of the last movement step.")]
    [SerializeField, ReadOnly] private float movementMs;
    [Tooltip("Main-thread time of the last frame's rendering (scheduling, waiting for the matrices, drawing).")]
    [SerializeField, ReadOnly] private float renderMs;
    [SerializeField, ReadOnly] private int drawCalls;

    [Header("Physics")]
    [Tooltip("Unity's Physics.Simulate for the last frame, all physics steps in it added up: the PhysX cost of the " +
             "goblins pushing each other and the rest of the scene. Read from the profiler, so only in the editor and " +
             "development builds.")]
    [SerializeField, ReadOnly] private float physicsSimulateMs;
    [Tooltip("Physics steps run in the last frame.")]
    [SerializeField, ReadOnly] private int physicsSteps;

    private readonly Dictionary<Horde_GoblinType, Stack<Horde_Goblin>> pools = new Dictionary<Horde_GoblinType, Stack<Horde_Goblin>>();
    private readonly HashSet<Horde_GoblinType> warnedEmpty = new HashSet<Horde_GoblinType>();
    private readonly Dictionary<Horde_GoblinType, Horde_RenderGroup> groups = new Dictionary<Horde_GoblinType, Horde_RenderGroup>();
    private readonly List<Horde_RenderGroup> groupList = new List<Horde_RenderGroup>();
    private Transform poolRoot;
    private readonly Stack<ParticleSystem> freeScreamParticles = new Stack<ParticleSystem>(16);
    private readonly List<Horde_Goblin> screamingWithParticles = new List<Horde_Goblin>(16);
    private int airborneLayerIndex = -1;
    private ProfilerRecorder simulateRecorder;

    // Per active goblin, in registry order
    private NativeList<int> fieldOffsets;
    private NativeList<float> maxSpeeds;
    private NativeList<float2> steers;          // cos, sin of the goblin's own steering offset
    private NativeList<float> healths;

    // Killed goblins shrinking away, and when each goes back to the pool. Swap-back removal; only the dying are here.
    // Corpses are the ones lying there waiting their turn (Horde_DebrisBudget); a burst or a shrinking one isn't.
    private struct Dying
    {
        public Horde_Goblin Goblin;
        public float End;
        public float Born;
        public bool Corpse;
        public int Cell;
    }

    private readonly List<Dying> dying = new List<Dying>(512);
    private int corpseCount;
    private Horde_CrowdGrid corpseCrowd;
    private float corpseThinAllowance;

    // Dead goblins lying on the ground, waiting their turn
    public int CorpseCount => corpseCount;

    // A goblin was killed (just before it starts shrinking): for the goblin counter, score and effects
    public event System.Action<Horde_Goblin> Killed;

    public int KilledCount => killed;

    private const RigidbodyConstraints Walking = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

    public static Horde_System Instance { get; private set; }

    public int ActiveCount => Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"{name}: A second Horde_System was found, this one is disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;

        airborneLayerIndex = string.IsNullOrEmpty(airborneLayer) ? -1 : LayerMask.NameToLayer(airborneLayer);

        if (airborneLayerIndex < 0)
            Debug.LogWarning($"{name}: No layer called '{airborneLayer}'. Thrown goblins keep their own layer, so they " +
                             "fall through the ground (walking goblins don't touch it).", this);

        poolRoot = new GameObject("Pool").transform;
        poolRoot.SetParent(transform, false);

        // Scream particles, made up front and parked under the pool
        if (screamParticlesPrefab != null)
        {
            for (int i = 0; i < maxScreamParticles; i++)
            {
                ParticleSystem particles = Instantiate(screamParticlesPrefab, poolRoot, false);
                particles.gameObject.SetActive(false);
                freeScreamParticles.Push(particles);
            }
        }

        fieldOffsets = new NativeList<int>(256, Allocator.Persistent);
        maxSpeeds = new NativeList<float>(256, Allocator.Persistent);
        steers = new NativeList<float2>(256, Allocator.Persistent);
        healths = new NativeList<float>(256, Allocator.Persistent);
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance == null)
        {
            Debug.LogError($"{name}: No UpdateManager in the scene, goblins won't move.", this);
            return;
        }

        UpdateManager.Instance.OnFixedUpdate += Move;
        UpdateManager.Instance.OnLateUpdate += Render;
        simulateRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.Simulate");

        // Impacts that miss a goblin (bullets into the ground) still hurt the goblins around them
        Combat_Damage.RegisterArea(this);

        // Aim assists look for goblins through this
        Combat_Targets.Register(this);

        // Bombs blow them apart and fling them
        Combat_Damage.RegisterExplosions(this);
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
        {
            UpdateManager.Instance.OnFixedUpdate -= Move;
            UpdateManager.Instance.OnLateUpdate -= Render;
        }

        simulateRecorder.Dispose();
        Combat_Damage.UnregisterArea(this);
        Combat_Targets.Unregister(this);
        Combat_Damage.UnregisterExplosions(this);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (Instance == this)
            Instance = null;

        for (int i = 0; i < groupList.Count; i++)
            groupList[i].Dispose();

        groupList.Clear();
        groups.Clear();

        if (fieldOffsets.IsCreated)
        {
            fieldOffsets.Dispose();
            maxSpeeds.Dispose();
            steers.Dispose();
            healths.Dispose();
        }
    }

    #region Pool
    // Makes sure the type's pool holds at least its Pool Size. Call before play gets busy (spawners do, on Start).
    public void Prewarm(Horde_GoblinType type)
    {
        if (!IsUsable(type))
            return;

        Stack<Horde_Goblin> pool = PoolFor(type);

        // Corpses hold on to their goblin, so the pool needs room for them on top of the living
        while (pool.Count + CountActive(type) < type.poolSize + Horde_DebrisBudget.Current.MaxCorpses)
            pool.Push(Create(type));

        RefreshCounts();
    }

    // Takes a goblin out of the pool and starts it walking along a field. Null if the type can't be used.
    public Horde_Goblin Spawn(Horde_GoblinType type, Vector3 position, Quaternion rotation, Flow_FieldHandle field)
    {
        return Spawn(type, position, rotation, field, true);
    }

    // Spawns a goblin flying: at this position (its height kept, e.g. down in a hole), thrown at this velocity and
    // tumbling at spin degrees/second. It lands, squishes, pops upright and walks the field.
    public Horde_Goblin SpawnLaunched(Horde_GoblinType type, Vector3 position, Quaternion rotation, Vector3 velocity,
        float spin, Flow_FieldHandle field)
    {
        Horde_Goblin goblin = Spawn(type, position, rotation, field, false);

        if (goblin != null)
            Launch(goblin, velocity, spin);

        return goblin;
    }

    // Spawns a goblin hopping out of somewhere (a hole): drawn along an arc from 'from' to 'landing' over duration
    // seconds (height: the arc's height, flipDegrees: how far it flips, ease: its progress along the arc), then it
    // lands with a squish and walks the field. Its body goes straight to the landing spot, so nothing can get stuck.
    public Horde_Goblin SpawnEmerging(Horde_GoblinType type, Vector3 from, Vector3 landing, Quaternion facing,
        Flow_FieldHandle field, float duration, float height, float flipDegrees, DG.Tweening.Ease ease)
    {
        Horde_Goblin goblin = Spawn(type, landing, facing, field, true);

        if (goblin == null)
            return null;

        float now = Time.time;
        goblin.Emerging = true;
        goblin.EmergeEnd = now + duration;
        goblin.Rigidbody.detectCollisions = false;

        landing.y = Level_GroundSurface.HeightAt(landing, groundHeight);
        groups[type].Emerge(goblin.RenderIndex, from, landing, now, duration, height, flipDegrees, ease);
        return goblin;
    }

    private Horde_Goblin Spawn(Horde_GoblinType type, Vector3 position, Quaternion rotation, Flow_FieldHandle field, bool onGround)
    {
        if (!IsUsable(type))
            return null;

        Stack<Horde_Goblin> pool = PoolFor(type);
        Horde_Goblin goblin;

        if (pool.Count > 0)
        {
            goblin = pool.Pop();
        }
        else
        {
            if (warnedEmpty.Add(type))
                Debug.LogWarning($"{name}: the {type.name} pool ran out, so goblins are being made mid-play. Raise its " +
                                 $"Pool Size (peak so far {peakActive}).", type);

            goblin = Create(type);
        }

        // Place it (on the ground, unless it's being launched) before it wakes, so interpolation doesn't smear it across
        // the map from where it was. The root never rotates: facing is data in the render group.
        if (onGround)
            position.y = Level_GroundSurface.HeightAt(position, groundHeight);

        Rigidbody body = goblin.Rigidbody;
        body.interpolation = RigidbodyInterpolation.None;
        body.position = position;
        body.rotation = Quaternion.identity;
        goblin.transform.SetPositionAndRotation(position, Quaternion.identity);
        goblin.gameObject.SetActive(true);
        SetWalking(goblin, true);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        goblin.InPool = false;
        goblin.WearSampleValid = false;
        goblin.SupportHeight = position.y;
        goblin.SupportWearRevision = Level_GrassWearSystem.Revision;
        Register(goblin);
        groups[type].Add(goblin, rotation);

        int index = goblin.RegistryIndex;
        float steer = UnityEngine.Random.Range(-type.steerSpread, type.steerSpread) * Mathf.Deg2Rad;
        Flow_System flow = Flow_System.Instance;

        fieldOffsets[index] = flow != null && flow.IsValid(field) ? flow.FieldOffset(field) : -1;
        maxSpeeds[index] = type.moveSpeed * (1f + UnityEngine.Random.Range(-type.speedVariation, type.speedVariation));
        healths[index] = type.maxHealth;
        steers[index] = new float2(math.cos(steer), math.sin(steer));

        RefreshCounts();
        return goblin;
    }

    // Hurts a goblin: plays its hit reaction (pushed along velocity, which is any length: only its direction is
    // used), and kills it when its health runs out. Point is where it was hit, for the twist.
    // Shows a hit marker at the point unless told not to (area damage shows one for the whole impact instead).
    // Returns whether it hit a living goblin, and whether that killed it.
    private int damageSoundFrame = -1;
    private int damageSoundsThisFrame;
    private int extraDamageSoundsThisFrame;
    private int deathSoundFrame = -1;
    private int deathSoundsThisFrame;

    // The type's damage sound at the point, if this frame still has room for one
    // and each extra one that wins its chance roll
    // Effects hear about the kill first (Horde_Gibs says whether it died in pieces), then its death sound plays
    private void AnnounceKill(Horde_Goblin goblin)
    {
        goblin.DiedInPieces = false;
        Killed?.Invoke(goblin);

        Horde_GoblinAudio audio = goblin.Type.audio;

        if (audio == null)
            return;

        int frame = Time.frameCount;

        if (frame != deathSoundFrame)
        {
            deathSoundFrame = frame;
            deathSoundsThisFrame = 0;
        }

        if (deathSoundsThisFrame >= maxDeathSoundsPerFrame)
            return;

        SoundData sound = goblin.DeathBurst
            ? (audio.blownApartSound != null ? audio.blownApartSound : audio.gibDeathSound)
            : goblin.DiedInPieces ? audio.gibDeathSound : audio.corpseDeathSound;

        if (sound != null && Utilities.TryPlaySound(sound, goblin.transform.position))
            deathSoundsThisFrame++;
    }

    private void PlayDamageSound(Horde_GoblinType type, Vector3 point)
    {
        int frame = Time.frameCount;

        if (frame != damageSoundFrame)
        {
            damageSoundFrame = frame;
            damageSoundsThisFrame = 0;
            extraDamageSoundsThisFrame = 0;
        }

        Horde_GoblinAudio audio = type.audio;

        if (audio == null)
            return;

        if (audio.damageSound != null && damageSoundsThisFrame < maxDamageSoundsPerFrame
            && Utilities.TryPlaySound(audio.damageSound, point))
            damageSoundsThisFrame++;

        Horde_ChanceSound[] extras = audio.extraDamageSounds;

        if (extras == null)
            return;

        for (int i = 0; i < extras.Length && extraDamageSoundsThisFrame < maxExtraDamageSoundsPerFrame; i++)
        {
            if (extras[i].sound != null && UnityEngine.Random.value < extras[i].chance
                && Utilities.TryPlaySound(extras[i].sound, point))
                extraDamageSoundsThisFrame++;
        }
    }

    public bool Damage(Horde_Goblin goblin, float damage, Vector3 velocity, Vector3 point, bool showMarker = true)
    {
        return Damage(goblin, damage, velocity, point, showMarker, out _);
    }

    public bool Damage(Horde_Goblin goblin, float damage, Vector3 velocity, Vector3 point, bool showMarker, out bool killedIt)
    {
        killedIt = false;

        if (goblin == null || !goblin.IsActive || goblin.Dead || goblin.FallingIn || goblin.Emerging || damage <= 0f)
            return false;

        Horde_GoblinType type = goblin.Type;
        Horde_RenderGroup group = groups[type];
        Vector3 root = goblin.transform.position;
        Vector3 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : (root - point).normalized;
        Vector3 impulse = direction * (damage * type.hitForce);

        group.Hit(goblin.RenderIndex, impulse, Vector3.Cross(point - root, impulse));
        PlayDamageSound(type, point);

        int index = goblin.RegistryIndex;
        float health = healths[index] - damage;
        healths[index] = health;

        if (health <= 0f)
        {
            Kill(goblin, direction);
            killedIt = true;
        }

        if (showMarker)
            UI_HitMarkers.TryPlay(point, killedIt ? UI_HitMarkerType.Kill : UI_HitMarkerType.Normal);

        return true;
    }

    // Area damage from Combat_Damage: every living goblin within the hit's radius of its point (flat distance) is hurt,
    // pushed away from the point. Reads last frame's positions from the render groups, so no engine calls per goblin.
    public void TakeAreaDamage(in Combat_Hit hit)
    {
        if (hit.Radius <= 0f || hit.Damage <= 0f)
            return;

        float radiusSqr = hit.Radius * hit.Radius;
        Vector3 point = hit.Point;
        bool anyHit = false;
        bool anyKilled = false;

        for (int g = 0; g < groupList.Count; g++)
        {
            Horde_RenderGroup group = groupList[g];

            // Backwards: a kill can't move anything in the group (despawns wait for the shrink), but stay safe
            for (int i = group.Count - 1; i >= 0; i--)
            {
                float3 position = group.LastPosition(i);
                float dx = position.x - point.x;
                float dz = position.z - point.z;

                if (dx * dx + dz * dz > radiusSqr)
                    continue;

                Horde_Goblin goblin = group.GoblinAt(i);
                Vector3 away = new Vector3(dx, 0f, dz);
                Vector3 push = away.sqrMagnitude > 0.0001f ? away : hit.Velocity;

                if (Damage(goblin, hit.Damage, push, point, false, out bool killedIt))
                {
                    anyHit = true;
                    anyKilled |= killedIt;
                }
            }
        }

        // One marker for the whole impact, red if it killed anything
        if (anyHit)
            UI_HitMarkers.TryPlay(point, anyKilled ? UI_HitMarkerType.Kill : UI_HitMarkerType.Normal);
    }

    // The best goblin for an aim assist: alive, out of its hole, in the query's cone and range, aimed at half its height.
    // Last frame's positions from the render groups, so no engine calls per goblin.
    public bool FindTarget(in Combat_TargetQuery query, out Vector3 point, out float score)
    {
        point = default;
        score = float.MinValue;
        bool found = false;

        // Goblins are ground targets
        if ((query.Kinds & Combat_TargetKind.Ground) == 0)
            return false;

        for (int g = 0; g < groupList.Count; g++)
        {
            Horde_RenderGroup group = groupList[g];

            for (int i = 0; i < group.Count; i++)
            {
                Horde_Goblin goblin = group.GoblinAt(i);

                if (goblin.Dead || goblin.FallingIn || goblin.Emerging || goblin.InPool)
                    continue;

                Vector3 candidate = (Vector3)group.LastPosition(i) + Vector3.up * (goblin.Type.height * 0.5f);

                if (!Combat_Targets.Score(query, candidate, out float candidateScore) || candidateScore <= score)
                    continue;

                score = candidateScore;
                point = candidate;
                found = true;
            }
        }

        return found;
    }

    // Kills a goblin: it stops, flashes, gets knocked back and up a little (the type's Death Knockback and Death Hop),
    // tips over away from the hit (direction: the way the hit pushed), and after the shrink delay shrinks along the
    // death ease, then goes back to the pool
    public void Kill(Horde_Goblin goblin, Vector3 direction) => Kill(goblin, direction, false);

    // Burst: blown apart (the middle of a blast): no knockback or topple, it pops out of existence in the type's Burst
    // Duration with its burst effect and sound
    public void Kill(Horde_Goblin goblin, Vector3 direction, bool burst)
    {
        if (goblin == null || !goblin.IsActive || goblin.Dead)
            return;

        Horde_GoblinType type = goblin.Type;
        goblin.Dead = true;
        goblin.DeathDirection = direction;
        goblin.DeathBurst = burst;
        killed++;
        StopScream(goblin);

        float now = Time.time;
        Horde_RenderGroup group = groups[type];

        // Killed in the air: it keeps tumbling with real physics and only starts to go once it has come to rest
        if (goblin.Physical && !burst)
        {
            goblin.Settled = false;
            AnnounceKill(goblin);
            return;
        }

        // On the ground: physics off, so bullets pass through and the living walk past it while it lies there
        Rigidbody body = goblin.Rigidbody;
        body.detectCollisions = false;
        body.isKinematic = true;

        if (burst)
        {
            Vector3 at = goblin.transform.position;

            if (type.burstEffect != null)
                type.burstEffect.Play(at, Vector3.up);

            group.Kill(goblin.RenderIndex, now, direction, 0f, type.burstDuration, false);
            dying.Add(new Dying { Goblin = goblin, End = now + type.burstDuration });
        }
        else
        {
            // Leans over where it stands, away from the hit, lies there for the delay, then shrinks
            AddCorpse(goblin, group, now, direction, true, true);
        }

        AnnounceKill(goblin);
    }

    // A blast, in one pass over the goblins (last frame's positions, flat distance): within the kill radius they're
    // blown apart; out to the fling radius they take the blast's damage and are thrown up and away, weaker toward the
    // edge, tumbling (and, if they live, screaming). The flying is the ordinary airborne state: they land, squish and
    // walk on. One hit marker for the whole blast.
    public void TakeExplosion(in Combat_Explosion explosion)
    {
        float killSqr = explosion.KillRadius * explosion.KillRadius;
        float flingSqr = explosion.FlingRadius * explosion.FlingRadius;
        float ring = Mathf.Max(explosion.FlingRadius - explosion.KillRadius, 0.01f);
        Vector3 point = explosion.Point;
        bool anyHit = false;
        bool anyKilled = false;
        int kills = 0;

        for (int g = 0; g < groupList.Count; g++)
        {
            Horde_RenderGroup group = groupList[g];

            // Kills don't move anything in a group (despawns wait for the shrink), so the indices hold
            for (int i = group.Count - 1; i >= 0; i--)
            {
                Horde_Goblin goblin = group.GoblinAt(i);

                if (goblin.Dead || goblin.FallingIn || goblin.Emerging || goblin.InPool)
                    continue;

                float3 position = group.LastPosition(i);
                float dx = position.x - point.x;
                float dz = position.z - point.z;
                float distanceSqr = dx * dx + dz * dz;

                if (distanceSqr > flingSqr)
                    continue;

                float distance = Mathf.Sqrt(distanceSqr);
                Vector3 away = distance > 0.001f ? new Vector3(dx / distance, 0f, dz / distance) : RandomFlat();
                anyHit = true;

                if (distanceSqr <= killSqr)
                {
                    Kill(goblin, away, true);
                    anyKilled = true;
                    kills++;
                    continue;
                }

                // Out in the ring: thrown, harder nearer the middle. Some die on the spot (more near the middle); the
                // rest fly screaming with their eyes open, and any the blast would have killed die when they land.
                float edge = Mathf.Clamp01((distance - explosion.KillRadius) / ring);
                float strength = 1f - 0.6f * edge;
                Vector3 throwVelocity = away * (explosion.FlingOut * strength) + Vector3.up * (explosion.FlingUp * strength);
                Launch(goblin, throwVelocity);

                if (UnityEngine.Random.value < Mathf.Lerp(explosion.InstantDeathInner, explosion.InstantDeathOuter, edge))
                {
                    Kill(goblin, away);
                    anyKilled = true;
                    kills++;
                    continue;
                }

                // Counted as a kill for the blast's message now; the death itself (and the counter) waits for the landing
                if (HurtInAir(goblin, explosion.Damage, away, point))
                {
                    anyKilled = true;
                    kills++;
                }

                StartScream(goblin);
            }
        }

        if (anyHit)
            UI_HitMarkers.TryPlay(point, anyKilled ? UI_HitMarkerType.Kill : UI_HitMarkerType.Normal);

        // The HUD's "BOOOOOM", if the blast killed enough
        if (kills > 0)
            UI_ContextMessages.TryPlay(UI_MessageContext.Explosion, point, kills);
    }

    private static Vector3 RandomFlat()
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    // Blast damage on a goblin that's been flung: the flinch, the sound and the health, but it doesn't die up there.
    // True if it would have: then it's doomed, and dies when it hits the ground.
    private bool HurtInAir(Horde_Goblin goblin, float damage, Vector3 direction, Vector3 point)
    {
        if (goblin == null || !goblin.IsActive || goblin.Dead || damage <= 0f)
            return false;

        Horde_GoblinType type = goblin.Type;
        Vector3 root = goblin.transform.position;
        Vector3 impulse = direction * (damage * type.hitForce);

        groups[type].Hit(goblin.RenderIndex, impulse, Vector3.Cross(point - root, impulse));
        PlayDamageSound(type, point);

        int index = goblin.RegistryIndex;
        float health = healths[index] - damage;
        healths[index] = health;

        if (health > 0f)
            return false;

        goblin.Doomed = true;
        return true;
    }

    // The moment a thrown goblin hits the ground (or a cliff top): its spin is mostly killed so it slams, skids and
    // stops, and it takes fall damage from how fast it came down. A doomed one, or one the fall finishes off, dies here.
    private void Impact(Horde_Goblin goblin, float speed, Vector3 velocity)
    {
        goblin.Impacted = true;

        Horde_GoblinType type = goblin.Type;
        Rigidbody body = goblin.Rigidbody;
        body.angularVelocity *= type.impactSpinKeep;
        body.angularDamping = type.groundAngularDamping;

        if (goblin.Dead)
            return;

        float damage = Mathf.Max(0f, speed - type.safeFallSpeed) * type.fallDamagePerSpeed;
        int index = goblin.RegistryIndex;
        float health = healths[index] - damage;
        healths[index] = health;

        if (!goblin.Doomed && health > 0f)
            return;

        if (damage > 0f)
            PlayDamageSound(type, goblin.transform.position);

        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        Kill(goblin, flat.sqrMagnitude > 0.0001f ? flat.normalized : RandomFlat());
    }

    // v1's scream: a tracked sound following the goblin while it flies (none if the AudioManager's slots are full),
    // and the scream particles out of its mouth (from a pool; when it's empty the longest screamer gives its up)
    private void StartScream(Horde_Goblin goblin)
    {
        Horde_GoblinAudio audio = goblin.Type.audio;
        SoundConfig scream = audio != null ? SoundData.Get(audio.screamSound) : null;

        if (scream != null && AudioManager.Instance != null &&
            !AudioManager.Instance.IsTrackedPlaying(goblin.ScreamHandle))
        {
            goblin.ScreamHandle = AudioManager.Instance.RequestTracked(scream, goblin.transform);
            if (goblin.ScreamHandle != 0)
                UI_ContextMessages.Instance?.StartScream(goblin);
        }

        StartScreamParticles(goblin);
    }

    private void StopScream(Horde_Goblin goblin)
    {
        UI_ContextMessages.Instance?.StopScream(goblin);
        StopScreamParticles(goblin);

        if (goblin.ScreamHandle == 0)
            return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.StopTracked(goblin.ScreamHandle);

        goblin.ScreamHandle = 0;
    }

    private void StartScreamParticles(Horde_Goblin goblin)
    {
        if (goblin.ScreamParticles != null || screamParticlesPrefab == null || maxScreamParticles <= 0)
            return;

        // All in use: the goblin that started screaming first gives its particles up
        if (freeScreamParticles.Count == 0 && screamingWithParticles.Count > 0)
            StopScreamParticles(screamingWithParticles[0]);

        if (freeScreamParticles.Count == 0)
            return;

        ParticleSystem particles = freeScreamParticles.Pop();
        Horde_GoblinType type = goblin.Type;
        Transform particleTransform = particles.transform;

        particleTransform.SetParent(goblin.transform, false);
        particleTransform.localPosition = type.mouthPosition;
        particleTransform.localRotation = Quaternion.Euler(type.mouthRotation);
        particles.gameObject.SetActive(true);
        particles.Play();

        goblin.ScreamParticles = particles;
        screamingWithParticles.Add(goblin);
    }

    private void StopScreamParticles(Horde_Goblin goblin)
    {
        ParticleSystem particles = goblin.ScreamParticles;

        if (particles == null)
            return;

        goblin.ScreamParticles = null;
        screamingWithParticles.Remove(goblin);

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.transform.SetParent(poolRoot, false);
        particles.gameObject.SetActive(false);
        freeScreamParticles.Push(particles);
    }

    // Throws a goblin: gravity on, flying at this velocity. It walks again once it's back down at Ground Height.
    // Physical (the default): real Rigidbody physics, launched like v1: the velocity plus one torque impulse round the
    // axis square to the throw (the type's Launch Torque per m/s), and no linear damping in the air. It flies, tumbles,
    // bounces and rolls, and once it has come to rest it gets up and walks on. Dead ones stay physical until they've
    // shrunk away. Not physical: rotation stays frozen and it falls straight (dropping into a hole); spin is only for
    // that (the old drawn tumble, degrees/second).
    public void Launch(Horde_Goblin goblin, Vector3 velocity, float spin = 0f, bool physical = true)
    {
        if (goblin == null || !goblin.IsActive)
            return;

        Rigidbody body = goblin.Rigidbody;
        SetWalking(goblin, false);

        if (!physical)
        {
            body.linearVelocity = velocity;
            groups[goblin.Type].SetAirborne(goblin.RenderIndex, true, spin);
            return;
        }

        goblin.Physical = true;
        goblin.AirTime = 0f;
        goblin.RestTime = 0f;
        body.constraints = RigidbodyConstraints.None;
        body.detectCollisions = true;

        if (airborneLayerIndex >= 0)
            goblin.gameObject.layer = airborneLayerIndex;

        body.linearDamping = 0f;
        body.angularDamping = goblin.Type.airAngularDamping;
        body.linearVelocity = velocity;
        body.angularVelocity = Vector3.zero;
        goblin.LastVerticalSpeed = velocity.y;
        goblin.Impacted = false;

        // v1's flick: tip it forward over the way it's thrown, harder the harder the throw
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 axis = flat.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, flat.normalized) : RandomFlat();
        body.AddTorque(axis * (velocity.magnitude * goblin.Type.launchTorque), ForceMode.Impulse);

        groups[goblin.Type].SetPhysical(goblin.RenderIndex);
    }

    // Drops a goblin into a hole: it stops walking, falls over the centre (mouth at ground height) and goes back to the
    // pool once below bottomY. Out of the way while it falls: no collisions, no damage.
    // Drops a goblin into a hole: a little hop from the mouth to 'target' (a spot in the hole, given by the hole so
    // they don't all land on the middle), kept over it as it falls, gone below bottomY
    public void FallInto(Horde_Goblin goblin, Vector3 target, float bottomY)
    {
        if (goblin == null || !goblin.IsActive || goblin.Dead || goblin.FallingIn)
            return;

        goblin.FallingIn = true;
        goblin.FallCentre = target;
        goblin.FallBottom = bottomY;
        goblin.Rigidbody.detectCollisions = false;

        // A little hop up and over the lip, then gravity takes it down, upright
        Vector3 toTarget = target - goblin.Rigidbody.position;
        Launch(goblin, new Vector3(toTarget.x * 4f, 2f, toTarget.z * 4f), 0f, false);
    }

    // Walking: held at the ground, no gravity. Airborne: free, with gravity, until it lands.
    private static void SetWalking(Horde_Goblin goblin, bool walking)
    {
        Rigidbody body = goblin.Rigidbody;
        goblin.Airborne = !walking;
        body.useGravity = !walking;
        body.constraints = walking ? Walking : RigidbodyConstraints.FreezeRotation;
    }

    // Back to the pool. Safe to call from triggers and from inside the movement step.
    public void Despawn(Horde_Goblin goblin)
    {
        if (goblin == null || !goblin.IsActive)
            return;

        // Despawned before its shrink ended (Despawn All): forget its pending return, so it can't despawn a later life
        if (goblin.Dead)
        {
            for (int i = dying.Count - 1; i >= 0; i--)
            {
                if (dying[i].Goblin != goblin)
                    continue;

                if (dying[i].Corpse)
                    ForgetCorpse(dying[i].Cell);

                dying[i] = dying[dying.Count - 1];
                dying.RemoveAt(dying.Count - 1);
            }
        }

        StopScream(goblin);

        if (goblin.Physical)
        {
            goblin.Physical = false;
            goblin.gameObject.layer = goblin.WalkingLayer;
            goblin.Rigidbody.linearDamping = goblin.WalkingDamping;
        }

        goblin.Settled = false;
        goblin.Doomed = false;
        goblin.Impacted = false;
        goblin.Rigidbody.isKinematic = false;
        goblin.InPool = true;
        goblin.Dead = false;
        goblin.FallingIn = false;
        goblin.Emerging = false;
        goblin.Destination = null;
        goblin.Rigidbody.detectCollisions = true;
        Unregister(goblin);
        groups[goblin.Type].Remove(goblin);

        goblin.Rigidbody.linearVelocity = Vector3.zero;
        goblin.Rigidbody.angularVelocity = Vector3.zero;
        goblin.gameObject.SetActive(false);
        PoolFor(goblin.Type).Push(goblin);

        RefreshCounts();
    }

    [InspectorButton("Despawn All", true)]
    public void DespawnAll()
    {
        for (int i = Count - 1; i >= 0; i--)
            Despawn(items[i]);
    }

    private Horde_Goblin Create(Horde_GoblinType type)
    {
        GameObject instance = Instantiate(type.prefab, poolRoot, false);
        instance.name = type.prefab.name;
        instance.SetActive(false);

        if (!instance.TryGetComponent(out Horde_Goblin goblin))
            goblin = instance.AddComponent<Horde_Goblin>();

        goblin.Create(type);
        created++;
        return goblin;
    }

    private Stack<Horde_Goblin> PoolFor(Horde_GoblinType type)
    {
        if (!pools.TryGetValue(type, out Stack<Horde_Goblin> pool))
        {
            pool = new Stack<Horde_Goblin>(Mathf.Max(16, type.poolSize));
            pools.Add(type, pool);

            // The type's prefab is read into parts once, alongside its pool
            Horde_RenderGroup group = new Horde_RenderGroup(type, renderBounds);
            groups.Add(type, group);
            groupList.Add(group);
        }

        return pool;
    }

    // Scale of a goblin's body and head, on top of its prefab's. The head stays on the neck; things on the head
    // scale with it.
    public void SetScales(Horde_Goblin goblin, Vector3 bodyScale, Vector3 headScale)
    {
        if (goblin == null || !goblin.IsActive || !groups.TryGetValue(goblin.Type, out Horde_RenderGroup group))
            return;

        group.SetScales(goblin.RenderIndex, bodyScale, headScale);
    }

    // The render group drawing a type's goblins, for the animator to write poses into (by RenderIndex)
    public Horde_RenderGroup GroupFor(Horde_GoblinType type)
    {
        return type != null && groups.TryGetValue(type, out Horde_RenderGroup group) ? group : null;
    }

    private int CountActive(Horde_GoblinType type)
    {
        int count = 0;

        for (int i = 0; i < Count; i++)
        {
            if (items[i].Type == type)
                count++;
        }

        return count;
    }

    private bool IsUsable(Horde_GoblinType type)
    {
        if (type != null && type.prefab != null)
            return true;

        Debug.LogError($"{name}: a Goblin Type with no prefab can't be spawned.", type);
        return false;
    }

    private void RefreshCounts()
    {
        active = Count;
        peakActive = Mathf.Max(peakActive, active);
        pooled = 0;

        foreach (Stack<Horde_Goblin> pool in pools.Values)
            pooled += pool.Count;
    }
    #endregion

    #region Registry Data
    protected override void OnRegistered(Horde_Goblin goblin, int index)
    {
        fieldOffsets.Add(-1);
        maxSpeeds.Add(0f);
        steers.Add(new float2(1f, 0f));
        healths.Add(0f);
    }

    protected override void OnRemoved(Horde_Goblin removed, int index, int lastIndex)
    {
        fieldOffsets.RemoveAtSwapBack(index);
        maxSpeeds.RemoveAtSwapBack(index);
        steers.RemoveAtSwapBack(index);
        healths.RemoveAtSwapBack(index);
    }
    #endregion

    #region Rendering
    // Every type's matrices are scheduled first so they build side by side, then each is drawn
    private void Render()
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();

        // Finished shrinking: back to the pool before this frame's draw
        float now = Time.time;
        bool keepCorpses = Horde_DebrisBudget.CleanupPaused;

        if (!keepCorpses)
        {
            RetireCorpses(now);
            ThinCorpses(now);
        }

        for (int i = dying.Count - 1; i >= 0; i--)
        {
            if (now < dying[i].End || (keepCorpses && dying[i].Corpse))
                continue;

            Horde_Goblin goblin = dying[i].Goblin;

            if (dying[i].Corpse)
                ForgetCorpse(dying[i].Cell);

            dying[i] = dying[dying.Count - 1];
            dying.RemoveAt(dying.Count - 1);
            Despawn(goblin);
        }

        for (int i = 0; i < groupList.Count; i++)
            groupList[i].Schedule();

        JobHandle.ScheduleBatchedJobs();
        int calls = 0;

        for (int i = 0; i < groupList.Count; i++)
            calls += groupList[i].Draw();

        drawCalls = calls;
        renderMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);

        // The profiler's total for the last finished frame (nanoseconds), and how many steps it was
        if (simulateRecorder.Valid && simulateRecorder.Count > 0)
        {
            ProfilerRecorderSample sample = simulateRecorder.GetSample(simulateRecorder.Count - 1);
            physicsSimulateMs = (float)(sample.Value * 1e-6);
            physicsSteps = (int)sample.Count;
        }
    }
    #endregion

    #region Movement
    private void Move()
    {
        if (Count == 0)
            return;

        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        float deltaTime = Time.fixedDeltaTime;
        Flow_System flow = Flow_System.Instance;
        bool hasFlow = flow != null && flow.IsReady;
        NativeArray<byte> directions = hasFlow ? flow.Directions : default;
        Flow_Grid grid = hasFlow ? flow.Grid : default;
        bool stampWear = Level_GrassWearSystem.BeginStep(Count,Time.fixedTime);

        BeginIteration();

        try
        {
            for (int i = 0; i < Count; i++)
            {
                Horde_Goblin goblin = items[i];

                // Despawned by something during this step; it leaves the list when the step ends
                if (goblin.InPool)
                    continue;

                Horde_GoblinType type = goblin.Type;
                Rigidbody body = goblin.Rigidbody;
                Vector3 position = body.position;
                if (goblin.Dead || goblin.Airborne || goblin.Emerging || goblin.FallingIn) goblin.WearSampleValid = false;

                // Thrown: physics flies it until it's back down at the ground, then it walks again
                // Hopping out of a hole: the body waits, untouchable, until the hop lands
                if (goblin.Emerging)
                {
                    if (Time.time < goblin.EmergeEnd)
                        continue;

                    goblin.Emerging = false;
                    body.detectCollisions = true;
                }

                // Dropping into a hole: kept over its landing spot as it falls, gone once it's below the bottom
                if (goblin.FallingIn)
                {
                    if (position.y < goblin.FallBottom)
                    {
                        Despawn(goblin);
                        continue;
                    }

                    Vector3 fall = body.linearVelocity;
                    Vector3 toTarget = goblin.FallCentre - position;
                    body.linearVelocity = new Vector3(toTarget.x * 6f, fall.y, toTarget.z * 6f);
                    continue;
                }

                // Killed: no more walking, it stays where it fell (or flies, if knocked into the air) while it shrinks
                // Killed on the ground: kinematic, lying where it fell until it's cleared
                if (goblin.Dead && !goblin.Physical)
                {
                    if (goblin.SupportWearRevision != Level_GrassWearSystem.Revision)
                    {
                        float support = Level_GroundSurface.HeightAt(position,groundHeight);
                        body.position = position + Vector3.up*(support-goblin.SupportHeight);
                        goblin.SupportHeight = support; goblin.SupportWearRevision = Level_GrassWearSystem.Revision;
                    }
                    continue;
                }

                if (goblin.Airborne)
                {
                    // Flung: the scream fades as it comes down
                    if (goblin.ScreamHandle != 0 && AudioManager.Instance != null)
                    {
                        Horde_GoblinAudio flung = goblin.Type.audio;
                        float height = Mathf.Clamp01((position.y - groundHeight) / flung.screamFullHeight);
                        AudioManager.Instance.SetTrackedVolume(goblin.ScreamHandle, Mathf.Lerp(flung.screamMinVolume, 1f, height));
                    }

                    // Thrown with physics: once it has come to rest it gets up, or if it died up there, lies still and
                    // starts to go
                    if (goblin.Physical)
                    {
                        // Hitting the ground: gravity only ever pulls its vertical speed down, so a sudden jump up from
                        // falling means it just struck something below it
                        if (!goblin.Impacted && !goblin.Settled)
                        {
                            Vector3 flying = body.linearVelocity;
                            float before = goblin.LastVerticalSpeed;

                            if (before < -1f && flying.y > before + impactSpeedJump)
                                Impact(goblin, -before, flying);

                            goblin.LastVerticalSpeed = flying.y;
                        }

                        if (!goblin.Settled && HasComeToRest(goblin, body, deltaTime))
                        {
                            if (goblin.Dead)
                                SettleDead(goblin, position);
                            else
                                GetUp(goblin, position);
                        }

                        continue;
                    }

                    if (position.y <= Level_GroundSurface.HeightAt(position, groundHeight) && body.linearVelocity.y <= 0f)
                        Land(goblin, position);

                    continue;
                }

                // Walking stays on the shallow baked surface without a raycast per goblin.
                float surfaceHeight = Level_GroundSurface.HeightAt(position, groundHeight);
                if (Mathf.Abs(position.y - surfaceHeight) > 0.002f)
                {
                    position.y = surfaceHeight;
                    body.position = position;
                }
                goblin.SupportHeight = surfaceHeight; goblin.SupportWearRevision = Level_GrassWearSystem.Revision;

                // The way the field says to go here, bent by this goblin's own steering offset
                float2 flowDirection = float2.zero;
                int offset = fieldOffsets[i];

                if (hasFlow && offset >= 0)
                {
                    int cell = grid.CellIndex(position);

                    if (cell >= 0)
                        flowDirection = Flow_Direction.Decode(directions[offset + cell]);
                }

                float2 steer = steers[i];
                float2 heading = new float2(
                    flowDirection.x * steer.x - flowDirection.y * steer.y,
                    flowDirection.x * steer.y + flowDirection.y * steer.x);

                // Velocity toward the wanted velocity, changed by at most the acceleration. Set directly: one call
                // instead of a force, and walking goblins have no vertical speed anyway.
                Vector3 velocity = body.linearVelocity;
                Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
                Vector3 change = new Vector3(heading.x, 0f, heading.y) * maxSpeeds[i] - flat;
                if (stampWear && Level_GrassWearSystem.ShouldSample(i) && flat.sqrMagnitude > 0.04f)
                {
                    Level_GrassWearSystem.Stamp(i,goblin.WearSampleValid ? goblin.WearPosition : position,position);
                    goblin.WearPosition = position; goblin.WearSampleValid = true;
                }
                float maxChange = type.acceleration * deltaTime;

                if (change.sqrMagnitude > maxChange * maxChange)
                    change = change.normalized * maxChange;

                if (change.sqrMagnitude > 0.000001f)
                    body.linearVelocity = flat + change;
            }
        }
        finally
        {
            try { if (stampWear) Level_GrassWearSystem.EndStep(); }
            finally { EndIteration(); }
        }

        movementMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
    }

    // Slow and barely spinning for Rest Time (after a moment in the air), or out of time
    private bool HasComeToRest(Horde_Goblin goblin, Rigidbody body, float deltaTime)
    {
        goblin.AirTime += deltaTime;

        if (goblin.AirTime >= maxAirTime)
            return true;

        float spin = restSpin * Mathf.Deg2Rad;
        bool still = goblin.AirTime > 0.25f
                     && body.linearVelocity.sqrMagnitude < restSpeed * restSpeed
                     && body.angularVelocity.sqrMagnitude < spin * spin;

        goblin.RestTime = still ? goblin.RestTime + deltaTime : 0f;
        return goblin.RestTime >= restTime;
    }

    // Killed in the air and now at rest: frozen where it lies (no physics, no collisions), then the delay and the
    // shrink. Down a hole, it's simply removed.
    private void SettleDead(Horde_Goblin goblin, Vector3 position)
    {
        if (position.y < Level_GroundSurface.HeightAt(position, groundHeight) - lostBelow)
        {
            Despawn(goblin);
            return;
        }

        Horde_GoblinType type = goblin.Type;
        Rigidbody body = goblin.Rigidbody;
        body.detectCollisions = false;
        body.isKinematic = true;
        goblin.Settled = true;
        float support = Level_GroundSurface.HeightAt(position,groundHeight);
        float collisionSupport = Level_GroundSurface.HeightAt(position,groundHeight,false);
        body.position = position + Vector3.up*Mathf.Min(0,support-collisionSupport);
        goblin.SupportHeight = support; goblin.SupportWearRevision = Level_GrassWearSystem.Revision;

        float now = Time.time;
        AddCorpse(goblin, groups[type], now, Vector3.forward, false, false);
    }

    // A body lying there: it shrinks after the budget's Corpse Lifetime, or (0) when Max Corpses needs the room
    private void AddCorpse(Horde_Goblin goblin, Horde_RenderGroup group, float now, Vector3 direction, bool tip, bool flash)
    {
        Horde_GoblinType type = goblin.Type;
        float lifetime = Horde_DebrisBudget.Current.corpseLifetime;
        bool timed = lifetime > 0f;

        group.Kill(goblin.RenderIndex, now, direction, timed ? lifetime : NoShrink, type.deathDuration, tip, flash);
        dying.Add(new Dying
        {
            Goblin = goblin,
            Born = now,
            Corpse = true,
            Cell = CorpseGrid.CellAt(goblin.transform.position),
            End = timed ? now + lifetime + type.deathDuration : float.PositiveInfinity
        });
        corpseCount++;
        CorpseGrid.Add(dying[dying.Count - 1].Cell);
    }

    private Horde_CrowdGrid CorpseGrid => corpseCrowd ??= new Horde_CrowdGrid(Horde_DebrisBudget.Current.crowdCell);

    // No longer waiting its turn
    private void ForgetCorpse(int cell)
    {
        corpseCount--;
        corpseCrowd.Remove(cell);
    }

    private void RetireCorpse(int index, float now)
    {
        Dying corpse = dying[index];
        Horde_GoblinType type = corpse.Goblin.Type;
        groups[type].StartShrink(corpse.Goblin.RenderIndex, now);
        ForgetCorpse(corpse.Cell);
        corpse.Corpse = false;
        corpse.End = now + type.deathDuration;
        dying[index] = corpse;
    }

    // Packed squares lose their oldest corpses, a few at a time, and never fresh ones
    private void ThinCorpses(float now)
    {
        Horde_DebrisBudget budget = Horde_DebrisBudget.Current;

        if (budget.maxCorpsesPerCell <= 0 || corpseCount == 0)
            return;

        corpseThinAllowance = Mathf.Min(corpseThinAllowance + budget.thinPerSecond * Time.deltaTime, budget.thinPerSecond);

        while (corpseThinAllowance >= 1f)
        {
            int oldest = -1;

            for (int i = 0; i < dying.Count; i++)
            {
                Dying corpse = dying[i];

                if (corpse.Corpse && now - corpse.Born >= budget.thinAfter && corpseCrowd.Count(corpse.Cell) > budget.maxCorpsesPerCell
                    && (oldest < 0 || corpse.Born < dying[oldest].Born))
                    oldest = i;
            }

            if (oldest < 0)
                return;

            RetireCorpse(oldest, now);
            corpseThinAllowance -= 1f;
        }
    }

    private const float NoShrink = 1000000f;

    // Over Max Corpses: the oldest start shrinking
    private void RetireCorpses(float now)
    {
        int limit = Horde_DebrisBudget.Current.MaxCorpses;

        while (corpseCount > limit)
        {
            int oldest = -1;

            for (int i = 0; i < dying.Count; i++)
            {
                if (dying[i].Corpse && (oldest < 0 || dying[i].Born < dying[oldest].Born))
                    oldest = i;
            }

            if (oldest < 0)
                return;

            RetireCorpse(oldest, now);
        }
    }

    // At rest after a throw: back to walking physics, standing where it lies and facing the way it lay, and the render
    // group eases it up from how it lay. Down a hole, it's removed instead.
    private void GetUp(Horde_Goblin goblin, Vector3 position)
    {
        if (position.y < Level_GroundSurface.HeightAt(position, groundHeight) - lostBelow)
        {
            Despawn(goblin);
            return;
        }

        Rigidbody body = goblin.Rigidbody;
        Quaternion lying = body.rotation;

        // Its new facing: where its front points, or failing that (lying flat on its back) where its head points
        Vector3 front = lying * Vector3.forward;
        front.y = 0f;

        if (front.sqrMagnitude < 0.01f)
        {
            front = lying * Vector3.up;
            front.y = 0f;
        }

        float yaw = front.sqrMagnitude > 0.0001f ? Mathf.Atan2(front.x, front.z) * Mathf.Rad2Deg : 0f;

        goblin.Physical = false;
        goblin.Doomed = false;
        goblin.gameObject.layer = goblin.WalkingLayer;
        body.linearDamping = goblin.WalkingDamping;
        position.y = Level_GroundSurface.HeightAt(position, groundHeight);
        body.position = position;
        goblin.SupportHeight = position.y; goblin.SupportWearRevision = Level_GrassWearSystem.Revision;
        SetWalking(goblin, true);
        body.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        StopScream(goblin);

        groups[goblin.Type].GetUp(goblin.RenderIndex, lying, yaw, Time.time, getUpDuration);
    }

    // Back on the ground: snapped to its height and walking again, keeping its sideways speed
    private void Land(Horde_Goblin goblin, Vector3 position)
    {
        Rigidbody body = goblin.Rigidbody;
        Vector3 velocity = body.linearVelocity;

        position.y = Level_GroundSurface.HeightAt(position, groundHeight);
        body.position = position;
        SetWalking(goblin, true);
        body.linearVelocity = new Vector3(velocity.x, 0f, velocity.z);
        StopScream(goblin);

        // The squish and the pop upright, harder the faster it came down
        groups[goblin.Type].Landed(goblin.RenderIndex, -velocity.y);
    }
    #endregion
}
