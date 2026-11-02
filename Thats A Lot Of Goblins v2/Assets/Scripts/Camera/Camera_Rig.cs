using UnityEngine;

// The plane camera. The world barely rotates; the rig mostly changes how it frames the plane, in layers:
//   1. Framing: distance, pitch and field of view from speed and height. Faster pulls out and widens, overspeed
//      from a descent widens more, flying low flattens the view and moves in close.
//   2. Follow: a spring on the plane, so the camera lags under acceleration and overshoots slightly on braking.
//   3. Lead: its own softer spring, toward the direction of travel and where the player is steering, so the
//      frame swings out through turns and settles on straights.
//   4. Lean: a small roll into turns.
//   4b. Heading bias: a few degrees of slow yaw toward the plane's heading, so more of the screen is ahead of it.
//       Uses the sine of the heading, so flying toward the camera adds nothing rather than flipping side to side.
//   5. Shake and kicks: trauma (squared, Perlin) plus a constant speed rumble near the ground, and a kick spring.
//
// Anything can shake or kick the camera through the static Shake / Kick, without a reference to the rig.
//
// GC: none per frame. One UpdateManager subscription (OnLateUpdate), added on enable.
[RequireComponent(typeof(Camera))]
public class Camera_Rig : MonoBehaviour
{
    [SerializeField] private Plane_Controller target;
    [SerializeField] private Camera_Settings settings;

    private Camera view;
    private Plane_Input input;

    private Core_SpringVector3State follow;
    private Core_SpringVector3State lead;
    private Core_SpringVector3State kick;
    private Core_SpringFloatState distance;
    private Core_SpringFloatState pitch;
    private Core_SpringFloatState fieldOfView;
    private Core_SpringFloatState roll;
    private Core_SpringFloatState yaw;
    private float trauma;

    // The rig shakes and kicks go to: the most recently enabled one
    public static Camera_Rig Active { get; private set; }

    public float Trauma => trauma;

    // Adds trauma, 0-1. Shake is trauma squared: 0.1 is a tap, 0.5 a thump, 1 an explosion on top of you.
    public static void Shake(float amount)
    {
        if (Active != null)
            Active.trauma = Mathf.Clamp01(Active.trauma + amount);
    }

    // Pushes the camera, world units/second. It springs back.
    public static void Kick(Vector3 velocity)
    {
        if (Active != null)
            Active.kick.velocity += velocity;
    }

    private void Awake()
    {
        view = GetComponent<Camera>();

        if (target != null)
            input = target.GetComponent<Plane_Input>();
    }

    private void OnEnable()
    {
        if (target == null || settings == null)
        {
            Debug.LogError($"{name}: Camera_Rig needs a target and Camera_Settings.", this);
            return;
        }

        if (UpdateManager.Instance == null)
        {
            Debug.LogError($"{name}: No UpdateManager in the scene, the camera won't follow.", this);
            return;
        }

        Active = this;
        Snap();
        UpdateManager.Instance.OnLateUpdate += LateTick;
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= LateTick;
    }

    // Jumps straight to the wanted framing, e.g. on spawn or after a teleport
    public void Snap()
    {
        Framing(out float wantedDistance, out float wantedPitch, out float wantedFieldOfView, out float wantedRoll);

        follow = new Core_SpringVector3State(FollowPoint());
        lead = new Core_SpringVector3State(LeadOffset());
        kick = new Core_SpringVector3State(Vector3.zero);
        distance = new Core_SpringFloatState(wantedDistance);
        pitch = new Core_SpringFloatState(wantedPitch);
        fieldOfView = new Core_SpringFloatState(wantedFieldOfView);
        roll = new Core_SpringFloatState(wantedRoll);
        yaw = new Core_SpringFloatState(HeadingYaw());
        trauma = 0f;

        Apply(Vector3.zero, Vector3.zero);
    }

    private void LateTick()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        Framing(out float wantedDistance, out float wantedPitch, out float wantedFieldOfView, out float wantedRoll);

        float stiffness = settings.framingStiffness;
        float ratio = settings.framingDampingRatio;
        Core_Math.SpringRatio(ref distance, wantedDistance, stiffness, ratio, deltaTime);
        Core_Math.SpringRatio(ref pitch, wantedPitch, stiffness, ratio, deltaTime);
        Core_Math.SpringRatio(ref fieldOfView, wantedFieldOfView, stiffness, ratio, deltaTime);
        Core_Math.SpringRatio(ref roll, wantedRoll, stiffness, ratio, deltaTime);
        Core_Math.SpringRatio(ref yaw, HeadingYaw(), settings.headingStiffness, settings.headingDampingRatio, deltaTime);

        Core_Math.SpringRatio(ref follow, FollowPoint(), settings.followStiffness, settings.followDampingRatio, deltaTime);
        Core_Math.SpringRatio(ref lead, LeadOffset(), settings.leadStiffness, settings.leadDampingRatio, deltaTime);
        Core_Math.SpringRatio(ref kick, Vector3.zero, settings.kickStiffness, settings.kickDampingRatio, deltaTime);

        trauma = Mathf.Max(0f, trauma - settings.traumaDecay * deltaTime);
        ComputeShake(out Vector3 shakeOffset, out Vector3 shakeAngles);

        Apply(shakeOffset, shakeAngles);
    }

    private void Framing(out float wantedDistance, out float wantedPitch, out float wantedFieldOfView, out float wantedRoll)
    {
        Plane_Settings plane = target.Settings;
        float speed = target.Speed;

        float speedAmount = Mathf.InverseLerp(plane.turningSpeed, plane.straightSpeed, speed);
        float overspeed = plane.descentSpeedBonus > 0f
            ? Mathf.InverseLerp(plane.straightSpeed, plane.straightSpeed + plane.descentSpeedBonus, speed)
            : 0f;
        float low = LowAmount();

        wantedDistance = settings.distance
            + settings.speedDistance * speedAmount + settings.overspeedDistance * overspeed
            + settings.lowDistance * low;

        wantedPitch = settings.pitch + settings.lowPitch * low;

        wantedFieldOfView = settings.fieldOfView
            + settings.speedFieldOfView * speedAmount + settings.overspeedFieldOfView * overspeed;

        wantedRoll = Mathf.Clamp(-target.TurnRate * settings.rollPerTurnRate, -settings.maxRoll, settings.maxRoll);
    }

    // 0 at cruise height, 1 at the low-flight height, curved so most of it comes in near the bottom
    private float LowAmount()
    {
        Plane_Settings plane = target.Settings;
        float amount = Mathf.InverseLerp(plane.cruiseHeight, plane.lowFlightHeight, target.Altitude);

        return Mathf.Pow(amount, settings.lowCurve);
    }

    // Base yaw plus up to headingYaw toward the heading: most when crossing the screen, none flying up or down it
    private float HeadingYaw()
    {
        float across = Mathf.Sin(Mathf.DeltaAngle(settings.yaw, target.Heading) * Mathf.Deg2Rad);
        return settings.yaw + across * settings.headingYaw;
    }

    private Vector3 FollowPoint()
    {
        Vector3 position = target.transform.position;
        return new Vector3(position.x, position.y * settings.heightFollow, position.z);
    }

    private Vector3 LeadOffset()
    {
        Vector3 steer = input != null ? input.Direction : target.transform.forward;
        return Core_Math.Flatten(target.Velocity) * settings.velocityLead + Core_Math.Flatten(steer) * settings.inputLead;
    }

    private void ComputeShake(out Vector3 offset, out Vector3 angles)
    {
        float time = Time.time;

        // Trauma: squared so it punches
        float shake = trauma * trauma;
        float shakeTime = time * settings.shakeFrequency;
        offset = new Vector3(Noise(0f, shakeTime), Noise(10f, shakeTime), 0f) * (settings.maxShakeOffset * shake);
        angles = new Vector3(Noise(20f, shakeTime), Noise(30f, shakeTime), Noise(40f, shakeTime)) * (settings.maxShakeAngle * shake);

        // Rumble: a constant fine hum that grows with speed and closeness to the ground
        float rumble = Rumble();

        if (rumble <= 0f)
            return;

        float rumbleTime = time * settings.rumbleFrequency;
        offset += new Vector3(Noise(50f, rumbleTime), Noise(60f, rumbleTime), 0f) * (settings.maxShakeOffset * rumble);
        angles += new Vector3(Noise(70f, rumbleTime), 0f, Noise(80f, rumbleTime)) * (settings.maxShakeAngle * rumble);
    }

    private float Rumble()
    {
        Plane_Settings plane = target.Settings;
        float topSpeed = plane.straightSpeed + plane.descentSpeedBonus;
        float speedAmount = topSpeed > 0f ? Mathf.Clamp01(target.Speed / topSpeed) : 0f;
        return settings.rumble * speedAmount * Mathf.Lerp(settings.rumbleHighScale, 1f, LowAmount());
    }

    private void Apply(Vector3 shakeOffset, Vector3 shakeAngles)
    {
        Quaternion frame = Quaternion.Euler(pitch.value, yaw.value, 0f);
        Quaternion rotation = Quaternion.Euler(pitch.value + shakeAngles.x, yaw.value + shakeAngles.y, roll.value + shakeAngles.z);
        Vector3 focus = follow.value + lead.value;
        Vector3 position = focus - frame * Vector3.forward * distance.value + kick.value + frame * shakeOffset;

        transform.SetPositionAndRotation(position, rotation);
        view.fieldOfView = fieldOfView.value;
    }

    // -1 to 1, smooth over time
    private static float Noise(float seed, float time)
    {
        return Mathf.PerlinNoise(seed, time) * 2f - 1f;
    }
}
