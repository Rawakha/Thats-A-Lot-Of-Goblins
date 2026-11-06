using System.Collections.Generic;
using UnityEngine;

// Blood splats on the ground where goblins die: a big blob plus a few droplets flung round it, each springing out from
// nothing past its size so it lands with a splat. They stay (the battlefield shows what you did) until they reach
// Lifetime, or the ground nears Max Splats and room is needed. Room comes from the most crowded spots first: a splat on
// top of many others is redundant, so the overall area of damage stays intact for longer. Either way they spring down
// to nothing, never pop. Fade (optional) lets them turn transparent with age.
// Built for thousands: every splat is one flat quad drawn with GPU instancing (Graphics.RenderMeshInstanced, 1023 a
// call) with the "Goblins/Blood Splat" shader, which draws the blob itself (no texture) and varies each one's shape
// and shade. No GameObjects, no decal projectors. Goblins killed in the air splat when they come down near the
// ground; ones that fall into a hole don't splat at all.
//
// Setup: put this anywhere in the scene (an Effects object), make a material on the Goblins/Blood Splat shader and
// assign it. Floor Height should match the ground (Combat_Projectiles' Floor Height is used if there is one).
//
// Effect_BloodSpray's droplets add small splats where they land (AddSplat).
//
// GC: none per kill or per frame (fixed arrays, a pre-sized pending list). One UpdateManager subscription
// (OnLateUpdate) and one to Horde_System.Killed.
public class Effect_BloodSplats : MonoBehaviour
{
    private struct Pending
    {
        public Horde_Goblin Goblin;
        public float Until;
    }

    [SerializeField] private Material material;

    [Tooltip("Slots kept free: splats start springing away this far before Max Splats, so there's time to see them go.")]
    [SerializeField, Min(1)] private int roomMargin = 128;

    [Tooltip("Size of the squares the ground is divided into to measure crowding, metres. Splats in the fullest squares go first.")]
    [SerializeField, Min(0.25f)] private float crowdCell = 1.5f;

    [Header("Fade")]
    [Tooltip("Splats turn transparent with age.")]
    [SerializeField] private bool fade;

    [Tooltip("Seconds before a splat starts to fade.")]
    [ShowIf(nameof(fade), true)]
    [SerializeField, Min(0f)] private float fadeDelay = 20f;

    [Tooltip("Seconds the fade takes.")]
    [ShowIf(nameof(fade), true)]
    [SerializeField, Min(0.01f)] private float fadeDuration = 15f;

    [Tooltip("Opacity it fades to, 0-1. At 0 a fully faded splat is removed.")]
    [ShowIf(nameof(fade), true)]
    [SerializeField, Range(0f, 1f)] private float fadeTo = 0.25f;

    [Tooltip("Height of the ground. Combat_Projectiles' Floor Height is used instead when there is one.")]
    [SerializeField] private float floorHeight;

    [Tooltip("Splats sit this far above the ground, metres, so they never flicker into it.")]
    [SerializeField, Min(0f)] private float heightOffset = 0.02f;

    [Header("Main Splat")]
    [Tooltip("Diameter of the big blob under each death, metres (random between).")]
    [SerializeField] private Vector2 size = new Vector2(1.2f, 2f);

    [Header("Droplets")]
    [Tooltip("Small splats flung round each death (random between).")]
    [SerializeField] private Vector2Int droplets = new Vector2Int(2, 5);

    [Tooltip("Diameter of each droplet, metres (random between).")]
    [SerializeField] private Vector2 dropletSize = new Vector2(0.2f, 0.5f);

    [Tooltip("How far from the body droplets land, metres.")]
    [SerializeField, Min(0f)] private float dropletSpread = 1.4f;

    [Header("Springs")]
    [Tooltip("How a splat scales up when it lands. Higher strength is quicker; a low damper overshoots and wobbles.")]
    [SerializeField] private Core_SpringSettings growSpring = new Core_SpringSettings(500f, 16f);

    [Tooltip("How a splat scales down when it goes.")]
    [SerializeField] private Core_SpringSettings shrinkSpring = new Core_SpringSettings(120f, 14f);

    [Header("Air Kills")]
    [Tooltip("A goblin killed in the air splats once it's this close to the ground, metres.")]
    [SerializeField, Min(0f)] private float landedHeight = 0.6f;

    [Tooltip("Gives up on an air kill that hasn't come down after this long, seconds.")]
    [SerializeField, Min(0.1f)] private float maxWait = 4f;

    private const int Batch = 1023;
    private const int CrowdCells = 4096;   // a power of two; squares are hashed into this many counters

    private Mesh quad;
    private Matrix4x4[] matrices;
    private Vector3[] positions;
    private float[] sizes;
    private float[] angles;
    private float[] born;
    private Core_SpringFloatState[] scales;
    private bool[] shrinking;
    private bool[] animating;
    private int[] cellOf;
    private int count;     // slots ever used: what gets drawn (empty ones have zero scale)
    private int staying;   // splats not springing away

    private readonly int[] crowd = new int[CrowdCells];
    private int[] free;
    private int freeCount;
    private int cursor;

    // Splats whose spring is moving (indices), and goblins killed in the air waiting to come down
    private readonly List<int> moving = new List<int>(256);
    private readonly List<Pending> pending = new List<Pending>(128);

    private Horde_System horde;
    private RenderParams renderParams;
    private float floor;

    // From Horde_DebrisBudget
    private int maxSplats;
    private float lifetime;

    public static Effect_BloodSplats Instance { get; private set; }

    // Splats staying on the ground
    public int Count => staying;

    private void Awake()
    {
        if (material == null)
        {
            Debug.LogError($"{name}: Effect_BloodSplats has no material (make one on Goblins/Blood Splat).", this);
            enabled = false;
            return;
        }

        Instance = this;
        material.enableInstancing = true;
        quad = BuildQuad();

        maxSplats = Horde_DebrisBudget.Current.MaxSplats;
        lifetime = Horde_DebrisBudget.Current.splatLifetime;
        matrices = new Matrix4x4[maxSplats];
        positions = new Vector3[maxSplats];
        sizes = new float[maxSplats];
        angles = new float[maxSplats];
        born = new float[maxSplats];
        scales = new Core_SpringFloatState[maxSplats];
        shrinking = new bool[maxSplats];
        animating = new bool[maxSplats];
        cellOf = new int[maxSplats];
        free = new int[maxSplats];

        // Handed out lowest first, so the drawn range only grows as needed
        for (int i = 0; i < maxSplats; i++)
            free[i] = maxSplats - 1 - i;

        freeCount = maxSplats;

        renderParams = new RenderParams(material)
        {
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000f)
        };
    }

    private void Start()
    {
        floor = Combat_Projectiles.Instance != null ? Combat_Projectiles.Instance.FloorHeight : floorHeight;
        horde = Horde_System.Instance;

        if (horde != null)
            horde.Killed += OnKilled;
        else
            Debug.LogWarning($"{name}: No Horde_System, so no blood splats.", this);
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

    private void OnDestroy()
    {
        if (horde != null)
            horde.Killed -= OnKilled;

        if (Instance == this)
            Instance = null;

        if (quad != null)
            Destroy(quad);
    }

    private void OnKilled(Horde_Goblin goblin)
    {
        Vector3 at = goblin.transform.position;

        // Up in the air: wait for it to come down
        if (at.y - Level_GroundSurface.HeightAt(at, floor) > landedHeight)
        {
            pending.Add(new Pending { Goblin = goblin, Until = Time.time + maxWait });
            return;
        }

        Splatter(at);
    }

    // A body hits the ground: one big blob and a few droplets round it
    public void Splatter(Vector3 at)
    {
        // Down a hole: nothing to splat on
        if (at.y < Level_GroundSurface.HeightAt(at, floor) - 0.2f)
            return;

        Add(at, Random.Range(size.x, size.y));

        int drops = Random.Range(droplets.x, droplets.y + 1);

        for (int i = 0; i < drops; i++)
        {
            Vector2 offset = Random.insideUnitCircle * dropletSpread;
            Add(at + new Vector3(offset.x, 0f, offset.y), Random.Range(dropletSize.x, dropletSize.y));
        }
    }

    // One splat of this diameter on the ground under a point (blood droplets landing use this)
    public void AddSplat(Vector3 at, float diameter) => Add(at, diameter);

    private void Add(Vector3 at, float diameter)
    {
        int index;

        if (freeCount > 0)
        {
            index = free[--freeCount];
        }
        else
        {
            // A flood used the margin up in one frame: take over the next slot round
            index = cursor;
            cursor = (cursor + 1) % maxSplats;

            if (!shrinking[index])
            {
                crowd[cellOf[index]]--;
                staying--;
            }
        }

        count = Mathf.Max(count, index + 1);
        staying++;
        cellOf[index] = CellAt(at);
        crowd[cellOf[index]]++;

        positions[index] = new Vector3(at.x, Level_GroundSurface.HeightAt(at, floor) + heightOffset, at.z);
        sizes[index] = diameter;
        angles[index] = Random.Range(0f, 360f);
        born[index] = Time.time;
        matrices[index] = Matrix(index, 0f);
        scales[index] = default;
        shrinking[index] = false;
        StartMoving(index);
    }

    private void StartMoving(int index)
    {
        if (animating[index])
            return;

        animating[index] = true;
        moving.Add(index);
    }

    private Matrix4x4 Matrix(int index, float scale)
    {
        // The Y scale carries the opacity to the shader (the quad is flat, so it changes nothing else)
        float s = sizes[index] * scale;
        return Matrix4x4.TRS(positions[index], Quaternion.Euler(0f, angles[index], 0f), new Vector3(s, Opacity(index, Time.time), s));
    }

    private float Opacity(int index, float now)
    {
        if (!fade)
            return 1f;

        return Mathf.Lerp(1f, fadeTo, Mathf.Clamp01((now - born[index] - fadeDelay) / fadeDuration));
    }

    private int CellAt(Vector3 at)
    {
        int x = Mathf.FloorToInt(at.x / crowdCell);
        int z = Mathf.FloorToInt(at.z / crowdCell);
        return (x * 73856093 ^ z * 19349663) & (CrowdCells - 1);
    }

    private void Retire(int index)
    {
        crowd[cellOf[index]]--;
        staying--;
        shrinking[index] = true;
        StartMoving(index);
    }

    private void Tick()
    {
        float now = Time.time;
        LandAirKills(now);
        RetireByAge(now);
        MakeRoom();
        FadeAll(now);
        Animate(Time.deltaTime);
        Draw();
    }

    // Air kills that have come down splat now; ones gone (despawned, down a hole) or taking too long are dropped
    private void LandAirKills(float now)
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            Pending waiting = pending[i];
            Horde_Goblin goblin = waiting.Goblin;
            bool gone = goblin == null || !goblin.IsActive;

            if (!gone && goblin.transform.position.y - Level_GroundSurface.HeightAt(goblin.transform.position, floor) > landedHeight && now < waiting.Until)
                continue;

            if (!gone && now < waiting.Until)
                Splatter(goblin.transform.position);

            pending[i] = pending[pending.Count - 1];
            pending.RemoveAt(pending.Count - 1);
        }
    }

    // Every splat still staying, checked against Lifetime (and a full fade to nothing)
    private void RetireByAge(float now)
    {
        float most = lifetime > 0f ? lifetime : float.PositiveInfinity;

        if (fade && fadeTo <= 0f)
            most = Mathf.Min(most, fadeDelay + fadeDuration);

        if (float.IsPositiveInfinity(most))
            return;

        for (int i = 0; i < count; i++)
        {
            if (IsStaying(i) && now - born[i] >= most)
                Retire(i);
        }
    }

    // Near Max Splats: the fullest squares give theirs up first, then less and less crowded ones, so lone splats on the
    // edge of the damage are the last to go. Runs only while over the limit, a few passes at most.
    private void MakeRoom()
    {
        int over = staying - (maxSplats - Mathf.Min(roomMargin, maxSplats / 2));

        if (over <= 0)
            return;

        int fullest = 1;

        for (int c = 0; c < CrowdCells; c++)
            fullest = Mathf.Max(fullest, crowd[c]);

        for (int limit = Mathf.Max(fullest / 2, 1); over > 0; limit /= 2)
        {
            for (int k = 0; k < count && over > 0; k++)
            {
                // Starts where it stopped last time, so one corner of the slots isn't favoured
                int i = cursor;
                cursor = (cursor + 1) % count;

                if (IsStaying(i) && crowd[cellOf[i]] > limit)
                {
                    Retire(i);
                    over--;
                }
            }

            if (limit <= 0)
                break;
        }
    }

    private bool IsStaying(int index) => sizes[index] > 0f && !shrinking[index];

    // Opacity only touches the one matrix entry that holds it, so this stays cheap across every splat
    private void FadeAll(float now)
    {
        if (!fade)
            return;

        for (int i = 0; i < count; i++)
        {
            if (sizes[i] > 0f)
                matrices[i].m11 = Opacity(i, now);
        }
    }

    // Only splats on the move are touched: up to full size, or down to nothing
    private void Animate(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        for (int i = moving.Count - 1; i >= 0; i--)
        {
            int index = moving[i];
            bool going = shrinking[index];
            float target = going ? 0f : 1f;
            float scale = Core_Math.Spring(ref scales[index], target, going ? shrinkSpring : growSpring, deltaTime);
            bool settled = Mathf.Abs(scale - target) < 0.002f && Mathf.Abs(scales[index].velocity) < 0.02f;

            // Going: done as soon as it reaches nothing (it mustn't bounce back up through it)
            if (settled || (going && scale <= 0f))
            {
                scale = target;
                animating[index] = false;
                moving[i] = moving[moving.Count - 1];
                moving.RemoveAt(moving.Count - 1);

                // Gone: its slot is free again
                if (going)
                {
                    matrices[index] = Matrix(index, 0f);
                    sizes[index] = 0f;
                    shrinking[index] = false;
                    free[freeCount++] = index;
                    continue;
                }
            }

            matrices[index] = Matrix(index, scale);
        }
    }

    private int supportWearRevision, supportCursor, supportRemaining;
    private void Draw()
    {
        if (supportWearRevision != Level_GrassWearSystem.Revision)
        { supportWearRevision = Level_GrassWearSystem.Revision; supportRemaining = count; }
        // Bounded refresh of old splats after paths lower the surface; animation and opacity stay intact.
        for (int work = 0; work < 256 && supportRemaining > 0 && count > 0; work++,supportRemaining--)
        {
            int i = supportCursor++%count;
            if (sizes[i] <= 0) continue;
            float support = Level_GroundSurface.HeightAt(positions[i],floor)+heightOffset;
            positions[i].y = support; matrices[i].m13 = support;
        }
        for (int start = 0; start < count; start += Batch)
            Graphics.RenderMeshInstanced(renderParams, quad, 0, matrices, Mathf.Min(Batch, count - start), start);
    }

    // A 1 x 1 quad lying flat, facing up, centred on its pivot, UVs 0-1
    private static Mesh BuildQuad()
    {
        Mesh mesh = new Mesh { name = "Blood Splat Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f)
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.RecalculateBounds();
        return mesh;
    }
}
