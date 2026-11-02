using UnityEngine;

// The pilot in the open cockpit. Purely cosmetic, like Plane_Visuals: it only rotates and offsets the body and head
// from their authored pose. Layers:
//   Head look: turns toward where the player is steering on a stiff spring, so it gets there before the plane and
//     leads the turn. Tips down when descending, rolls a little against the bank.
//   Inertia: the parent's real acceleration is measured each frame (turns, speed changes, sway, recoil kicks, all of
//     it), and the body leans and slides against it on a spring: thrown outward in turns, back when speeding up.
//   Head wobble: the head gets its own looser inertia spring on top and lags the model's pitch and roll, so it
//     bobbles after every jolt.
//   Breathing and wind buffet: a slow rise and nod, and a fine shake that grows with speed and low flight.
//
// Pivots: rotation happens around each transform's pivot. Put the body's at the hips / seat and the head's at the neck
// (wrap the meshes in empty parents if their pivots are elsewhere).
//
// Kick() is the hook for hits and crashes.
//
// GC: none per frame. One UpdateManager subscription (OnLateUpdate), added on enable.
[RequireComponent(typeof(Plane_Controller))]
public class Plane_Pilot : MonoBehaviour
{
    [SerializeField] private Plane_PilotSettings settings;

    [Header("Parts")]
    [Tooltip("The pilot's body. Leans and slides with inertia.")]
    [SerializeField] private Transform body;
    [Tooltip("The pilot's head. Looks into turns and wobbles. Can be a child of the body.")]
    [SerializeField] private Transform head;

    private Plane_Controller plane;
    private Plane_Input input;

    private Vector3 bodyBasePosition;
    private Quaternion bodyBaseRotation;
    private Quaternion headBaseRotation;

    private Core_SpringVector3State look;
    private Core_SpringVector3State bodyLean;
    private Core_SpringVector3State bodyShift;
    private Core_SpringVector3State headWobble;

    private Vector3 lastPosition;
    private Vector3 lastVelocity;
    private Quaternion lastRotation;
    private Vector3 acceleration;
    private int framesMeasured;
    private float time;
    private float seed;

    // Jolts the pilot, degrees/second in the parent's space (x = nod forward, y = turn right, z = lean left).
    // Body and head both take it; the head's looser spring makes it bobble.
    public void Kick(Vector3 rotationVelocity)
    {
        bodyLean.velocity += rotationVelocity;
        headWobble.velocity += rotationVelocity * 1.5f;
    }

    private void Awake()
    {
        plane = GetComponent<Plane_Controller>();
        input = GetComponent<Plane_Input>();

        if (settings == null || body == null || head == null)
        {
            Debug.LogError($"{name}: Plane_Pilot needs Plane_PilotSettings, a body and a head.", this);
            enabled = false;
            return;
        }

        bodyBasePosition = body.localPosition;
        bodyBaseRotation = body.localRotation;
        headBaseRotation = head.localRotation;
        seed = Random.value * 100f;
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance == null)
        {
            Debug.LogError($"{name}: No UpdateManager in the scene, the pilot won't move.", this);
            return;
        }

        framesMeasured = 0;
        acceleration = Vector3.zero;
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

        time += deltaTime;
        Transform parent = body.parent != null ? body.parent : transform;

        Measure(parent, deltaTime, out Vector3 localAcceleration, out Vector3 localAngularVelocity);

        Plane_Settings flight = plane.Settings;
        float maxTurnRate = Mathf.Max(1f, flight.maxTurnRate);
        float turn = Mathf.Clamp(plane.TurnRate / maxTurnRate, -1f, 1f);

        // Head look: toward the steering direction, plus a bit more while turning, down in a dive, level-ish in a bank
        float steer = 0f;

        if (input != null && input.Direction.sqrMagnitude > 0.0001f)
        {
            float targetHeading = Mathf.Atan2(input.Direction.x, input.Direction.z) * Mathf.Rad2Deg;
            steer = Mathf.DeltaAngle(plane.Heading, targetHeading);
        }

        float lookYaw = Mathf.Clamp(steer * settings.lookPerDegree + plane.TurnRate * settings.lookPerTurnRate,
            -settings.maxLookYaw, settings.maxLookYaw);
        Vector3 lookTarget = new Vector3(plane.Dive * settings.diveLookDown, lookYaw, turn * settings.counterRoll);
        Core_Math.SpringRatio(ref look, lookTarget, settings.lookStiffness, settings.lookDampingRatio, deltaTime);

        // Inertia: lean against the acceleration. Accelerating right throws the body left (+Z),
        // accelerating forward pushes it back (-X), accelerating up squashes it down.
        Vector3 leanTarget = ClampMagnitude(
            new Vector3(-localAcceleration.z, 0f, localAcceleration.x) * settings.bodyLeanPerAcceleration, settings.maxBodyLean);
        Vector3 shiftTarget = ClampMagnitude(-localAcceleration * settings.bodyShiftPerAcceleration, settings.maxBodyShift);
        Core_Math.SpringRatio(ref bodyLean, leanTarget, settings.bodyStiffness, settings.bodyDampingRatio, deltaTime);
        Core_Math.SpringRatio(ref bodyShift, shiftTarget, settings.bodyStiffness, settings.bodyDampingRatio, deltaTime);

        // Head wobble: the same push but looser, and left behind when the model pitches or rolls (not yaw: the head leads that)
        Vector3 wobbleTarget = new Vector3(-localAcceleration.z, 0f, localAcceleration.x) * settings.headLeanPerAcceleration
            - new Vector3(localAngularVelocity.x, 0f, localAngularVelocity.z) * settings.headRotationLag;
        wobbleTarget = ClampMagnitude(wobbleTarget, settings.maxHeadLean);
        Core_Math.SpringRatio(ref headWobble, wobbleTarget, settings.headWobbleStiffness, settings.headWobbleDampingRatio, deltaTime);

        // Breathing
        float breath = Mathf.Sin(time * settings.breathRate * Mathf.PI * 2f + seed);

        // Buffet: fine shake with speed, stronger skimming the ground
        float topSpeed = Mathf.Max(0.01f, flight.straightSpeed + flight.descentSpeedBonus);
        float speed01 = Mathf.Clamp01(plane.Speed / topSpeed);
        float low = Mathf.InverseLerp(flight.cruiseHeight, flight.lowFlightHeight, plane.Altitude);
        float buffet = settings.buffetAngle * speed01 * speed01 * Mathf.Lerp(1f, settings.buffetLowBoost, low);
        float buffetTime = time * settings.buffetFrequency;
        Vector3 buffetAngles = new Vector3(Noise(buffetTime, seed), Noise(buffetTime, seed + 31f) * 0.5f, Noise(buffetTime, seed + 67f)) * buffet;

        body.localPosition = bodyBasePosition + bodyShift.value + new Vector3(0f, breath * settings.breathHeight, 0f);
        body.localRotation = bodyBaseRotation * Quaternion.Euler(bodyLean.value);

        Vector3 headAngles = look.value + headWobble.value + buffetAngles;
        headAngles.x += breath * settings.breathNod;
        head.localRotation = headBaseRotation * Quaternion.Euler(headAngles);
    }

    // The parent's acceleration and angular velocity in its own space, from how it actually moved.
    // Needs two frames of history; until then reads as still.
    private void Measure(Transform parent, float deltaTime, out Vector3 localAcceleration, out Vector3 localAngularVelocity)
    {
        Vector3 position = parent.position;
        Quaternion rotation = parent.rotation;
        Vector3 angularVelocity = Vector3.zero;

        if (framesMeasured == 0)
        {
            framesMeasured = 1;
        }
        else
        {
            Vector3 velocity = (position - lastPosition) / deltaTime;

            (rotation * Quaternion.Inverse(lastRotation)).ToAngleAxis(out float angle, out Vector3 axis);

            if (angle > 180f)
                angle -= 360f;

            if (!float.IsNaN(axis.x) && !float.IsInfinity(axis.x))
                angularVelocity = axis * (angle / deltaTime);    // degrees/second

            // The first velocity has nothing to compare against
            if (framesMeasured == 1)
                framesMeasured = 2;
            else
                acceleration = Core_Math.Damp(acceleration, (velocity - lastVelocity) / deltaTime, settings.accelerationSmoothing, deltaTime);

            lastVelocity = velocity;
        }

        lastPosition = position;
        lastRotation = rotation;

        localAcceleration = parent.InverseTransformDirection(acceleration);
        localAngularVelocity = parent.InverseTransformDirection(angularVelocity);
    }

    private void ResetPose()
    {
        if (body == null || head == null)
            return;

        body.localPosition = bodyBasePosition;
        body.localRotation = bodyBaseRotation;
        head.localRotation = headBaseRotation;
    }

    private static Vector3 ClampMagnitude(Vector3 value, float max)
    {
        float sqr = value.sqrMagnitude;
        return sqr > max * max ? value * (max / Mathf.Sqrt(sqr)) : value;
    }

    // Smooth random in roughly -1 to 1
    private static float Noise(float time, float seed)
    {
        return Mathf.PerlinNoise(time, seed) * 2f - 1f;
    }
}
