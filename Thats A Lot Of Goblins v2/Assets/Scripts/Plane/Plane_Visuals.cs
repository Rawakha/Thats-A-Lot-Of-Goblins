using UnityEngine;

// Makes the plane look alive. Purely cosmetic: it only moves the model root and its parts, never the
// Plane_Controller's transform, so aim, the shadow and the camera stay true to the real flight.
//
// Layers, summed onto modelRoot's local pose each frame:
//   bank (turn rate) + pitch (vertical speed) + turn sink + air sway + kicks + squash & stretch
// Air sway drifts the model along a slow wandering path (up / down and side to side) and points it along that path:
// the nose lifts as it rises, it rolls and yaws into its sideways drift. Because the rotation comes from the motion,
// it reads as riding the air rather than bobbing on a spring. A faster, smaller turbulence wobble sits on top.
// Parts: the propeller spins with speed, ailerons and rudder follow the turn.
//
// Kick() is the hook for weapons and hits, e.g. a bomb leaving the plane bumps it upward.
//
// GC: none per frame. One UpdateManager subscription (OnLateUpdate), added on enable.
[RequireComponent(typeof(Plane_Controller))]
public class Plane_Visuals : MonoBehaviour
{
    // Uneven ratios so the layered waves never line up and visibly repeat
    private const float WaveRatioB = 1.618f;
    private const float WaveRatioC = 2.71f;

    [SerializeField] private Plane_VisualSettings settings;

    [Header("Parts")]
    [Tooltip("Empty child holding the whole model. Everything is banked, bobbed and squashed through this.")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Transform propeller;
    [Tooltip("Local axis the propeller spins around.")]
    [SerializeField] private Vector3 propellerAxis = Vector3.forward;
    [SerializeField] private Transform[] leftAilerons;
    [SerializeField] private Transform[] rightAilerons;
    [SerializeField] private Transform rudder;

    private Plane_Controller plane;

    // The animated model: equipment is spawned under it so guns and racks bank, bob and kick with the plane
    public Transform ModelRoot => modelRoot;

    private Vector3 modelBasePosition;
    private Quaternion modelBaseRotation;
    private Vector3 modelBaseScale;
    private Quaternion propellerBase;
    private Quaternion rudderBase;
    private Quaternion[] leftAileronBases;
    private Quaternion[] rightAileronBases;

    private Core_SpringFloatState bank;
    private Core_SpringFloatState pitch;
    private Core_SpringFloatState stretch;
    private Core_SpringFloatState squash;
    private Core_SpringVector3State kickPosition;
    private Core_SpringVector3State kickRotation;

    private float surfaceAngle;
    private float propellerAngle;
    private float time;
    private float seed;
    private float lastSpeed;
    private float lastVerticalSpeed;
    private bool hasLastSpeeds;

    // Adds a jolt that springs back out. Position in units/second, rotation in degrees/second, both in model space
    // (x = right, y = up, z = forward; rotation x = nose down, z = roll left).
    public void Kick(Vector3 positionVelocity, Vector3 rotationVelocity)
    {
        kickPosition.velocity += positionVelocity;
        kickRotation.velocity += rotationVelocity;
    }

    private void Awake()
    {
        plane = GetComponent<Plane_Controller>();

        if (settings == null || modelRoot == null)
        {
            Debug.LogError($"{name}: Plane_Visuals needs a Plane_VisualSettings and a Model Root.", this);
            enabled = false;
            return;
        }

        modelBasePosition = modelRoot.localPosition;
        modelBaseRotation = modelRoot.localRotation;
        modelBaseScale = modelRoot.localScale;

        if (propeller != null)
            propellerBase = propeller.localRotation;

        if (rudder != null)
            rudderBase = rudder.localRotation;

        leftAileronBases = CacheRotations(leftAilerons);
        rightAileronBases = CacheRotations(rightAilerons);

        kickRotation = new Core_SpringVector3State(Vector3.zero);
        kickPosition = new Core_SpringVector3State(Vector3.zero);

        // Different planes shouldn't sway in sync
        seed = Random.value * 100f;
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance == null)
        {
            Debug.LogError($"{name}: No UpdateManager in the scene, the plane visuals won't run.", this);
            return;
        }

        hasLastSpeeds = false;
        UpdateManager.Instance.OnLateUpdate += LateTick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= LateTick;

        ResetPose();
    }

    private void LateTick()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        float maxTurnRate = Mathf.Max(1f, plane.Settings.maxTurnRate);
        float turn = Mathf.Clamp(plane.TurnRate / maxTurnRate, -1f, 1f);    // -1 full left, +1 full right
        float turnAmount = Mathf.Abs(turn);

        // Bank into the turn; a right turn rolls right, which is negative around Z
        float bankTarget = Mathf.Clamp(-plane.TurnRate * settings.bankPerTurnRate, -settings.maxBank, settings.maxBank);
        Core_Math.SpringRatio(ref bank, bankTarget, settings.bankStiffness, settings.bankDampingRatio, deltaTime);

        // Nose follows the climb or dive; descending pitches the nose down, which is positive around X
        float pitchTarget = Mathf.Clamp(-plane.VerticalSpeed * settings.pitchPerVerticalSpeed, -settings.maxPitch, settings.maxPitch);
        Core_Math.SpringRatio(ref pitch, pitchTarget, settings.pitchStiffness, settings.pitchDampingRatio, deltaTime);

        // Air sway, calmed a little in turns and dives (the bank and pitch carry the motion there)
        time += deltaTime;
        float calm = (1f - turnAmount * settings.swayTurnQuiet) * (1f - plane.Dive * settings.swayDiveQuiet);

        float swayFrequency = settings.swayFrequency;
        float sideFrequency = swayFrequency * settings.swaySideFrequency;
        float rise = Wave(time, swayFrequency, seed, out float riseRate) * settings.swayHeight * calm;
        float drift = Wave(time, sideFrequency, seed + 29f, out float driftRate) * settings.swaySide * calm;
        riseRate *= settings.swayHeight * calm;
        driftRate *= settings.swaySide * calm;

        // Point along the path: nose up while rising (negative X), roll and yaw into the sideways drift
        float swayPitch = -riseRate * settings.swayPitch;
        float swayRoll = -driftRate * settings.swayRoll;
        float swayYaw = driftRate * settings.swayYaw;

        float turbulenceRoll = Wave(time, settings.turbulenceFrequency, seed + 17f, out _) * settings.turbulenceRoll * calm;
        float turbulencePitch = Wave(time, settings.turbulenceFrequency * 0.77f, seed + 41f, out _) * settings.turbulencePitch * calm;

        Core_Math.SpringRatio(ref kickPosition, Vector3.zero, settings.kickStiffness, settings.kickDampingRatio, deltaTime);
        Core_Math.SpringRatio(ref kickRotation, Vector3.zero, settings.kickStiffness, settings.kickDampingRatio, deltaTime);

        UpdateSquash(deltaTime);

        Vector3 position = modelBasePosition + kickPosition.value;
        position.x += drift;
        position.y += rise - turnAmount * settings.turnSink;

        Vector3 angles = kickRotation.value;
        angles.x += pitch.value + swayPitch + turbulencePitch;
        angles.y += swayYaw;
        angles.z += bank.value + swayRoll + turbulenceRoll;

        modelRoot.localPosition = position;
        modelRoot.localRotation = modelBaseRotation * Quaternion.Euler(angles);
        modelRoot.localScale = SquashedScale();

        UpdateParts(turn, deltaTime);
    }

    // Stretches along the nose when accelerating, squashes when pulling out of a dive
    private void UpdateSquash(float deltaTime)
    {
        // First frame after enabling has nothing to compare against; don't read it as a huge acceleration
        if (!hasLastSpeeds)
        {
            lastSpeed = plane.Speed;
            lastVerticalSpeed = plane.VerticalSpeed;
            hasLastSpeeds = true;
        }

        float acceleration = (plane.Speed - lastSpeed) / deltaTime;
        float verticalAcceleration = (plane.VerticalSpeed - lastVerticalSpeed) / deltaTime;

        lastSpeed = plane.Speed;
        lastVerticalSpeed = plane.VerticalSpeed;

        float stretchTarget = Mathf.Clamp(acceleration * settings.stretchPerAcceleration, -settings.maxSquash, settings.maxSquash);
        float squashTarget = Mathf.Clamp(-verticalAcceleration * settings.squashPerVerticalAcceleration, -settings.maxSquash, settings.maxSquash);

        Core_Math.SpringRatio(ref stretch, stretchTarget, settings.squashStiffness, settings.squashDampingRatio, deltaTime);
        Core_Math.SpringRatio(ref squash, squashTarget, settings.squashStiffness, settings.squashDampingRatio, deltaTime);
    }

    private Vector3 SquashedScale()
    {
        // Squash is vertical (positive = taller), stretch is along the nose (positive = longer)
        Vector3 scale = Core_Math.SquashScale(modelBaseScale, squash.value, settings.squashWidth);
        float thin = 1f - stretch.value * settings.squashWidth;

        scale.x *= thin;
        scale.y *= thin;
        scale.z *= 1f + stretch.value;

        return scale;
    }

    private void UpdateParts(float turn, float deltaTime)
    {
        if (propeller != null)
        {
            float spin = settings.propellerIdleSpeed + plane.Speed * settings.propellerSpeedPerUnit;
            propellerAngle = Mathf.Repeat(propellerAngle + spin * deltaTime, 360f);
            propeller.localRotation = propellerBase * Quaternion.AngleAxis(propellerAngle, propellerAxis);
        }

        // Surfaces react faster than the bank, so they visibly lead it
        surfaceAngle = Core_Math.Damp(surfaceAngle, turn, settings.surfaceResponse, deltaTime);

        // Right turn: right ailerons trailing edge up, left ones down
        float aileron = surfaceAngle * settings.maxAileron;
        ApplyRotations(rightAilerons, rightAileronBases, Quaternion.Euler(aileron, 0f, 0f));
        ApplyRotations(leftAilerons, leftAileronBases, Quaternion.Euler(-aileron, 0f, 0f));

        // Right turn: rudder trailing edge swings right, which is negative around Y
        if (rudder != null)
            rudder.localRotation = rudderBase * Quaternion.Euler(0f, -surfaceAngle * settings.maxRudder, 0f);
    }

    private void ResetPose()
    {
        if (modelRoot == null || leftAileronBases == null)
            return;

        modelRoot.localPosition = modelBasePosition;
        modelRoot.localRotation = modelBaseRotation;
        modelRoot.localScale = modelBaseScale;

        if (propeller != null)
            propeller.localRotation = propellerBase;

        if (rudder != null)
            rudder.localRotation = rudderBase;

        ApplyRotations(leftAilerons, leftAileronBases, Quaternion.identity);
        ApplyRotations(rightAilerons, rightAileronBases, Quaternion.identity);
    }

    // Three layered sines at uneven ratios, roughly -1 to 1. Rate is how fast it's changing, per second, so motion
    // can be turned into matching rotation.
    private static float Wave(float time, float frequency, float seed, out float rate)
    {
        float omega = frequency * Mathf.PI * 2f;
        float phase = time * omega + seed;
        float phaseB = phase * WaveRatioB + seed;
        float phaseC = phase * WaveRatioC + seed * 2f;

        rate = (Mathf.Cos(phase)
              + Mathf.Cos(phaseB) * 0.5f * WaveRatioB
              + Mathf.Cos(phaseC) * 0.25f * WaveRatioC) * omega / 1.75f;

        return (Mathf.Sin(phase)
              + Mathf.Sin(phaseB) * 0.5f
              + Mathf.Sin(phaseC) * 0.25f) / 1.75f;
    }

    private static Quaternion[] CacheRotations(Transform[] parts)
    {
        if (parts == null)
            return new Quaternion[0];

        Quaternion[] bases = new Quaternion[parts.Length];

        for (int i = 0; i < parts.Length; i++)
            bases[i] = parts[i] != null ? parts[i].localRotation : Quaternion.identity;

        return bases;
    }

    private static void ApplyRotations(Transform[] parts, Quaternion[] bases, Quaternion offset)
    {
        if (parts == null)
            return;

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] != null)
                parts[i].localRotation = bases[i] * offset;
        }
    }
}
