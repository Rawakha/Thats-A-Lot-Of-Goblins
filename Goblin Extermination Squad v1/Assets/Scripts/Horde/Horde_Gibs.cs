using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

// Goblins coming apart: the flying pieces (Horde_GibSet) that replace them when they die. On every kill, Head Pop Chance
// decides the head comes off whole, X eyes and all, and the body topples without it; otherwise a scrap or two is torn out
// of the body. A goblin blown apart by a blast disappears entirely and bursts into all its chunks, up and away.
//
// Pieces are meaty, not debris: they thud with hardly any bounce, squish flat on the first impact and spring back, skid
// a little and leave a blood splat (Effect_BloodSplats). They stay until Horde_DebrisBudget's limit needs the room (or
// its Gib Lifetime), then the oldest spring down to nothing first.
// Once down, a piece ages over Age Time: it dims and greys, sinks and flattens, so bright means still flying and old
// fights turn into texture instead of looking like living goblins.
// Each piece takes its goblin's skin colour and size, and starts exactly where that part of the goblin was.
//
// Built for hordes: no GameObjects or Rigidbodies. A fixed pool simulated here (gravity against the flat floor, so
// pieces pass through cliffs) and drawn GPU instanced per chunk mesh. A piece that has stopped moving keeps its draw
// data and costs almost nothing, so thousands can lie around. Max Per Frame thins a huge blast to its biggest pieces.
//
// Setup: put this in the scene, assign a baked Horde_GibSet and a material on "Goblins/Gib".
//
// GC: none per kill or per frame (fixed arrays, allocated at start; a mesh's settled pieces take a new draw chunk each
// time they pass another 1023). One UpdateManager subscription (OnLateUpdate) and one to Horde_System.Killed.
public class Horde_Gibs : MonoBehaviour
{
    private enum State : byte
    {
        Free,
        Flying,
        Grounded,
        Settled,    // stopped: its draw data is kept in a StaticChunk
        Leaving
    }

    private struct Gib
    {
        public State State;
        public int Mesh;
        public Vector3 Position;
        public Vector3 Velocity;
        public Quaternion Rotation;
        public Vector3 Spin;        // axis x degrees per second
        public Vector3 Scale;
        public Vector4 Colour;
        public float Seed;
        public float Radius;        // how far its middle rests above the floor
        public float Born;
        public Core_SpringFloatState Squash;
        public Core_SpringFloatState Size;
        public int StaticSlot;
        public float Age;           // 0 just landed, 1 fully aged
        public int Older;           // age order: neighbours in the list of pieces staying, -1 at the ends
        public int Newer;
        public int Cell;            // its square of ground, once Counted (it has landed)
        public bool Counted;
    }

    // Settled pieces of one mesh, ready to draw: only touched when one arrives or leaves
    private sealed class StaticChunk
    {
        public readonly Matrix4x4[] Matrices = new Matrix4x4[Batch];
        public readonly Vector4[] Colours = new Vector4[Batch];
        public readonly float[] Seeds = new float[Batch];
        public readonly float[] Wobbles = new float[Batch];
        public readonly int[] Owners = new int[Batch];
        public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        public bool Dirty;
    }

    // One chunk mesh's instances this frame, flushed to a draw when full
    private sealed class Bucket
    {
        public Mesh Mesh;
        public readonly Matrix4x4[] Matrices = new Matrix4x4[Batch];
        public readonly Vector4[] Colours = new Vector4[Batch];
        public readonly float[] Seeds = new float[Batch];
        public readonly float[] Wobbles = new float[Batch];
        public int Count;
        public readonly List<StaticChunk> Chunks = new List<StaticChunk>();
        public int StaticCount;
    }

    [SerializeField] private Horde_GibSet set;
    [SerializeField] private Material material;

    [Tooltip("Slots kept free: the oldest start springing away this far before Max Gibs.")]
    [SerializeField, Min(1)] private int roomMargin = 100;

    [Tooltip("Most pieces started in one frame. Past half of this, blown-apart goblins throw only their biggest pieces.")]
    [SerializeField, Min(1)] private int maxPerFrame = 200;

    [Tooltip("Height of the ground. Combat_Projectiles' Floor Height is used instead when there is one.")]
    [SerializeField] private float floorHeight;

    [Header("Every Kill")]
    [Tooltip("Chance (0-1) the head comes off whole, on every kill.")]
    [SerializeField, Range(0f, 1f)] private float headPopChance = 0.5f;

    [Tooltip("Speed a popped head leaves at, m/s (random between).")]
    [SerializeField] private Vector2 headSpeed = new Vector2(5f, 9f);

    [Tooltip("How much a popped head goes up rather than along the hit (1 is 45 degrees).")]
    [SerializeField, Min(0f)] private float headLift = 1.2f;

    [Tooltip("Scraps torn from the body when the head stays on (random between).")]
    [SerializeField] private Vector2Int scraps = new Vector2Int(1, 2);

    [Tooltip("Speed scraps leave at, m/s (random between).")]
    [SerializeField] private Vector2 scrapSpeed = new Vector2(4f, 8f);

    [Tooltip("How much scraps go up rather than along the hit.")]
    [SerializeField, Min(0f)] private float scrapLift = 0.7f;

    [Header("Blown Apart")]
    [Tooltip("Speed pieces leave a blast at, m/s (random between).")]
    [SerializeField] private Vector2 blastSpeed = new Vector2(7f, 14f);

    [Tooltip("How much they go up rather than away from the blast.")]
    [SerializeField, Min(0f)] private float blastLift = 1.3f;

    [Tooltip("How much each piece also flies out from the goblin's own middle, so it bursts rather than moving as one.")]
    [SerializeField, Min(0f)] private float blastBurst = 1f;

    [Header("Flight")]
    [SerializeField, Min(0f)] private float gravity = 26f;

    [Tooltip("Tumbling speed in the air, degrees per second (random between).")]
    [SerializeField] private Vector2 spin = new Vector2(180f, 720f);

    [Tooltip("Random change in each piece's direction, as a share of its speed.")]
    [SerializeField, Range(0f, 1f)] private float scatter = 0.3f;

    [Header("Landing")]
    [Tooltip("Share of its falling speed a piece bounces back with. Low is a meaty thud.")]
    [SerializeField, Range(0f, 1f)] private float bounce = 0.15f;

    [Tooltip("A bounce slower than this just stays down, m/s.")]
    [SerializeField, Min(0f)] private float settleSpeed = 2f;

    [Tooltip("Share of its sideways speed kept on the first impact.")]
    [SerializeField, Range(0f, 1f)] private float impactKeep = 0.5f;

    [Tooltip("How quickly a grounded piece stops skidding and spinning, per second.")]
    [SerializeField, Min(0f)] private float friction = 6f;

    [Tooltip("How hard landing squishes it: squash speed per m/s of impact.")]
    [SerializeField, Min(0f)] private float squishKick = 0.5f;

    [Tooltip("The flattest it gets (0.6 is 60% flatter), however hard it landed.")]
    [SerializeField, Range(0f, 0.9f)] private float maxSquish = 0.6f;

    [Tooltip("How the squish springs back. Low damper jiggles.")]
    [SerializeField] private Core_SpringSettings squishSpring = new Core_SpringSettings(220f, 9f);

    [Tooltip("Splat under a piece's first impact, as a multiple of its size (diameter). 0 is none.")]
    [SerializeField, Min(0f)] private float splatScale = 3f;

    [Tooltip("Most landing splats per frame.")]
    [SerializeField, Min(0)] private int maxSplatsPerFrame = 30;

    [Header("Ageing")]
    [Tooltip("Seconds a piece on the ground takes to dim, sink and flatten. It only becomes cheap to draw after this.")]
    [SerializeField, Min(0.01f)] private float ageTime = 2f;

    [Tooltip("Brightness when fully aged, as a share of its colour.")]
    [SerializeField, Range(0f, 1f)] private float agedBrightness = 0.45f;

    [Tooltip("How much colour it loses: 0 keeps it, 1 is fully grey.")]
    [SerializeField, Range(0f, 1f)] private float agedGrey = 0.55f;

    [Tooltip("How far it sinks into the ground, as a share of its radius.")]
    [SerializeField, Range(0f, 1f)] private float agedSink = 0.35f;

    [Tooltip("How flat it ends up (0.4 is 40% flatter).")]
    [SerializeField, Range(0f, 0.9f)] private float agedFlatten = 0.4f;

    [Header("Leaving")]
    [Tooltip("How it scales down when it goes.")]
    [SerializeField] private Core_SpringSettings leaveSpring = new Core_SpringSettings(120f, 14f);

    private const int Batch = 1023;
    private static readonly int SkinId = Shader.PropertyToID("_SkinColor");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int WobbleId = Shader.PropertyToID("_WobbleAmount");

    public static Horde_Gibs Instance { get; private set; }

    // Pieces staying (not springing away)
    public int Count => staying;

    // From Horde_DebrisBudget
    private int maxGibs;
    private float lifetime;

    private Gib[] gibs;
    private Bucket[] buckets;
    private int bodyStart;
    private int headStart;
    private int wholeHead = -1;
    private int oldest = -1;    // ends of the age-ordered list
    private int newest = -1;
    private int staying;
    private int[] free;
    private int freeCount;
    private Horde_CrowdGrid crowd;
    private int maxPerCell;
    private float thinAfter;
    private float thinPerSecond;
    private float thinAllowance;
    private int startedThisFrame;
    private float floor;
    private Horde_System horde;
    private RenderParams renderParams;
    private MaterialPropertyBlock properties;

    private void Awake()
    {
        if (set == null || material == null || set.bodyChunks.Length == 0 || set.wholeHead == null)
        {
            Debug.LogError($"{name}: Horde_Gibs needs a baked Gib Set and a material on Goblins/Gib.", this);
            enabled = false;
            return;
        }

        material.enableInstancing = true;
        Instance = this;
        maxGibs = Horde_DebrisBudget.Current.MaxGibs;
        lifetime = Horde_DebrisBudget.Current.gibLifetime;
        gibs = new Gib[maxGibs];
        free = new int[maxGibs];

        for (int i = 0; i < maxGibs; i++)
            free[i] = maxGibs - 1 - i;

        freeCount = maxGibs;

        Horde_DebrisBudget budget = Horde_DebrisBudget.Current;
        crowd = new Horde_CrowdGrid(budget.crowdCell);
        maxPerCell = budget.maxGibsPerCell;
        thinAfter = budget.thinAfter;
        thinPerSecond = budget.thinPerSecond;
        properties = new MaterialPropertyBlock();

        bodyStart = 0;
        headStart = set.bodyChunks.Length;
        wholeHead = headStart + set.headChunks.Length;
        buckets = new Bucket[wholeHead + 1];

        for (int i = 0; i < buckets.Length; i++)
        {
            Mesh mesh = i < headStart ? set.bodyChunks[i].mesh : i < wholeHead ? set.headChunks[i - headStart].mesh : set.wholeHead;
            buckets[i] = new Bucket { Mesh = mesh };
        }

        renderParams = new RenderParams(material)
        {
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000f),
            matProps = properties
        };
    }

    private void Start()
    {
        floor = Combat_Projectiles.Instance != null ? Combat_Projectiles.Instance.FloorHeight : floorHeight;
        horde = Horde_System.Instance;

        if (horde != null)
            horde.Killed += OnKilled;
        else
            Debug.LogWarning($"{name}: No Horde_System, so no gibs.", this);
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate += Tick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;
    }

#if UNITY_EDITOR
    // Selected, in play: pieces in squares over the limit, red
    private void OnDrawGizmosSelected()
    {
        if (gibs == null || maxPerCell <= 0)
            return;

        Gizmos.color = Color.red;

        for (int slot = oldest; slot >= 0; slot = gibs[slot].Newer)
        {
            if (gibs[slot].Counted && crowd.Count(gibs[slot].Cell) > maxPerCell)
                Gizmos.DrawWireSphere(gibs[slot].Position, 0.3f);
        }
    }
#endif

    private void OnDestroy()
    {
        if (horde != null)
            horde.Killed -= OnKilled;

        if (Instance == this)
            Instance = null;
    }

    private void OnKilled(Horde_Goblin goblin)
    {
        Horde_RenderGroup group = horde.GroupFor(goblin.Type);

        if (group == null)
            return;

        int index = goblin.RenderIndex;
        group.GetAnchors(index, out float4x4 body, out float4x4 head);
        Vector4 colour = (Vector4)group.ColourAt(index);
        Vector3 pushed = goblin.DeathDirection;
        bool popHead = Random.value < headPopChance;

        if (goblin.DeathBurst)
        {
            BlowApart(group, index, body, head, colour, pushed, popHead);
            return;
        }

        if (popHead)
        {
            group.HideHead(index);
            goblin.DiedInPieces = true;
            Throw(wholeHead, set.wholeHeadCentre, head, colour, pushed + Vector3.up * headLift, Random.Range(headSpeed.x, headSpeed.y));
            return;
        }

        // Scraps: the smaller chunks (the list is biggest first); the body itself stays and topples
        int count = Random.Range(scraps.x, scraps.y + 1);

        for (int i = 0; i < count; i++)
        {
            int chunk = set.bodyChunks.Length > 1 ? Random.Range(1, set.bodyChunks.Length) : 0;
            Throw(bodyStart + chunk, set.bodyChunks[chunk].centre, body, colour, pushed + Vector3.up * scrapLift, Random.Range(scrapSpeed.x, scrapSpeed.y));
        }
    }

    // The whole goblin gone, every piece thrown up and away. A busy frame throws only the biggest.
    private void BlowApart(Horde_RenderGroup group, int index, in float4x4 body, in float4x4 head, Vector4 colour, Vector3 away, bool popHead)
    {
        group.HideAll(index);

        bool busy = startedThisFrame > maxPerFrame / 2;
        int bodyCount = busy ? Mathf.Min(2, set.bodyChunks.Length) : set.bodyChunks.Length;
        Vector3 middle = new Vector3(body.c3.x, body.c3.y, body.c3.z);

        for (int i = 0; i < bodyCount; i++)
            ThrowBurst(bodyStart + i, set.bodyChunks[i].centre, body, colour, away, middle);

        if (popHead || busy || set.headChunks.Length == 0)
        {
            ThrowBurst(wholeHead, set.wholeHeadCentre, head, colour, away, middle);
            return;
        }

        for (int i = 0; i < set.headChunks.Length; i++)
            ThrowBurst(headStart + i, set.headChunks[i].centre, head, colour, away, middle);
    }

    private void ThrowBurst(int mesh, Vector3 centre, in float4x4 anchor, Vector4 colour, Vector3 away, Vector3 middle)
    {
        Vector3 at = ToVector(math.transform(anchor, centre));
        Vector3 outward = (at - middle).normalized * blastBurst;
        Throw(mesh, centre, anchor, colour, away + outward + Vector3.up * blastLift, Random.Range(blastSpeed.x, blastSpeed.y));
    }

    // Starts one piece where that part of the goblin was, in its pose and size
    private void Throw(int mesh, Vector3 centre, in float4x4 anchor, Vector4 colour, Vector3 direction, float speed)
    {
        if (startedThisFrame >= maxPerFrame)
            return;

        startedThisFrame++;

        int slot;

        if (freeCount > 0)
        {
            slot = free[--freeCount];
        }
        else if (oldest >= 0)
        {
            // A flood used up the room before anything could leave: the oldest is taken over
            slot = oldest;
            Unlink(slot);
        }
        else
        {
            return;     // every slot is springing away
        }

        staying++;

        Vector3 column0 = new Vector3(anchor.c0.x, anchor.c0.y, anchor.c0.z);
        Vector3 column1 = new Vector3(anchor.c1.x, anchor.c1.y, anchor.c1.z);
        Vector3 column2 = new Vector3(anchor.c2.x, anchor.c2.y, anchor.c2.z);
        Vector3 scale = new Vector3(column0.magnitude, column1.magnitude, column2.magnitude);
        Quaternion rotation = scale.x > 0f && scale.y > 0f && scale.z > 0f
            ? Quaternion.LookRotation(column2 / scale.z, column1 / scale.y)
            : Quaternion.identity;

        Vector3 aim = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
        Vector3 size = buckets[mesh].Mesh.bounds.size;

        gibs[slot] = new Gib
        {
            State = State.Flying,
            Mesh = mesh,
            Position = ToVector(math.transform(anchor, centre)),
            Velocity = (aim + Random.insideUnitSphere * scatter) * speed,
            Rotation = rotation,
            Spin = Random.onUnitSphere * Random.Range(spin.x, spin.y),
            Scale = scale,
            Colour = colour,
            Seed = Random.value,
            Radius = (size.x * scale.x + size.y * scale.y + size.z * scale.z) / 6f,
            Born = Time.time,
            Size = new Core_SpringFloatState(1f),
            Older = newest,
            Newer = -1
        };

        if (newest >= 0)
            gibs[newest].Newer = slot;
        else
            oldest = slot;

        newest = slot;
    }

    private static Vector3 ToVector(float3 value) => new Vector3(value.x, value.y, value.z);

    private void Tick()
    {
        float deltaTime = Time.deltaTime;
        startedThisFrame = 0;

        if (deltaTime > 0f)
        {
            if (!Horde_DebrisBudget.CleanupPaused)
            {
                RetireOldest(Time.time);
                ThinPacked(Time.time, deltaTime);
            }

            Simulate(deltaTime);
        }

        Draw();
    }

    // The oldest start leaving once they've had their time, or to keep room free
    private void RetireOldest(float now)
    {
        int most = maxGibs - Mathf.Min(roomMargin, maxGibs / 2);

        while (oldest >= 0 && (staying > most || (lifetime > 0f && now - gibs[oldest].Born >= lifetime)))
        {
            int slot = oldest;
            Unlink(slot);
            gibs[slot].State = State.Leaving;
        }
    }

    // Packed squares lose their oldest pieces, oldest first across the map, a few at a time
    private void ThinPacked(float now, float deltaTime)
    {
        if (maxPerCell <= 0)
            return;

        thinAllowance = Mathf.Min(thinAllowance + thinPerSecond * deltaTime, thinPerSecond);

        for (int slot = oldest; slot >= 0 && thinAllowance >= 1f;)
        {
            ref Gib gib = ref gibs[slot];
            int newer = gib.Newer;

            // Everything after this is younger still
            if (now - gib.Born < thinAfter)
                break;

            if (gib.Counted && crowd.Count(gib.Cell) > maxPerCell)
            {
                Unlink(slot);
                gib.State = State.Leaving;
                thinAllowance -= 1f;
            }

            slot = newer;
        }
    }

    // Takes a piece out of the staying list and everything that counts it (its state is the caller's to set)
    private void Unlink(int slot)
    {
        ref Gib gib = ref gibs[slot];

        if (gib.Older >= 0)
            gibs[gib.Older].Newer = gib.Newer;
        else
            oldest = gib.Newer;

        if (gib.Newer >= 0)
            gibs[gib.Newer].Older = gib.Older;
        else
            newest = gib.Older;

        if (gib.State == State.Settled)
            RemoveStatic(slot);

        if (gib.Counted)
        {
            crowd.Remove(gib.Cell);
            gib.Counted = false;
        }

        staying--;
    }

    private int supportWearRevision;
    private void Simulate(float deltaTime)
    {
        bool wearChanged = supportWearRevision != Level_GrassWearSystem.Revision;
        supportWearRevision = Level_GrassWearSystem.Revision;
        int splats = 0;
        float grip = Mathf.Exp(-friction * deltaTime);
        Effect_BloodSplats ground = Effect_BloodSplats.Instance;

        for (int i = 0; i < maxGibs; i++)
        {
            ref Gib gib = ref gibs[i];

            if (gib.State == State.Free)
                continue;
            if (gib.State == State.Settled)
            {
                if (!wearChanged) continue;
                float target = Level_GroundSurface.HeightAt(gib.Position,floor)+gib.Radius*(1-gib.Squash.value-agedSink*gib.Age);
                if (Mathf.Abs(target-gib.Position.y) <= 0.02f) continue;
                RemoveStatic(i); gib.State = State.Grounded;
            }

            if (gib.State == State.Grounded && !Horde_DebrisBudget.CleanupPaused)
                gib.Age = Mathf.Min(1f, gib.Age + deltaTime / ageTime);

            float rest = Level_GroundSurface.HeightAt(gib.Position, floor) + gib.Radius * (1f - gib.Squash.value - agedSink * gib.Age);
            // A skidding piece crossing the shore falls to the bed instead of snapping vertically to it.
            if (gib.State == State.Grounded && gib.Position.y > rest + 0.1f)
            {
                if (gib.Counted) { crowd.Remove(gib.Cell); gib.Counted = false; }
                gib.State = State.Flying;
            }
            float flatten = agedFlatten * gib.Age;

            if (gib.State == State.Flying)
            {
                gib.Velocity.y -= gravity * deltaTime;
                gib.Position += gib.Velocity * deltaTime;

                // The ground: squish, a splat, and at most a small hop
                if (gib.Position.y <= rest && gib.Velocity.y < 0f)
                {
                    float impact = -gib.Velocity.y;
                    gib.Position.y = rest;
                    gib.Squash.velocity += impact * squishKick;

                    if (ground != null && splatScale > 0f && splats < maxSplatsPerFrame && impact > settleSpeed)
                    {
                        ground.AddSplat(gib.Position, gib.Radius * 2f * splatScale);
                        splats++;
                    }

                    gib.Velocity.x *= impactKeep;
                    gib.Velocity.z *= impactKeep;
                    gib.Spin *= impactKeep;
                    gib.Velocity.y = impact * bounce;

                    if (gib.Velocity.y < settleSpeed)
                    {
                        gib.Velocity.y = 0f;
                        gib.State = State.Grounded;
                        gib.Cell = crowd.CellAt(gib.Position);
                        gib.Counted = true;
                        crowd.Add(gib.Cell);
                    }
                }
            }
            else
            {
                // On the ground (or leaving from it): skidding to a stop, resting on its squish
                gib.Velocity.x *= grip;
                gib.Velocity.z *= grip;
                gib.Spin *= grip;
                gib.Position.x += gib.Velocity.x * deltaTime;
                gib.Position.z += gib.Velocity.z * deltaTime;

                if (gib.Velocity.y == 0f)
                    gib.Position.y = rest;
                else
                {
                    // Left in mid-air: it keeps falling as it goes
                    gib.Velocity.y -= gravity * deltaTime;
                    gib.Position.y = Mathf.Max(rest, gib.Position.y + gib.Velocity.y * deltaTime);
                }
            }

            float degrees = gib.Spin.magnitude * deltaTime;

            if (degrees > 0.001f)
                gib.Rotation = Quaternion.AngleAxis(degrees, gib.Spin) * gib.Rotation;

            Core_Math.Spring(ref gib.Squash, flatten, squishSpring, deltaTime);

            if (gib.Squash.value > maxSquish)
            {
                gib.Squash.value = maxSquish;
                gib.Squash.velocity = Mathf.Min(gib.Squash.velocity, 0f);
            }

            if (gib.State == State.Leaving)
            {
                Core_Math.Spring(ref gib.Size, 0f, leaveSpring, deltaTime);

                if (gib.Size.value <= 0f)
                {
                    gib.State = State.Free;
                    free[freeCount++] = i;
                }
            }
            else if (gib.State == State.Grounded && gib.Velocity.x * gib.Velocity.x + gib.Velocity.z * gib.Velocity.z < 0.01f
                     && gib.Age >= 1f
                     && gib.Spin.sqrMagnitude < 1f && Mathf.Abs(gib.Squash.value - flatten) < 0.002f && Mathf.Abs(gib.Squash.velocity) < 0.02f)
            {
                // Stopped: from now on it's only drawn
                gib.Position.y = rest;
                gib.State = State.Settled;
                AddStatic(i);
            }
        }
    }

    private void Draw()
    {
        for (int i = 0; i < maxGibs; i++)
        {
            ref Gib gib = ref gibs[i];

            if (gib.State == State.Free || gib.State == State.Settled)
                continue;

            Bucket bucket = buckets[gib.Mesh];
            bucket.Matrices[bucket.Count] = MatrixOf(gib);
            bucket.Colours[bucket.Count] = ColourOf(gib);
            bucket.Seeds[bucket.Count] = gib.Seed;
            bucket.Wobbles[bucket.Count] = gib.State == State.Flying ? 1f : 0f;
            bucket.Count++;

            if (bucket.Count == Batch)
                Flush(bucket);
        }

        for (int i = 0; i < buckets.Length; i++)
        {
            Flush(buckets[i]);
            DrawStatic(buckets[i]);
        }
    }

    private Vector4 ColourOf(in Gib gib)
    {
        if (gib.Age <= 0f)
            return gib.Colour;

        Vector3 live = gib.Colour;
        float grey = live.x * 0.3f + live.y * 0.59f + live.z * 0.11f;
        Vector3 dead = Vector3.Lerp(live, new Vector3(grey, grey, grey), agedGrey) * agedBrightness;
        return Vector3.Lerp(live, dead, gib.Age);
    }

    private static Matrix4x4 MatrixOf(in Gib gib)
    {
        Matrix4x4 matrix = Matrix4x4.TRS(gib.Position, gib.Rotation, gib.Scale * gib.Size.value);

        // Squish in the world: flatter and wider, whichever way the piece lies. Only the rotation/scale columns change.
        float squash = gib.Squash.value;

        if (squash != 0f)
        {
            float wide = 1f + squash * 0.5f;
            float flat = 1f - squash;
            matrix.m00 *= wide; matrix.m01 *= wide; matrix.m02 *= wide;
            matrix.m10 *= flat; matrix.m11 *= flat; matrix.m12 *= flat;
            matrix.m20 *= wide; matrix.m21 *= wide; matrix.m22 *= wide;
        }

        return matrix;
    }

    // A piece that has stopped goes in with its mesh's settled pieces
    private void AddStatic(int slot)
    {
        ref Gib gib = ref gibs[slot];
        Bucket bucket = buckets[gib.Mesh];
        int at = bucket.StaticCount++;
        int chunkIndex = at / Batch;

        if (chunkIndex == bucket.Chunks.Count)
            bucket.Chunks.Add(new StaticChunk());

        StaticChunk chunk = bucket.Chunks[chunkIndex];
        int i = at % Batch;
        chunk.Matrices[i] = MatrixOf(gib);
        chunk.Colours[i] = ColourOf(gib);
        chunk.Seeds[i] = gib.Seed;
        chunk.Owners[i] = slot;
        chunk.Dirty = true;
        gib.StaticSlot = at;
    }

    // Takes a piece out; the last settled one of that mesh moves into its place
    private void RemoveStatic(int slot)
    {
        ref Gib gib = ref gibs[slot];
        Bucket bucket = buckets[gib.Mesh];
        int last = --bucket.StaticCount;
        int at = gib.StaticSlot;

        if (at == last)
            return;

        StaticChunk from = bucket.Chunks[last / Batch];
        StaticChunk to = bucket.Chunks[at / Batch];
        int f = last % Batch;
        int t = at % Batch;
        to.Matrices[t] = from.Matrices[f];
        to.Colours[t] = from.Colours[f];
        to.Seeds[t] = from.Seeds[f];
        to.Owners[t] = from.Owners[f];
        to.Dirty = true;
        gibs[to.Owners[t]].StaticSlot = at;
    }

    private void DrawStatic(Bucket bucket)
    {
        for (int c = 0; c * Batch < bucket.StaticCount; c++)
        {
            StaticChunk chunk = bucket.Chunks[c];

            if (chunk.Dirty)
            {
                chunk.Properties.SetVectorArray(SkinId, chunk.Colours);
                chunk.Properties.SetFloatArray(SeedId, chunk.Seeds);
                chunk.Properties.SetFloatArray(WobbleId, chunk.Wobbles);
                chunk.Dirty = false;
            }

            RenderParams parameters = renderParams;
            parameters.matProps = chunk.Properties;
            Graphics.RenderMeshInstanced(parameters, bucket.Mesh, 0, chunk.Matrices, Mathf.Min(Batch, bucket.StaticCount - c * Batch));
        }
    }

    private void Flush(Bucket bucket)
    {
        if (bucket.Count == 0)
            return;

        properties.SetVectorArray(SkinId, bucket.Colours);
        properties.SetFloatArray(SeedId, bucket.Seeds);
        properties.SetFloatArray(WobbleId, bucket.Wobbles);
        Graphics.RenderMeshInstanced(renderParams, bucket.Mesh, 0, bucket.Matrices, bucket.Count);
        bucket.Count = 0;
    }
}
