using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

// A pose on top of a part's authored one, in the part's own space: what the animator writes (bob, lean, squash).
public struct Horde_Pose
{
    public float3 Position;
    public quaternion Rotation;
    public float3 Scale;

    public static Horde_Pose Identity => new Horde_Pose { Position = float3.zero, Rotation = quaternion.identity, Scale = 1f };
}

// Draws every active goblin of one type, instanced, from data.
//   Bake: the type's prefab is read once. Body and Head are the two anchors; every enabled MeshRenderer in the prefab
//     becomes a part, stored relative to its anchor (under Head: the head; anything else: the body).
//   Per goblin: body and head scale (variation) and body and head pose (animation), in lists kept in the same order
//     as the goblins (swap-back on removal).
//   Each frame: a Burst job reads the goblins' root transforms, Horde_AnimateJob writes the body and head poses (if
//     the type has Animation Settings), another builds every part's matrix (root, then the anchor with its scale and
//     pose, then the part), and each (mesh, submesh, material) is drawn with RenderMeshInstanced in chunks of 1023.
// The head rides on the body: its authored offset from the body is stretched by the body's scale and pose, so a
// taller body lifts it and a leaning body carries it, while its own size follows only the head scale.
//
// Eye-card pairs are combined once per type and attached to Head, including authoring objects beside Head.
// GC: none per frame. Arrays are allocated at bake and when the group outgrows its capacity (doubling).
// Eyes add two fixed 1023-vector buffers and a property block per group, owned meshes disposed with the group,
// plus seed/hit time/size/gaze fields in the existing swap-back animation state. No new subscriptions.
public sealed class Horde_RenderGroup : IDisposable
{
    private const int MaxPerDraw = 1023;

    private struct Batch
    {
        public Mesh Mesh;
        public int SubMesh;
        public int Part;
        public RenderParams Params;
        public bool Eyes;
        public int Variation;
        public int Option;
    }

    private readonly Horde_GoblinType type;
    private readonly List<Horde_Goblin> goblins = new List<Horde_Goblin>(64);
    private readonly Batch[] batches;
    private readonly int partCount;
    private readonly Horde_Variations variations;
    private readonly Matrix4x4[] selectedMatrices;
    private readonly List<Mesh> ownedEyeMeshes = new List<Mesh>();
    private readonly Vector4[] eyeBuffer;
    private readonly Vector4[] gazeBuffer;
    private readonly MaterialPropertyBlock eyeProperties;
    private static readonly int EyeStateId = Shader.PropertyToID("_GoblinEyeState");
    private static readonly int EyeGazeId = Shader.PropertyToID("_GoblinEyeGaze");

    // Anchors as authored, in the root's space
    private readonly float3 bodyPosition;
    private readonly quaternion bodyRotation;
    private readonly float3 bodyScale;
    private readonly float3 headPosition;
    private readonly quaternion headRotation;
    private readonly float3 headScale;

    // How far the body's bottom edge sits from the root, measured from the prefab: a killed goblin tips over that edge
    private readonly float autoTipEdge;

    private NativeArray<float4x4> partLocal;     // part relative to its anchor
    private NativeArray<byte> partOnHead;

    private TransformAccessArray transforms;
    private NativeList<float3> bodyScales;
    private NativeList<float3> headScales;
    private NativeList<Horde_Pose> bodyPoses;
    private NativeList<Horde_Pose> headPoses;
    private NativeList<Horde_AnimState> animStates;

    // Per goblin: rgb tint (linear) and a in 0-1 for a hit flash, read by the Goblin shader as _GoblinColorFlash.
    // Sent per draw chunk in a property block, like v1.
    private NativeList<float4> colours;
    private readonly Vector4[] colourBuffer = new Vector4[MaxPerDraw];
    private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    private static readonly int ColourFlashId = Shader.PropertyToID("_GoblinColorFlash");
    private static uint nextSeed = 1;

    private NativeArray<float3> rootPositions;
    private NativeArray<quaternion> rootRotations;
    private NativeArray<quaternion> physicsRotations;     // the roots' real rotations, for goblins thrown with physics
    private NativeArray<float> rootScales;      // 1 alive, shrinking along the death ease once killed
    private NativeArray<float4x4> matrices;     // part-major: part * capacity + goblin

    // The type's death ease (a DOTween ease, from full size to nothing), sampled evenly so the Burst job can follow it
    // with no tweens: baked again only when the ease or its overshoot changes
    private const int DeathCurveSamples = 64;
    private NativeArray<float> deathCurve;
    private DG.Tweening.Ease bakedEase;
    private float bakedOvershoot = float.NaN;

    // The ease of the hop out of a hole, baked the same way; whoever spawns goblins emerging chooses it
    private NativeArray<float> emergeCurve;
    private DG.Tweening.Ease bakedEmergeEase;
    private bool emergeBaked;
    private int capacity;
    private JobHandle handle;

    public int Count => goblins.Count;

    // The goblin drawn at a RenderIndex, and where its root was when last drawn (no engine calls: last frame's data)
    public Horde_Goblin GoblinAt(int index) => goblins[index];
    public float3 LastPosition(int index) => rootPositions[index];
    public int PartCount => partCount;
    public int BatchCount => batches.Length;

    // Per-goblin data, by RenderIndex: scales (variation) and poses (animation). Write them before LateUpdate;
    // the arrays stay valid until a goblin of this type spawns or despawns.
    public NativeArray<float3> BodyScales => bodyScales.AsArray();
    public NativeArray<float3> HeadScales => headScales.AsArray();
    public NativeArray<Horde_Pose> BodyPoses => bodyPoses.AsArray();
    public NativeArray<Horde_Pose> HeadPoses => headPoses.AsArray();

    public void SetScales(int index, float3 body, float3 head)
    {
        handle.Complete();
        bodyScales[index] = body;
        headScales[index] = head;
    }

    // A hit, played by the animation job next frame: impulse in world space, moment = cross(hit point - feet, impulse).
    // Several hits in one frame add up.
    public void Hit(int index, float3 impulse, float3 moment)
    {
        handle.Complete();
        Horde_AnimState state = animStates[index];
        state.PendingHit += impulse;
        state.PendingMoment += moment;
        state.Flash = 1f;
        state.EyeHitTime = Time.time;
        animStates[index] = state;
    }

    // Where a goblin's body and head are drawn right now, with the same sums as Horde_PartMatricesJob (gibs start here)
    public void GetAnchors(int index, out float4x4 body, out float4x4 head)
    {
        handle.Complete();
        float4x4 root = float4x4.TRS(rootPositions[index], rootRotations[index], new float3(rootScales[index]));

        Horde_Pose bodyPose = bodyPoses[index];
        float3 bodyStretch = bodyScales[index] * bodyPose.Scale;
        float3 bodyAt = bodyPosition + math.mul(bodyRotation, bodyPose.Position);
        quaternion bodyTurn = math.mul(bodyRotation, bodyPose.Rotation);
        body = math.mul(root, float4x4.TRS(bodyAt, bodyTurn, bodyScale * bodyStretch));

        float3 fromBody = math.mul(math.inverse(bodyRotation), headPosition - bodyPosition) * bodyStretch;
        Horde_Pose headPose = headPoses[index];
        quaternion carried = math.mul(bodyTurn, math.inverse(bodyRotation));
        float3 headAt = bodyAt + math.mul(bodyTurn, fromBody) + math.mul(math.mul(carried, headRotation), headPose.Position);
        quaternion headTurn = math.mul(math.mul(carried, headRotation), headPose.Rotation);
        head = math.mul(root, float4x4.TRS(headAt, headTurn, headScale * headScales[index] * headPose.Scale));
    }

    // The goblin's tint (rgb) and hit flash (a)
    public float4 ColourAt(int index)
    {
        handle.Complete();
        return colours[index];
    }

    // Stops drawing a goblin's head and whatever rides on it (eyes), or all of it. Its next life rolls new scales.
    public void HideHead(int index)
    {
        handle.Complete();
        headScales[index] = float3.zero;
    }

    public void HideAll(int index)
    {
        handle.Complete();
        headScales[index] = float3.zero;
        bodyScales[index] = float3.zero;
    }

    // A corpse's time is up: it starts its shrink now, whatever delay it was given
    public void StartShrink(int index, float time)
    {
        handle.Complete();
        Horde_AnimState state = animStates[index];
        state.DeathDelay = time - state.DeathTime;
        animStates[index] = state;
    }

    // Both pupils share this offset. Zero restores the material gaze plus automatic glances.
    public void SetEyeGaze(int index, Vector2 gaze)
    {
        handle.Complete();
        Horde_AnimState state = animStates[index];
        state.EyeGaze = new float2(Mathf.Clamp(gaze.x, -1f, 1f), Mathf.Clamp(gaze.y, -1f, 1f));
        animStates[index] = state;
    }

    // In the air (thrown, launched out of a hole) or back on the ground. Spin: tumble speed while flying, degrees/second,
    // round a random horizontal axis.
    public void SetAirborne(int index, bool airborne, float spin)
    {
        handle.Complete();
        Horde_AnimState state = animStates[index];
        state.Airborne = airborne;

        if (airborne)
        {
            float angle = UnityEngine.Random.Range(0f, math.PI * 2f);
            state.TumbleAxis = new float3(math.cos(angle), 0f, math.sin(angle));
            state.TumbleSpeed = spin;
        }

        animStates[index] = state;
    }

    // Thrown with real physics: drawn as its Rigidbody lies until GetUp
    public void SetPhysical(int index)
    {
        handle.Complete();
        Horde_AnimState state = animStates[index];
        state.Physical = true;
        state.Airborne = false;
        state.Tumble = default;
        state.RecoverStart = -1f;
        animStates[index] = state;
    }

    // Came to rest after a throw: eases from how it lay (world rotation) to upright facing yaw degrees over duration,
    // with a squish
    public void GetUp(int index, quaternion lying, float yaw, float time, float duration)
    {
        handle.Complete();
        Horde_AnimState state = animStates[index];
        state.Physical = false;
        state.Yaw = yaw;
        state.RecoverFrom = lying;
        state.RecoverStart = time;
        state.RecoverDuration = duration;
        state.PendingLanding = math.max(state.PendingLanding, 1000f);   // full-strength squish
        animStates[index] = state;
    }

    // Touched down at this downward speed: the squish, and the pop upright from any tumble
    public void Landed(int index, float impactSpeed)
    {
        handle.Complete();
        Horde_AnimState state = animStates[index];
        state.Airborne = false;
        state.PendingLanding = math.max(state.PendingLanding, impactSpeed);
        animStates[index] = state;
    }

    // Starts a hop out of a hole: drawn from 'from' (down in the hole) along an arc of the given height to 'to' (where
    // its body waits on the ground) over duration seconds, eased, turning flipDegrees round the axis square to its travel
    public void Emerge(int index, float3 from, float3 to, float start, float duration, float height, float flipDegrees,
        DG.Tweening.Ease ease)
    {
        handle.Complete();
        BakeEmergeCurve(ease);

        Horde_AnimState state = animStates[index];
        float3 travel = new float3(to.x - from.x, 0f, to.z - from.z);
        float3 forward = math.lengthsq(travel) > 0.0001f ? math.normalize(travel) : new float3(0f, 0f, 1f);

        state.EmergeStart = start;
        state.EmergeDuration = duration;
        state.EmergeFrom = from;
        state.EmergeTo = to;
        state.EmergeHeight = height;
        state.EmergeFlip = flipDegrees;
        state.EmergeAxis = math.cross(new float3(0f, 1f, 0f), forward);     // forward flip: head goes over the way it hops
        state.Airborne = false;
        state.Tumble = default;
        animStates[index] = state;
    }

    // Starts the death: the job tips it over away from the hit (direction in world space, the way the hit pushed),
    // then shrinks it along the death ease
    // Delay: seconds before the shrink, length: seconds of shrinking. Tip off: it doesn't topple (blown apart, or
    // already lying where physics left it). Flash off: no white flash (it flashed when it was killed, in the air).
    public void Kill(int index, float time, float3 direction, float delay, float length, bool tip, bool flash = true)
    {
        handle.Complete();
        Horde_AnimState state = animStates[index];
        state.DeathTime = time;
        state.LiveColour = colours[index].xyz;
        state.DeathDelay = delay;
        state.DeathLength = length;

        if (flash)
            state.Flash = 1f;

        // Falling along the hit: tip about the horizontal axis square to it. No direction: a random way.
        float3 flat = new float3(direction.x, 0f, direction.z);

        if (math.lengthsq(flat) < 0.0001f)
        {
            float angle = UnityEngine.Random.Range(0f, math.PI * 2f);
            flat = new float3(math.cos(angle), 0f, math.sin(angle));
        }

        state.TipDirection = math.normalize(flat);
        state.TipAxis = math.normalize(math.cross(new float3(0f, 1f, 0f), flat));
        state.Tip = new Core_SpringFloatState(0f) { velocity = tip ? type.deathTipKick : 0f };
        animStates[index] = state;
    }

    public Horde_RenderGroup(Horde_GoblinType type, Bounds worldBounds)
    {
        this.type = type;
        GameObject prefab = type.prefab;
        Transform root = prefab.transform;
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        variations = new Horde_Variations(prefab, Mathf.Max(16, type.poolSize));
        if (variations.Count > 0) selectedMatrices = new Matrix4x4[MaxPerDraw];

        Horde_Goblin.FindParts(prefab, out Transform body, out Transform head);

        if (body == null)
            Debug.LogWarning($"{type.name}: the prefab has no Body; parts outside Head hang off the root.", prefab);

        if (head == null)
            Debug.LogWarning($"{type.name}: the prefab has no Head, so nothing scales as a head.", prefab);

        Matrix4x4 bodyMatrix = body != null ? toRoot * body.localToWorldMatrix : Matrix4x4.identity;
        Matrix4x4 headMatrix = head != null ? toRoot * head.localToWorldMatrix : Matrix4x4.identity;
        Decompose(bodyMatrix, out bodyPosition, out bodyRotation, out bodyScale);
        Decompose(headMatrix, out headPosition, out headRotation, out headScale);

        // Anchors without their authored scale: a part's local matrix keeps only its offset from the anchor, and the
        // anchor's scale comes back in at runtime with the goblin's own
        Matrix4x4 bodyFrame = Matrix4x4.TRS(bodyPosition, bodyRotation, bodyScale);
        Matrix4x4 headFrame = Matrix4x4.TRS(headPosition, headRotation, headScale);

        List<float4x4> locals = new List<float4x4>();
        List<byte> onHead = new List<byte>();
        List<Batch> found = new List<Batch>();
        Vector2 bodyMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 bodyMax = new Vector2(float.MinValue, float.MinValue);

        // Eye authoring may be beside Head: bake relative to the head anchor while preserving its actual root pose.
        // Keep the two GameObjects in the prefab, but draw one combined part per pair, never the originals too.
        HashSet<MeshRenderer> eyeRenderers = new HashSet<MeshRenderer>();
        foreach (Horde_EyeCards eyes in prefab.GetComponentsInChildren<Horde_EyeCards>(true))
        {
            if (!eyes.enabled || !variations.Included(eyes.transform) || head == null || !eyes.TryBake(out Mesh combined, out Material material))
                continue;
            Vector2Int choice = variations.Resolve(eyes.transform);
            bool eyeOnHead = choice.x < 0 || variations.OnHead(choice.x);
            ownedEyeMeshes.Add(combined);
            foreach (MeshRenderer renderer in eyes.GetComponentsInChildren<MeshRenderer>(true))
                if (eyes.OwnsRenderer(renderer))
                    eyeRenderers.Add(renderer);
            int partIndex = locals.Count;
            locals.Add((eyeOnHead ? headFrame : bodyFrame).inverse * toRoot * eyes.transform.localToWorldMatrix);
            onHead.Add(eyeOnHead ? (byte)1 : (byte)0);
            if (!material.enableInstancing)
                Debug.LogWarning($"{type.name}: enable GPU Instancing on eye material '{material.name}'.", material);
            found.Add(new Batch
            {
                Mesh = combined, SubMesh = 0, Part = partIndex, Eyes = true, Variation = choice.x, Option = choice.y,
                Params = new RenderParams(material)
                {
                    layer = prefab.layer, shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false, worldBounds = worldBounds
                }
            });
        }

        if (ownedEyeMeshes.Count > 0)
        {
            eyeBuffer = new Vector4[MaxPerDraw];
            gazeBuffer = new Vector4[MaxPerDraw];
            eyeProperties = new MaterialPropertyBlock();
        }

        foreach (MeshRenderer meshRenderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (eyeRenderers.Contains(meshRenderer) || !variations.Included(meshRenderer.transform))
                continue;
            if (!meshRenderer.enabled || !meshRenderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                continue;

            Transform part = meshRenderer.transform;
            Vector2Int choice = variations.Resolve(part);
            bool isHead = head != null && (choice.x >= 0 ? variations.OnHead(choice.x) : (part == head || part.IsChildOf(head)));
            Matrix4x4 inRoot = toRoot * part.localToWorldMatrix;
            Matrix4x4 local = (isHead ? headFrame : bodyFrame).inverse * inRoot;

            // The body's footprint, for where a killed goblin tips over
            if (!isHead)
                Footprint(filter.sharedMesh.bounds, inRoot, ref bodyMin, ref bodyMax);
            int partIndex = locals.Count;

            locals.Add(local);
            onHead.Add(isHead ? (byte)1 : (byte)0);

            Mesh mesh = filter.sharedMesh;
            Material[] materials = meshRenderer.sharedMaterials;

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                Material material = materials.Length > 0 ? materials[Mathf.Min(s, materials.Length - 1)] : null;

                if (material == null)
                    continue;

                if (!material.enableInstancing)
                    Debug.LogWarning($"{type.name}: material '{material.name}' on {part.name} has GPU Instancing off; " +
                                     "tick Enable GPU Instancing on it.", material);

                found.Add(new Batch
                {
                    Variation = choice.x,
                    Option = choice.y,
                    Mesh = mesh,
                    SubMesh = s,
                    Part = partIndex,
                    Params = new RenderParams(material)
                    {
                        layer = prefab.layer,
                        shadowCastingMode = type.shadows,
                        receiveShadows = type.receiveShadows,
                        worldBounds = worldBounds
                    }
                });
            }
        }

        if (locals.Count == 0)
            Debug.LogWarning($"{type.name}: the prefab has no enabled MeshRenderers, so its goblins are invisible.", prefab);

        // Average half-width of the body around the root: how far out its bottom edge is, whichever way it falls
        autoTipEdge = bodyMax.x >= bodyMin.x
            ? Mathf.Max(0f, (bodyMax.x - bodyMin.x + bodyMax.y - bodyMin.y) * 0.25f)
            : type.radius;

        partCount = locals.Count;
        batches = found.ToArray();
        partLocal = new NativeArray<float4x4>(locals.ToArray(), Allocator.Persistent);
        partOnHead = new NativeArray<byte>(onHead.ToArray(), Allocator.Persistent);

        int start = Mathf.Max(16, type.poolSize);
        transforms = new TransformAccessArray(start);
        bodyScales = new NativeList<float3>(start, Allocator.Persistent);
        headScales = new NativeList<float3>(start, Allocator.Persistent);
        bodyPoses = new NativeList<Horde_Pose>(start, Allocator.Persistent);
        headPoses = new NativeList<Horde_Pose>(start, Allocator.Persistent);
        animStates = new NativeList<Horde_AnimState>(start, Allocator.Persistent);
        colours = new NativeList<float4>(start, Allocator.Persistent);
        Allocate(start);
    }

    // Rotation is its starting facing; from then the animation job turns it toward where it walks
    public void Add(Horde_Goblin goblin, Quaternion rotation)
    {
        handle.Complete();

        if (goblins.Count >= capacity)
            Grow();

        goblin.RenderIndex = goblins.Count;
        goblins.Add(goblin);
        transforms.Add(goblin.transform);
        // Combat can query this goblin before its first rendered frame.
        rootPositions[goblin.RenderIndex] = goblin.transform.position;
        // Its own look: body size and shape, head size and colour, rolled from the type's variation
        type.RollLook(out Vector3 body, out Vector3 head, out Vector4 colour);
        bodyScales.Add(body);
        headScales.Add(head);
        colours.Add(colour);
        bodyPoses.Add(Horde_Pose.Identity);
        headPoses.Add(Horde_Pose.Identity);
        uint seed = Hash(nextSeed++);
        variations.Add(Hash(seed ^ 0x85ebca6bu));
        Horde_AnimState state = Horde_AnimState.Create(type.animation, seed, rotation.eulerAngles.y);
        state.EyeSeed = (seed & 0x00ffffffu) / 16777216f;
        state.EyeHitTime = -10000f;
        float minimum = Mathf.Clamp(Mathf.Min(type.eyeSizeRange.x, type.eyeSizeRange.y), 0.1f, 2f);
        float maximum = Mathf.Clamp(Mathf.Max(type.eyeSizeRange.x, type.eyeSizeRange.y), minimum, 2f);
        state.EyeSize = Mathf.Lerp(minimum, maximum, (Hash(seed) & 0x00ffffffu) / 16777216f);
        var eyeRandom = new Unity.Mathematics.Random(Hash(seed ^ 0x9e3779b9u) | 1u);
        if (eyeRandom.NextFloat() < Mathf.Clamp01(type.eyeAsymmetryChance))
        {
            float low = Mathf.Clamp(Mathf.Min(type.eyeAsymmetryRange.x, type.eyeAsymmetryRange.y), 0f, 1f);
            float high = Mathf.Clamp(Mathf.Max(type.eyeAsymmetryRange.x, type.eyeAsymmetryRange.y), low, 1f);
            state.EyeAsymmetry = eyeRandom.NextFloat(low, high) * (eyeRandom.NextBool() ? 1f : -1f);
        }
        animStates.Add(state);
    }

    public void Remove(Horde_Goblin goblin)
    {
        int index = goblin.RenderIndex;

        if (index < 0 || index >= goblins.Count || goblins[index] != goblin)
            return;

        handle.Complete();

        int last = goblins.Count - 1;

        if (index != last)
        {
            goblins[index] = goblins[last];
            goblins[index].RenderIndex = index;
            rootPositions[index] = rootPositions[last];
        }

        variations.Remove(index);
        goblins.RemoveAt(last);
        transforms.RemoveAtSwapBack(index);
        bodyScales.RemoveAtSwapBack(index);
        headScales.RemoveAtSwapBack(index);
        bodyPoses.RemoveAtSwapBack(index);
        headPoses.RemoveAtSwapBack(index);
        animStates.RemoveAtSwapBack(index);
        colours.RemoveAtSwapBack(index);
        goblin.RenderIndex = -1;
    }

    // Builds this frame's matrices on worker threads
    public JobHandle Schedule()
    {
        handle.Complete();
        int count = goblins.Count;

        if (count == 0 || partCount == 0)
        {
            handle = default;
            return handle;
        }

        JobHandle read = new Horde_ReadRootsJob
        {
            Positions = rootPositions,
            Rotations = physicsRotations
        }.ScheduleReadOnly(transforms, 64);

        BakeDeathCurve();

        // Always runs: it turns the goblins (their facing is data, the roots never rotate), flashes and shrinks them,
        // and animates them if the type has Animation Settings
        read = new Horde_AnimateJob
        {
            RootPositions = rootPositions,
            BodyScales = bodyScales.AsArray(),
            PhysicsRotations = physicsRotations,
            DeathCurve = deathCurve,
            EmergeCurve = emergeCurve.IsCreated ? emergeCurve : deathCurve,
            States = animStates.AsArray(),
            Colours = colours.AsArray(),
            CorpseBrightness = type.corpseBrightness,
            CorpseGrey = type.corpseGrey,
            CorpseDimTime = type.corpseDimTime,
            RootRotations = rootRotations,
            RootScales = rootScales,
            TipAngle = type.deathTipAngle,
            TipStiffness = type.deathTipStiffness,
            TipDamping = type.deathTipDamping,
            TipEdge = type.deathTipEdge > 0f ? type.deathTipEdge : autoTipEdge,
            TipBounce = type.deathTipBounce,
            BodyPoses = bodyPoses.AsArray(),
            HeadPoses = headPoses.AsArray(),
            P = type.animation != null ? type.animation.Params : default,
            Animate = type.animation != null,
            TurnSpeed = type.turnSpeed,
            TurnMinSpeed = type.turnMinSpeed,
            DeltaTime = Time.deltaTime,
            Time = Time.time
        }.Schedule(count, 64, read);

        handle = new Horde_PartMatricesJob
        {
            RootPositions = rootPositions,
            RootRotations = rootRotations,
            RootScales = rootScales,
            BodyScales = bodyScales.AsArray(),
            HeadScales = headScales.AsArray(),
            BodyPoses = bodyPoses.AsArray(),
            HeadPoses = headPoses.AsArray(),
            PartLocal = partLocal,
            PartOnHead = partOnHead,
            BodyPosition = bodyPosition,
            BodyRotation = bodyRotation,
            BodyScale = bodyScale,
            HeadPosition = headPosition,
            HeadRotation = headRotation,
            HeadScale = headScale,
            PartCount = partCount,
            Capacity = capacity,
            Matrices = matrices
        }.Schedule(count, 32, read);

        return handle;
    }

    // Waits for the matrices and issues the draws. Returns how many draw calls it made.
    public int Draw()
    {
        handle.Complete();
        int count = goblins.Count;

        if (count == 0)
            return 0;

        // Parts that aren't in a variation group draw everyone straight from the matrices; only the varied
        // option parts need their chosen goblins picked out
        return variations.Count > 0 ? DrawDirect(count, true) + DrawSelected() : DrawDirect(count, false);
    }

    // Every goblin in chunks of 1023, read straight from the matrices with no copying. Common only: skip the parts
    // that belong to a variation option (DrawSelected draws those).
    private int DrawDirect(int count, bool commonOnly)
    {
        NativeArray<Matrix4x4> all = matrices.Reinterpret<Matrix4x4>();
        NativeArray<Vector4> tints = colours.AsArray().Reinterpret<Vector4>();
        int calls = 0;

        // Eye state is only gathered if an eye part draws here (eye layouts in a variation group draw in DrawSelected)
        bool eyes = false;

        for (int b = 0; b < batches.Length && !eyes; b++)
            eyes = batches[b].Eyes && (!commonOnly || batches[b].Variation < 0);

        // Chunk by chunk: the chunk's colours go in the property block once, then every part draws that chunk
        for (int done = 0; done < count; done += MaxPerDraw)
        {
            int chunk = Mathf.Min(MaxPerDraw, count - done);
            NativeArray<Vector4>.Copy(tints, done, colourBuffer, 0, chunk);
            properties.SetVectorArray(ColourFlashId, colourBuffer);

            if (eyes)
            {
                for (int i = 0; i < chunk; i++)
                {
                    int index = done + i;
                    Horde_AnimState state = animStates[index];
                    eyeBuffer[i] = new Vector4(state.EyeSeed, state.EyeHitTime, goblins[index].Dead ? 1f : 0f, state.EyeSize);
                    gazeBuffer[i] = new Vector4(state.EyeGaze.x, state.EyeGaze.y, state.EyeAsymmetry, 0f);
                }
                eyeProperties.SetVectorArray(EyeStateId, eyeBuffer);
                eyeProperties.SetVectorArray(EyeGazeId, gazeBuffer);
            }

            for (int b = 0; b < batches.Length; b++)
            {
                Batch batch = batches[b];

                if (commonOnly && batch.Variation >= 0)
                    continue;

                RenderParams renderParams = batch.Params;
                renderParams.matProps = batch.Eyes ? eyeProperties : properties;

                Graphics.RenderMeshInstanced(renderParams, batch.Mesh, batch.SubMesh, all, chunk, batch.Part * capacity + done);
                calls++;
            }
        }

        return calls;
    }

    // Compact only selected goblins into fixed draw buffers. The same index supplies matrix, tint and eye state.
    // Only the parts of variation options: common parts are drawn directly. Independent groups never create a batch
    // for every possible combination.
    private int DrawSelected()
    {
        var all = matrices.Reinterpret<Matrix4x4>();
        int calls = 0;
        for (int b = 0; b < batches.Length; b++)
        {
            Batch batch = batches[b];
            if (batch.Variation < 0) continue;
            int written = 0;
            for (int i = 0; i < goblins.Count; i++)
            {
                if (!variations.Matches(i, batch.Variation, batch.Option)) continue;
                selectedMatrices[written] = all[batch.Part * capacity + i];
                if (batch.Eyes)
                {
                    Horde_AnimState state = animStates[i];
                    eyeBuffer[written] = new Vector4(state.EyeSeed, state.EyeHitTime, goblins[i].Dead ? 1f : 0f, state.EyeSize);
                    gazeBuffer[written] = new Vector4(state.EyeGaze.x, state.EyeGaze.y, state.EyeAsymmetry, 0f);
                }
                else colourBuffer[written] = colours[i];
                written++;
                if (written == MaxPerDraw)
                {
                    DrawSelectedChunk(batch, written);
                    written = 0;
                    calls++;
                }
            }
            if (written > 0) { DrawSelectedChunk(batch, written); calls++; }
        }
        return calls;
    }

    private void DrawSelectedChunk(Batch batch, int count)
    {
        RenderParams parameters = batch.Params;
        if (batch.Eyes)
        {
            eyeProperties.SetVectorArray(EyeStateId, eyeBuffer);
            eyeProperties.SetVectorArray(EyeGazeId, gazeBuffer);
            parameters.matProps = eyeProperties;
        }
        else
        {
            properties.SetVectorArray(ColourFlashId, colourBuffer);
            parameters.matProps = properties;
        }
        Graphics.RenderMeshInstanced(parameters, batch.Mesh, batch.SubMesh, selectedMatrices, count);
    }

    public void Dispose()
    {
        handle.Complete();

        foreach (Mesh mesh in ownedEyeMeshes)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(mesh);
            else
                UnityEngine.Object.DestroyImmediate(mesh);
        }
        ownedEyeMeshes.Clear();
        variations.Dispose();

        if (transforms.isCreated)
            transforms.Dispose();

        partLocal.Dispose();
        partOnHead.Dispose();
        bodyScales.Dispose();
        headScales.Dispose();
        bodyPoses.Dispose();
        headPoses.Dispose();
        animStates.Dispose();
        colours.Dispose();
        rootPositions.Dispose();
        rootRotations.Dispose();
        physicsRotations.Dispose();
        rootScales.Dispose();

        if (deathCurve.IsCreated)
            deathCurve.Dispose();

        if (emergeCurve.IsCreated)
            emergeCurve.Dispose();
        matrices.Dispose();
    }

    private void Allocate(int size)
    {
        capacity = size;
        rootPositions = new NativeArray<float3>(size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        rootRotations = new NativeArray<quaternion>(size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        physicsRotations = new NativeArray<quaternion>(size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        rootScales = new NativeArray<float>(size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        matrices = new NativeArray<float4x4>(Mathf.Max(1, partCount) * size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    }

    // Render outputs are rebuilt each frame, but combat reads cached positions between frames.
    private void Grow()
    {
        NativeArray<float3> previousPositions = rootPositions;
        rootRotations.Dispose();
        physicsRotations.Dispose();
        rootScales.Dispose();
        matrices.Dispose();
        Allocate(capacity * 2);
        NativeArray<float3>.Copy(previousPositions, rootPositions, goblins.Count);
        previousPositions.Dispose();
    }

    // Spreads neighbouring seeds apart, so goblins spawned one after another don't roll alike
    private static uint Hash(uint value)
    {
        value ^= value >> 16;
        value *= 0x7feb352dU;
        value ^= value >> 15;
        value *= 0x846ca68bU;
        value ^= value >> 16;
        return value;
    }

    // Samples the death ease (DOTween's own maths, via DOVirtual.EasedValue) into the curve the job reads. Only when
    // the ease or overshoot changed, so tweaking it in play mode shows at once and otherwise costs nothing.
    private void BakeDeathCurve()
    {
        if (deathCurve.IsCreated && bakedEase == type.deathEase && bakedOvershoot.Equals(type.deathEaseOvershoot))
            return;

        if (!deathCurve.IsCreated)
            deathCurve = new NativeArray<float>(DeathCurveSamples, Allocator.Persistent);

        for (int i = 0; i < DeathCurveSamples; i++)
        {
            float t = i / (float)(DeathCurveSamples - 1);
            deathCurve[i] = DG.Tweening.DOVirtual.EasedValue(1f, 0f, t, type.deathEase, type.deathEaseOvershoot);
        }

        bakedEase = type.deathEase;
        bakedOvershoot = type.deathEaseOvershoot;
    }

    // The hop's progress (0 to 1) along the ease, sampled for the job; only when the ease changes
    private void BakeEmergeCurve(DG.Tweening.Ease ease)
    {
        if (emergeBaked && bakedEmergeEase == ease)
            return;

        if (!emergeCurve.IsCreated)
            emergeCurve = new NativeArray<float>(DeathCurveSamples, Allocator.Persistent);

        for (int i = 0; i < DeathCurveSamples; i++)
            emergeCurve[i] = DG.Tweening.DOVirtual.EasedValue(0f, 1f, i / (float)(DeathCurveSamples - 1), ease);

        bakedEmergeEase = ease;
        emergeBaked = true;
    }

    // Grows a horizontal box (x, z in the root's space) to take in a mesh's bounds
    private static void Footprint(Bounds bounds, Matrix4x4 toRoot, ref Vector2 min, ref Vector2 max)
    {
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = toRoot.MultiplyPoint3x4(c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z));

            min = Vector2.Min(min, new Vector2(corner.x, corner.z));
            max = Vector2.Max(max, new Vector2(corner.x, corner.z));
        }
    }

    private static void Decompose(Matrix4x4 matrix, out float3 position, out quaternion rotation, out float3 scale)
    {
        position = matrix.GetPosition();
        scale = new float3(matrix.GetColumn(0).magnitude, matrix.GetColumn(1).magnitude, matrix.GetColumn(2).magnitude);
        rotation = matrix.ValidTRS() ? matrix.rotation : Quaternion.identity;
    }
}

[BurstCompile]
public struct Horde_ReadRootsJob : IJobParallelForTransform
{
    [WriteOnly] public NativeArray<float3> Positions;
    [WriteOnly] public NativeArray<quaternion> Rotations;

    public void Execute(int index, TransformAccess transform)
    {
        Positions[index] = transform.position;
        Rotations[index] = transform.rotation;
    }
}

// Every part's world matrix for one goblin. Body and head anchors get the goblin's scale and pose; the head is carried
// by the body. Written part-major so each part's matrices are one run for RenderMeshInstanced.
// [Unity.Collections.ReadOnly] is written out in full: the project also has an inspector ReadOnly attribute.
[BurstCompile]
public struct Horde_PartMatricesJob : IJobParallelFor
{
    [Unity.Collections.ReadOnly] public NativeArray<float3> RootPositions;
    [Unity.Collections.ReadOnly] public NativeArray<quaternion> RootRotations;
    [Unity.Collections.ReadOnly] public NativeArray<float> RootScales;
    [Unity.Collections.ReadOnly] public NativeArray<float3> BodyScales;
    [Unity.Collections.ReadOnly] public NativeArray<float3> HeadScales;
    [Unity.Collections.ReadOnly] public NativeArray<Horde_Pose> BodyPoses;
    [Unity.Collections.ReadOnly] public NativeArray<Horde_Pose> HeadPoses;
    [Unity.Collections.ReadOnly] public NativeArray<float4x4> PartLocal;
    [Unity.Collections.ReadOnly] public NativeArray<byte> PartOnHead;

    public float3 BodyPosition;
    public quaternion BodyRotation;
    public float3 BodyScale;
    public float3 HeadPosition;
    public quaternion HeadRotation;
    public float3 HeadScale;
    public int PartCount;
    public int Capacity;

    [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<float4x4> Matrices;

    public void Execute(int index)
    {
        float4x4 root = float4x4.TRS(RootPositions[index], RootRotations[index], new float3(RootScales[index]));

        // Body: authored, then the goblin's pose (in the body's own space) and scale
        Horde_Pose bodyPose = BodyPoses[index];
        float3 bodyStretch = BodyScales[index] * bodyPose.Scale;
        float3 bodyAt = BodyPosition + math.mul(BodyRotation, bodyPose.Position);
        quaternion bodyTurn = math.mul(BodyRotation, bodyPose.Rotation);
        float4x4 body = math.mul(root, float4x4.TRS(bodyAt, bodyTurn, BodyScale * bodyStretch));

        // Head: its authored offset from the body, in the body's space, stretched by the body and turned with it
        float3 fromBody = math.mul(math.inverse(BodyRotation), HeadPosition - BodyPosition) * bodyStretch;
        Horde_Pose headPose = HeadPoses[index];
        quaternion carried = math.mul(bodyTurn, math.inverse(BodyRotation));
        float3 headAt = bodyAt + math.mul(bodyTurn, fromBody) + math.mul(math.mul(carried, HeadRotation), headPose.Position);
        quaternion headTurn = math.mul(math.mul(carried, HeadRotation), headPose.Rotation);
        float4x4 head = math.mul(root, float4x4.TRS(headAt, headTurn, HeadScale * HeadScales[index] * headPose.Scale));

        for (int p = 0; p < PartCount; p++)
            Matrices[p * Capacity + index] = math.mul(PartOnHead[p] != 0 ? head : body, PartLocal[p]);
    }
}
