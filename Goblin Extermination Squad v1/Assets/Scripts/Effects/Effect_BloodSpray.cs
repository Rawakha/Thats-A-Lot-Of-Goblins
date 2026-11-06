using UnityEngine;

// Blood droplets flying through the air, made for this game instead of a Unity Particle System: a fixed pool of
// droplets simulated here (gravity, a little air drag) and drawn GPU instanced as small chunky blobs, stretched along
// the way they fly and thinning out as they go. When one hits the ground it leaves a small splat (Effect_BloodSplats),
// so sprays paint the ground. Unlit and saturated so it reads from the plane.
//   - Direction: a gun kill bursts out along the bullet's path in a tight cone; a blown-apart goblin bursts up and away
//     from the blast, wide and hard.
//   - Shape: many tiny, fast droplets that streak far, a few big, slow blobs that arc and drop.
//   - Puff: a chunky ball at the wound that swells and shrinks in a moment, the flash of colour you see from the plane.
//
// Every goblin death sprays automatically (Horde_System.Killed); anything else can spray too:
//   Effect_BloodSpray.TrySpray(point, direction, count, strength, spread)
// Past Max Droplets the oldest are reused; past Max Emit Per Frame a frame's sprays are thinned, so a bomb killing
// hundreds can't flood it.
//
// Setup: put this in the scene (next to Effect_BloodSplats), make a material on "Goblins/Blood Droplet" and assign it.
//
// GC: none per spray or per frame (fixed arrays). One UpdateManager subscription (OnLateUpdate) and one to
// Horde_System.Killed.
public class Effect_BloodSpray : MonoBehaviour
{
    [SerializeField] private Material material;

    [Tooltip("Most droplets in the air at once. Past this the oldest are reused.")]
    [SerializeField, Min(16)] private int maxDroplets = 2048;

    [Tooltip("Most droplets started in one frame, across every spray, so a huge blast stays cheap.")]
    [SerializeField, Min(1)] private int maxEmitPerFrame = 300;

    [Tooltip("Height of the ground. Combat_Projectiles' Floor Height is used instead when there is one.")]
    [SerializeField] private float floorHeight;

    [Header("Droplets")]
    [Tooltip("Droplet size, metres: smallest to biggest.")]
    [SerializeField] private Vector2 size = new Vector2(0.08f, 0.3f);

    [Tooltip("Launch speed, m/s: the biggest blobs' to the tiniest droplets', times the spray's strength. Small ones go fast.")]
    [SerializeField] private Vector2 speed = new Vector2(3f, 11f);

    [Tooltip("How many are tiny: 1 is an even mix of sizes, higher makes big blobs rarer.")]
    [SerializeField, Range(1f, 5f)] private float smallBias = 2.5f;

    [Tooltip("Random change in each droplet's speed, as a share, so a burst isn't one neat shell.")]
    [SerializeField, Range(0f, 1f)] private float speedJitter = 0.3f;

    [Tooltip("Size left when a droplet has flown for Shrink Time, as a share of its size.")]
    [SerializeField, Range(0f, 1f)] private float shrinkTo = 0.4f;

    [Tooltip("Seconds a droplet takes to thin down to Shrink To.")]
    [SerializeField, Min(0.01f)] private float shrinkTime = 0.7f;

    [Tooltip("Gravity on droplets, m/s².")]
    [SerializeField, Min(0f)] private float gravity = 22f;

    [Tooltip("Air drag: share of speed lost per second.")]
    [SerializeField, Range(0f, 5f)] private float drag = 0.6f;

    [Tooltip("How much a droplet stretches along its flight per m/s, so fast ones streak.")]
    [SerializeField, Min(0f)] private float stretch = 0.2f;

    [Header("On Death")]
    [Tooltip("Droplets per goblin death (random between).")]
    [SerializeField] private Vector2Int perDeath = new Vector2Int(5, 9);

    [Tooltip("Height above the goblin's feet the death spray starts, metres.")]
    [SerializeField, Min(0f)] private float deathHeight = 0.7f;

    [Tooltip("Gun kills: how far droplets fan out from the bullet's path, degrees.")]
    [SerializeField, Range(0f, 180f)] private float hitSpread = 28f;

    [Tooltip("Gun kills: how much the burst tilts upward (0 straight along the bullet, 1 halfway to straight up).")]
    [SerializeField, Range(0f, 2f)] private float hitLift = 0.35f;

    [Header("On Blown Apart")]
    [Tooltip("How far droplets fan out, degrees.")]
    [SerializeField, Range(0f, 180f)] private float blastSpread = 65f;

    [Tooltip("How much the burst points upward rather than away from the blast (1 is 45 degrees).")]
    [SerializeField, Range(0f, 3f)] private float blastLift = 1.2f;

    [Tooltip("Droplets, as a multiple of Per Death.")]
    [SerializeField, Min(0f)] private float blastCount = 2f;

    [Tooltip("Launch speed, as a multiple.")]
    [SerializeField, Min(0f)] private float blastStrength = 1.5f;

    [Header("Puff")]
    [Tooltip("Size of the ball at the wound, metres. 0 turns puffs off.")]
    [SerializeField, Min(0f)] private float puffSize = 0.8f;

    [Tooltip("Puff size, as a multiple, when blown apart.")]
    [SerializeField, Min(0f)] private float blastPuff = 2f;

    [Tooltip("Seconds a puff lasts.")]
    [SerializeField, Min(0.01f)] private float puffTime = 0.2f;

    [Tooltip("Share of its life a puff spends swelling; the rest it shrinks.")]
    [SerializeField, Range(0.05f, 0.95f)] private float puffPeak = 0.25f;

    [Tooltip("Most puffs at once. Past this new ones are skipped until there's room.")]
    [SerializeField, Min(1)] private int maxPuffs = 128;

    [Header("Landing")]
    [Tooltip("Chance (0-1) a droplet leaves a splat where it lands.")]
    [SerializeField, Range(0f, 1f)] private float splatChance = 0.5f;

    [Tooltip("Size of a droplet's splat as a multiple of the droplet (diameter).")]
    [SerializeField, Min(0f)] private float splatScale = 3.5f;

    [Tooltip("Most landing splats made per frame.")]
    [SerializeField, Min(0)] private int maxSplatsPerFrame = 40;

    private const int Batch = 1023;

    private Mesh droplet;
    private Mesh puff;
    private Vector3[] puffPositions;
    private float[] puffSizes;
    private float[] puffBorn;
    private Matrix4x4[] puffMatrices;
    private int puffCount;
    private float[] born;
    private Vector3[] positions;
    private Vector3[] velocities;
    private float[] sizes;
    private bool[] alive;
    private Matrix4x4[] matrices;
    private int next;
    private int emittedThisFrame;
    private float floor;
    private RenderParams renderParams;
    private Horde_System horde;

    public static Effect_BloodSpray Instance { get; private set; }

    public static bool TrySpray(Vector3 point, Vector3 direction, int count, float strength = 1f, float spreadDegrees = 40f)
    {
        if (Instance == null)
            return false;

        Instance.Spray(point, direction, count, strength, spreadDegrees);
        return true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"{name}: A second Effect_BloodSpray was found, this one is disabled.", this);
            enabled = false;
            return;
        }

        if (material == null)
        {
            Debug.LogError($"{name}: Effect_BloodSpray has no material (make one on Goblins/Blood Droplet).", this);
            enabled = false;
            return;
        }

        Instance = this;
        material.enableInstancing = true;
        droplet = BuildDroplet();
        puff = BuildPuff();
        puffPositions = new Vector3[maxPuffs];
        puffSizes = new float[maxPuffs];
        puffBorn = new float[maxPuffs];
        puffMatrices = new Matrix4x4[maxPuffs];
        born = new float[maxDroplets];

        positions = new Vector3[maxDroplets];
        velocities = new Vector3[maxDroplets];
        sizes = new float[maxDroplets];
        alive = new bool[maxDroplets];
        matrices = new Matrix4x4[maxDroplets];

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

        if (droplet != null)
            Destroy(droplet);

        if (puff != null)
            Destroy(puff);
    }

    // A death: out along the bullet, or up and away from the blast
    private void OnKilled(Horde_Goblin goblin)
    {
        Vector3 point = goblin.transform.position + Vector3.up * deathHeight;
        int count = Random.Range(perDeath.x, perDeath.y + 1);
        Vector3 pushed = goblin.DeathDirection;

        if (goblin.DeathBurst)
        {
            Spray(point, pushed + Vector3.up * blastLift, Mathf.RoundToInt(count * blastCount), blastStrength, blastSpread);
            AddPuff(point, puffSize * blastPuff);
            return;
        }

        Spray(point, pushed + Vector3.up * hitLift, count, 1f, hitSpread);
        AddPuff(point, puffSize);
    }

    private void AddPuff(Vector3 point, float diameter)
    {
        if (diameter <= 0f || puffCount >= maxPuffs)
            return;

        puffPositions[puffCount] = point;
        puffSizes[puffCount] = diameter;
        puffBorn[puffCount] = Time.time;
        puffCount++;
    }

    // Droplets from a point, fanned round a direction, tiny fast ones and a few big slow ones, at Speed x strength
    public void Spray(Vector3 point, Vector3 direction, int count, float strength, float spreadDegrees)
    {
        Vector3 aim = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;

        for (int i = 0; i < count && emittedThisFrame < maxEmitPerFrame; i++)
        {
            int index = next;
            next = (next + 1) % maxDroplets;
            emittedThisFrame++;

            // A random direction within Spread of the aim
            Vector3 random = Random.onUnitSphere;
            Vector3 fanned = Vector3.Slerp(aim, random, spreadDegrees / 180f).normalized;

            // 0 tiny, 1 big: size and speed run opposite ways
            float big = Mathf.Pow(Random.value, smallBias);
            float launch = Mathf.Lerp(speed.y, speed.x, big) * (1f + Random.Range(-speedJitter, speedJitter));

            positions[index] = point;
            velocities[index] = fanned * (launch * strength);
            sizes[index] = Mathf.Lerp(size.x, size.y, big);
            born[index] = Time.time;
            alive[index] = true;
        }
    }

    private void Tick()
    {
        float deltaTime = Time.deltaTime;
        emittedThisFrame = 0;

        if (deltaTime <= 0f)
        {
            Draw();
            return;
        }

        int splats = 0;
        float keep = Mathf.Clamp01(1f - drag * deltaTime);
        Effect_BloodSplats ground = Effect_BloodSplats.Instance;

        for (int i = 0; i < maxDroplets; i++)
        {
            if (!alive[i])
                continue;

            Vector3 velocity = velocities[i] * keep;
            velocity.y -= gravity * deltaTime;
            Vector3 position = positions[i] + velocity * deltaTime;

            // Landed: maybe a splat, then gone
            float surfaceHeight = Level_GroundSurface.HeightAt(position, floor);
            if (position.y <= surfaceHeight)
            {
                alive[i] = false;

                if (ground != null && splats < maxSplatsPerFrame && Random.value < splatChance)
                {
                    ground.AddSplat(new Vector3(position.x, surfaceHeight, position.z), sizes[i] * splatScale);
                    splats++;
                }

                continue;
            }

            positions[i] = position;
            velocities[i] = velocity;
        }

        Draw();
    }

    // Every droplet in the air, stretched along its flight
    private void Draw()
    {
        int count = 0;

        for (int i = 0; i < maxDroplets; i++)
        {
            if (!alive[i])
                continue;

            Vector3 velocity = velocities[i];
            float speedNow = velocity.magnitude;
            Quaternion facing = speedNow > 0.01f ? Quaternion.LookRotation(velocity / speedNow) : Quaternion.identity;
            float s = sizes[i] * Mathf.Lerp(1f, shrinkTo, (Time.time - born[i]) / shrinkTime);
            matrices[count++] = Matrix4x4.TRS(positions[i], facing, new Vector3(s, s, s * (1f + speedNow * stretch)));
        }

        for (int start = 0; start < count; start += Batch)
            Graphics.RenderMeshInstanced(renderParams, droplet, 0, matrices, Mathf.Min(Batch, count - start), start);

        DrawPuffs();
    }

    // Each puff swells fast then shrinks away; finished ones swap out
    private void DrawPuffs()
    {
        float now = Time.time;

        for (int i = puffCount - 1; i >= 0; i--)
        {
            float t = (now - puffBorn[i]) / puffTime;

            if (t >= 1f)
            {
                puffCount--;
                puffPositions[i] = puffPositions[puffCount];
                puffSizes[i] = puffSizes[puffCount];
                puffBorn[i] = puffBorn[puffCount];
                puffMatrices[i] = puffMatrices[puffCount];
                continue;
            }

            float swell = t < puffPeak ? Core_Math.EaseOut(t / puffPeak) : 1f - Core_Math.Smooth01((t - puffPeak) / (1f - puffPeak));
            float s = puffSizes[i] * swell;
            puffMatrices[i] = Matrix4x4.TRS(puffPositions[i], Quaternion.identity, new Vector3(s, s, s));
        }

        for (int start = 0; start < puffCount; start += Batch)
            Graphics.RenderMeshInstanced(renderParams, puff, 0, puffMatrices, Mathf.Min(Batch, puffCount - start), start);
    }

    // A chunky ball 1 m across: an octahedron split once and pushed out round, flat-shaded
    private static Mesh BuildPuff()
    {
        Vector3[] c = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        int[] faces = { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
        Vector3[] vertices = new Vector3[faces.Length * 4];
        Vector3[] normals = new Vector3[vertices.Length];
        int[] triangles = new int[vertices.Length];
        int v = 0;

        void Face(Vector3 p, Vector3 q, Vector3 r)
        {
            Vector3 normal = Vector3.Cross(q - p, r - p).normalized;

            // Facing outward: (a, b, c) faces along Cross(b - a, c - a)
            if (Vector3.Dot(normal, p + q + r) < 0f)
            {
                (q, r) = (r, q);
                normal = -normal;
            }

            vertices[v] = p * 0.5f; vertices[v + 1] = q * 0.5f; vertices[v + 2] = r * 0.5f;
            normals[v] = normals[v + 1] = normals[v + 2] = normal;
            triangles[v] = v; triangles[v + 1] = v + 1; triangles[v + 2] = v + 2;
            v += 3;
        }

        for (int f = 0; f < faces.Length; f += 3)
        {
            Vector3 a = c[faces[f]], b = c[faces[f + 1]], d = c[faces[f + 2]];
            Vector3 ab = (a + b).normalized, bd = (b + d).normalized, da = (d + a).normalized;
            Face(a, ab, da);
            Face(ab, b, bd);
            Face(da, bd, d);
            Face(ab, bd, da);
        }

        Mesh mesh = new Mesh { name = "Blood Puff" };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    // A chunky blob: an octahedron 1 m across, long axis along Z
    private static Mesh BuildDroplet()
    {
        Vector3[] points =
        {
            new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 0f),
            new Vector3(0f, 0.5f, 0f), new Vector3(0f, -0.5f, 0f)
        };

        int[] faces =
        {
            0, 4, 2,  0, 2, 5,  0, 5, 3,  0, 3, 4,
            1, 2, 4,  1, 5, 2,  1, 3, 5,  1, 4, 3
        };

        // Each face its own vertices, so it's flat-shaded and chunky
        Vector3[] vertices = new Vector3[faces.Length];
        Vector3[] normals = new Vector3[faces.Length];
        int[] triangles = new int[faces.Length];

        for (int f = 0; f < faces.Length; f += 3)
        {
            Vector3 a = points[faces[f]], b = points[faces[f + 1]], c = points[faces[f + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

            // Facing outward: Unity's front faces wind clockwise, (a, b, c) faces along Cross(b - a, c - a)
            if (Vector3.Dot(normal, a + b + c) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }

            vertices[f] = a; vertices[f + 1] = b; vertices[f + 2] = c;
            normals[f] = normals[f + 1] = normals[f + 2] = normal;
            triangles[f] = f; triangles[f + 1] = f + 1; triangles[f + 2] = f + 2;
        }

        Mesh mesh = new Mesh { name = "Blood Droplet" };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }
}
