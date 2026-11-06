using UnityEngine;
using UnityEngine.Serialization;

// Builds the shared aim for every weapon.
//   Target mode: Ground or Air. Ground tips the guns down to strafe a point Strafe Distance ahead, so the impact line
//     stays the same distance in front at any height and you can steer it; Air keeps them level. Toggle: the Target
//     Mode input (middle click, gamepad Y) switches between them, and they stay. Hold Descend: Ground only while
//     the plane's descend (Ctrl) is held. Either way the guns swing smoothly between the two.
//   Assist: while a target source has something of the mode's kind (goblins on the ground, planes in the air) in a
//     cone ahead, the aim bends toward it: some of the way in yaw and pitch (Assist Strength), within limits, leading
//     for the plane's speed (shots carry it), and sticking to the same target.
//
// GC: none. No subscription: Equip_Loadout calls Compute each tick. One target query a tick while the assist is on.
[RequireComponent(typeof(Plane_Controller))]
public class Weapon_Aim : MonoBehaviour
{
    public enum Control
    {
        Toggle,
        HoldDescend
    }

    [Header("Target Mode")]
    [Tooltip("Toggle: the Target Mode input (middle click, gamepad Y) switches between ground and air targets, and " +
             "it stays. Hold Descend: ground targets only while descend (Ctrl) is held.")]
    [SerializeField] private Control control = Control.Toggle;

    [Tooltip("Start in ground mode, guns down (Toggle).")]
    [SerializeField, FormerlySerializedAs("startDown")] private bool startOnGround = true;

    [Tooltip("How far ahead the guns hit the ground in ground mode, at cruise height.")]
    [SerializeField, Min(0.1f)] private float strafeDistance = 14f;

    [Tooltip("How far ahead they hit at the low-flight height. Shorter than Strafe Distance shrinks the gap in front of " +
             "the plane you can't hit, so a goblin right by a hole needs a dive. It fades between the two by height.")]
    [SerializeField, Min(0.1f)] private float lowStrafeDistance = 10f;

    [Tooltip("Work the gun angle out from the shots' real path (the plane's forward and diving speed included), so " +
             "they land Strafe Distance ahead at any height and while descending. Off: the barrels point there and the " +
             "plane's speed carries shots further (and diving pulls them shorter).")]
    [SerializeField] private bool compensateSpeed = true;

    [Tooltip("Steepest the guns tip down, degrees.")]
    [SerializeField, Range(0f, 80f)] private float maxStrafePitch = 45f;

    [Tooltip("How quickly the guns tip down, per second.")]
    [SerializeField, Min(0f)] private float aimDownResponse = 6f;

    [Tooltip("How quickly the guns come back level, per second.")]
    [SerializeField, Min(0f)] private float aimUpResponse = 4f;

    [Tooltip("How far ahead the crosshair's points sit when the guns are level (nothing to hit on the ground), metres.")]
    [SerializeField, Min(1f)] private float aimDistance = 60f;

    [Header("Assist")]
    [SerializeField] private bool assist = true;

    [Tooltip("How far ahead the assist looks for targets, metres.")]
    [ShowIf(nameof(assist), true)]
    [SerializeField, Min(1f)] private float assistRange = 70f;

    [Tooltip("Half-angle of the cone (flat, round the plane's heading) it looks in, degrees.")]
    [ShowIf(nameof(assist), true)]
    [SerializeField, Range(1f, 90f)] private float assistAngle = 25f;

    [Tooltip("How much of the way to the target the aim bends: 0 none, 1 dead on.")]
    [ShowIf(nameof(assist), true)]
    [SerializeField, Range(0f, 1f)] private float assistStrength = 0.8f;

    [Tooltip("How quickly the aim swings onto (and off) a target, per second.")]
    [ShowIf(nameof(assist), true)]
    [SerializeField, Min(0f)] private float assistResponse = 12f;

    [Tooltip("Furthest the assist turns the guns sideways from the heading, degrees.")]
    [ShowIf(nameof(assist), true)]
    [SerializeField, Range(0f, 45f)] private float maxAssistYaw = 15f;

    [Tooltip("Furthest the assist tips the guns down, degrees.")]
    [ShowIf(nameof(assist), true)]
    [SerializeField, Range(0f, 80f)] private float maxAssistPitch = 40f;

    [Tooltip("Score bonus for staying on the last target, so the aim doesn't flick between goblins.")]
    [ShowIf(nameof(assist), true)]
    [SerializeField, Min(0f)] private float stickiness = 0.15f;

    [Tooltip("How close a target must be to the last one to count as the same, metres.")]
    [ShowIf(nameof(assist), true)]
    [SerializeField, Min(0f)] private float stickyRadius = 3f;

    [Header("Assist Lead")]
    [Tooltip("Shot speed the lead and the crosshair are worked out for, m/s. Match the machine gun's Projectile Speed.")]
    [SerializeField, Min(1f)] private float shotSpeed = 110f;

    [Tooltip("How much of the plane's velocity shots keep. Match the machine gun's Inherit Velocity.")]
    [SerializeField, Range(0f, 1f)] private float shotInheritVelocity = 1f;

    [Header("Gizmos")]
    [Tooltip("Draw what each setting does, each in its own colour and labelled with its name and value. Shows while " +
             "the plane is selected; Always shows it all the time. Works in edit mode too, from the plane's own position " +
             "and facing.")]
    [SerializeField] private bool drawGizmos = true;

    [ShowIf(nameof(drawGizmos), true)]
    [SerializeField] private bool drawAlways;

    [Tooltip("The assist's search area: Range (cyan arc), Angle (cyan cone), Max Yaw (blue), Max Pitch (magenta) and " +
             "the blind spot under the plane it leaves (red).")]
    [ShowIf(nameof(drawGizmos), true)]
    [SerializeField] private bool drawAssist = true;

    [Tooltip("Ground mode's strafe: where shots land now (yellow), the cruise and low distances (orange marks), and " +
             "where Max Strafe Pitch stops it (red).")]
    [ShowIf(nameof(drawGizmos), true)]
    [SerializeField] private bool drawStrafe = true;

    [Tooltip("In play: where shots really go (white), the assist's bend from the plain aim (cyan), the target (red) " +
             "and its Sticky Radius.")]
    [ShowIf(nameof(drawGizmos), true)]
    [SerializeField] private bool drawLive = true;

    [Tooltip("Name and value labels beside each gizmo.")]
    [ShowIf(nameof(drawGizmos), true)]
    [SerializeField] private bool drawLabels = true;

    private Plane_Controller plane;
    private Plane_Input input;
    private Equip_Loadout loadout;
    private float pitch;
    private float strafe;
    private float assistYaw;
    private bool gunsDown;
    private bool hasTarget;
    private Vector3 lastTarget;
    private Weapon_AimData data;

    public Weapon_AimData Data => data;

    // Ground mode (guns down, hunting ground targets) rather than air mode (level, hunting air targets)
    public bool GroundMode => control == Control.Toggle ? gunsDown : plane.Dive > 0.5f;

    private void Awake()
    {
        plane = GetComponent<Plane_Controller>();
        input = GetComponent<Plane_Input>();
        loadout = GetComponent<Equip_Loadout>();
        gunsDown = startOnGround;
    }

    public void Compute(float deltaTime)
    {
        float floor = Combat_Projectiles.Instance != null ? Combat_Projectiles.Instance.FloorHeight : 0f;
        Vector3 position = transform.position;
        float heading = plane.Heading;

        // Gun angle: 0 level, 1 down, eased at its own speed each way
        float wanted = WantedStrafe();
        strafe = Core_Math.DampAsymmetric(strafe, wanted, aimDownResponse, aimUpResponse, deltaTime);

        float strafePitch = StrafePitch(Mathf.Max(position.y - floor, 0f), heading);
        float wantedPitch = strafePitch * strafe;
        float wantedYaw = 0f;

        // Assist: bend part of the way toward the best target ahead
        hasTarget = assist && assistStrength > 0f && FindTarget(position, heading, out lastTarget);

        if (hasTarget)
        {
            LeadDirection(position, lastTarget, out float targetYaw, out float targetPitch);

            wantedYaw = Mathf.Clamp(Mathf.DeltaAngle(heading, targetYaw), -maxAssistYaw, maxAssistYaw) * assistStrength;
            // Ground targets are only ever aimed down at; air ones can be up or down
            float lowest = GroundMode ? 0f : -maxAssistPitch;
            wantedPitch = Mathf.Lerp(wantedPitch, Mathf.Clamp(targetPitch, lowest, Mathf.Max(maxAssistPitch, wantedPitch)), assistStrength);
        }

        // Off a target the aim eases back with the gun angle's own speed; onto one, at the assist's
        float pitchResponse = hasTarget ? Mathf.Max(assistResponse, aimDownResponse) : (wantedPitch > pitch ? aimDownResponse : aimUpResponse);
        pitch = Core_Math.Damp(pitch, wantedPitch, pitchResponse, deltaTime);
        assistYaw = Core_Math.Damp(assistYaw, wantedYaw, assistResponse, deltaTime);

        data.PlanePosition = position;
        data.PlaneVelocity = plane.Velocity;
        data.Heading = heading;
        data.YawRotation = Quaternion.Euler(0f, heading, 0f);
        data.Pitch = pitch;
        data.AimYaw = heading + assistYaw;
        data.Direction = Quaternion.Euler(pitch, data.AimYaw, 0f) * Vector3.forward;
        data.HasTarget = hasTarget;
        data.TargetPoint = lastTarget;
        data.BelowPoint = new Vector3(position.x, floor, position.z);

        data.HasGroundPoint = data.Direction.y < -0.001f && position.y > floor;
        data.GroundPoint = data.HasGroundPoint
            ? position + data.Direction * ((position.y - floor) / -data.Direction.y)
            : data.BelowPoint;

        // The crosshair: where you're aiming, and where the shots go. Shots carry the plane's velocity as well as the
        // gun's, so they fly ahead of where the barrels point: follow their real path, not the barrels.
        Vector3 unassisted = Quaternion.Euler(strafePitch * strafe, heading, 0f) * Vector3.forward;
        data.AimPoint = AimAlong(position, ShotPath(unassisted), floor);
        data.ShotPoint = AimAlong(position, ShotPath(data.Direction), floor);

        if (loadout != null && loadout.Get(Equip_Slot.Weapon) is Weapon_IAimOverride customAim)
            customAim.RefineAim(ref data);
    }

    // Degrees down the guns tip to land Strafe Distance ahead from this height. With Compensate Speed, for the shot's
    // real velocity: the gun's plus the plane's forward and vertical speed it keeps. Solves
    //   height * (S cos p + forward) = distance * (S sin p - vertical)
    // which is S * R * sin(p - phi) = height * forward + distance * vertical, with R and phi from the height and distance.
    private float StrafePitch(float height, float heading)
    {
        float distance = StrafeDistanceAt(height);
        float pitch = Mathf.Atan2(height, distance);

        if (compensateSpeed && height > 0.01f)
        {
            Vector3 velocity = plane.Velocity * shotInheritVelocity;
            Vector3 ahead = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            float forward = velocity.x * ahead.x + velocity.z * ahead.z;
            float vertical = velocity.y;
            float reach = Mathf.Sqrt(height * height + distance * distance);
            float sine = (height * forward + distance * vertical) / (shotSpeed * reach);

            pitch += Mathf.Asin(Mathf.Clamp(sine, -1f, 1f));
        }

        return Mathf.Clamp(pitch * Mathf.Rad2Deg, 0f, maxStrafePitch);
    }

    // Strafe Distance at cruise height, fading to Low Strafe Distance at the low-flight height
    private float StrafeDistanceAt(float height)
    {
        Plane_Settings flight = plane != null ? plane.Settings : null;

        if (flight == null)
            return strafeDistance;

        float low = Mathf.InverseLerp(flight.cruiseHeight, flight.lowFlightHeight, height);
        return Mathf.Lerp(strafeDistance, lowStrafeDistance, low);
    }

    // The way a shot fired along a gun direction actually flies (Shot Speed and Shot Inherit Velocity, as the gun)
    private Vector3 ShotPath(Vector3 direction)
    {
        Vector3 velocity = direction * shotSpeed + plane.Velocity * shotInheritVelocity;
        return velocity.sqrMagnitude > 0.0001f ? velocity.normalized : direction;
    }

    // Where a direction from the plane meets the ground, or Aim Distance along it if it's level or the ground is
    // further than that
    private Vector3 AimAlong(Vector3 from, Vector3 direction, float floor)
    {
        if (direction.y < -0.001f && from.y > floor)
        {
            float distance = (from.y - floor) / -direction.y;

            if (distance <= aimDistance * 2f)
                return from + direction * distance;
        }

        return from + direction * aimDistance;
    }

    // 1 for guns down (ground), 0 for level (air). Toggle flips on each Target Mode press; Hold Descend follows descend.
    private float WantedStrafe()
    {
        if (control == Control.HoldDescend)
            return plane.Dive;

        if (input != null && input.TargetModePressed)
            gunsDown = !gunsDown;

        return gunsDown ? 1f : 0f;
    }

    private bool FindTarget(Vector3 position, float heading, out Vector3 point)
    {
        Combat_TargetQuery query = new Combat_TargetQuery
        {
            Kinds = GroundMode ? Combat_TargetKind.Ground : Combat_TargetKind.Air,
            Origin = position,
            Forward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward,
            Range = assistRange,
            CosAngle = Mathf.Cos(assistAngle * Mathf.Deg2Rad),
            HasPrevious = hasTarget,
            Previous = lastTarget,
            StickyRadius = stickyRadius,
            Sticky = stickiness
        };

        return Combat_Targets.Find(query, out point);
    }

    // The direction to fire so the shot, which also carries the plane's velocity, meets the target: as yaw and pitch
    // (+ down), degrees
    private void LeadDirection(Vector3 from, Vector3 target, out float yaw, out float pitchDown)
    {
        Vector3 offset = target - from;
        float time = offset.magnitude / shotSpeed;
        Vector3 fire = time > 0.0001f ? offset / time - plane.Velocity * shotInheritVelocity : offset;

        if (fire.sqrMagnitude < 0.0001f)
            fire = offset;

        fire.Normalize();
        yaw = Mathf.Atan2(fire.x, fire.z) * Mathf.Rad2Deg;
        pitchDown = -Mathf.Asin(Mathf.Clamp(fire.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (drawAlways)
            DrawAim();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAlways)
            DrawAim();
    }

    private static readonly Color RangeColour = new Color(0.3f, 0.9f, 1f, 0.9f);
    private static readonly Color YawColour = new Color(0.35f, 0.5f, 1f, 0.9f);
    private static readonly Color PitchColour = new Color(1f, 0.35f, 1f, 0.9f);
    private static readonly Color BlindColour = new Color(1f, 0.25f, 0.25f, 0.9f);
    private static readonly Color StrafeColour = new Color(1f, 0.9f, 0.2f, 0.95f);
    private static readonly Color StrafeMarkColour = new Color(1f, 0.6f, 0.15f, 0.9f);
    private static readonly Color ShotColour = new Color(1f, 1f, 1f, 0.95f);
    private static readonly Color TargetColour = new Color(1f, 0.2f, 0.2f, 0.95f);

    // Each setting drawn where it acts, in its own colour, with its name and value beside it
    private void DrawAim()
    {
        if (!drawGizmos)
            return;

        // Edit mode: no Awake yet, so find the plane for its flight heights (the low/cruise strafe fade)
        if (plane == null)
            plane = GetComponent<Plane_Controller>();

        bool live = Application.isPlaying && plane != null;
        Vector3 position = live ? data.PlanePosition : transform.position;
        float heading = live ? data.Heading : transform.eulerAngles.y;
        float floor = Combat_Projectiles.Instance != null ? Combat_Projectiles.Instance.FloorHeight : 0f;
        float height = Mathf.Max(position.y - floor, 0f);
        Vector3 below = new Vector3(position.x, floor, position.z);
        Vector3 forward = Debug_Gizmos.Direction(heading);
        Vector3 lift = Vector3.up * 0.6f;   // labels float a little above what they name

        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        Gizmos.DrawLine(position, below);

        if (drawAssist && assist)
            DrawAssistArea(position, below, heading, forward, height, lift);

        if (drawStrafe)
            DrawStrafe(position, below, forward, height, live, lift);

        if (drawLive && live)
            DrawLiveAim(lift);
    }

    private void DrawAssistArea(Vector3 position, Vector3 below, float heading, Vector3 forward, float height, Vector3 lift)
    {
        // Range is measured from the plane, so on the ground it reaches less far the higher the plane flies
        float reach = height < assistRange ? Mathf.Sqrt(assistRange * assistRange - height * height) : 0f;

        Gizmos.color = RangeColour;
        Debug_Gizmos.Wedge(below, heading, assistAngle, reach);

        Vector3 yawLeft = Debug_Gizmos.Direction(heading - maxAssistYaw);
        Vector3 yawRight = Debug_Gizmos.Direction(heading + maxAssistYaw);
        Gizmos.color = YawColour;
        Gizmos.DrawLine(below, below + yawLeft * reach);
        Gizmos.DrawLine(below, below + yawRight * reach);

        // The steepest it may tip the guns: anything closer in than where that meets the ground can't be aimed at
        float closest = maxAssistPitch > 0.1f && height > 0f ? height / Mathf.Tan(maxAssistPitch * Mathf.Deg2Rad) : 0f;
        Vector3 pitchPoint = below + forward * closest;

        if (closest > 0f)
        {
            Gizmos.color = PitchColour;
            Gizmos.DrawLine(position, pitchPoint);

            Gizmos.color = BlindColour;
            Gizmos.DrawLine(below, pitchPoint);
            Gizmos.DrawWireSphere(pitchPoint, 0.3f);
        }

        if (!drawLabels)
            return;

        Debug_Gizmos.Label(below + forward * reach + lift, $"Assist Range {assistRange:0} m (reaches {reach:0} m on the ground)", RangeColour);
        Debug_Gizmos.Label(below + Debug_Gizmos.Direction(heading - assistAngle) * reach * 0.75f + lift, $"Assist Angle {assistAngle:0}°", RangeColour);
        Debug_Gizmos.Label(below + yawRight * reach * 0.55f + lift, $"Max Yaw {maxAssistYaw:0}°", YawColour);

        if (closest > 0f)
        {
            Debug_Gizmos.Label(Vector3.Lerp(position, pitchPoint, 0.5f) + lift, $"Max Pitch {maxAssistPitch:0}°", PitchColour);
            Debug_Gizmos.Label(below + forward * (closest * 0.5f) + lift, $"Blind spot {closest:0.0} m", BlindColour);
        }

        Debug_Gizmos.Label(position + Vector3.up * 2f, $"Assist strength {assistStrength:0.00}, response {assistResponse:0}/s", RangeColour);
    }

    private void DrawStrafe(Vector3 position, Vector3 below, Vector3 forward, float height, bool live, Vector3 lift)
    {
        // Where shots land now: in play from the real shot path (no assist), in edit mode from the height here
        float here = StrafeDistanceAt(height);
        float steepest = height > 0f ? height / Mathf.Tan(maxStrafePitch * Mathf.Deg2Rad) : 0f;
        Vector3 landing = live ? new Vector3(data.AimPoint.x, below.y, data.AimPoint.z) : below + forward * Mathf.Max(here, steepest);

        Gizmos.color = StrafeColour;
        Gizmos.DrawLine(position, landing);
        Gizmos.DrawWireSphere(landing, 0.75f);

        // The two ends it fades between, marked on the ground ahead
        Vector3 cruiseMark = below + forward * strafeDistance;
        Vector3 lowMark = below + forward * lowStrafeDistance;
        Gizmos.color = StrafeMarkColour;
        Gizmos.DrawWireCube(cruiseMark, new Vector3(0.4f, 0.05f, 0.4f));
        Gizmos.DrawWireCube(lowMark, new Vector3(0.4f, 0.05f, 0.4f));

        // Max Strafe Pitch: from this height the guns can't strafe closer than this
        bool pitchLimited = steepest > here;

        if (pitchLimited)
        {
            Gizmos.color = BlindColour;
            Gizmos.DrawWireSphere(below + forward * steepest, 0.3f);
        }

        if (!drawLabels)
            return;

        float landed = Vector3.Distance(below, landing);
        Debug_Gizmos.Label(landing + lift * 2f, $"Lands {landed:0.0} m ahead", StrafeColour);
        Debug_Gizmos.Label(cruiseMark + lift, $"Strafe {strafeDistance:0} m (cruise)", StrafeMarkColour);
        Debug_Gizmos.Label(lowMark + lift, $"Low Strafe {lowStrafeDistance:0} m (low)", StrafeMarkColour);

        if (pitchLimited)
            Debug_Gizmos.Label(below + forward * steepest + lift * 3f, $"Max Strafe Pitch {maxStrafePitch:0}° stops it here", BlindColour);
    }

    private void DrawLiveAim(Vector3 lift)
    {
        // Where shots really go (assist included), and how far the assist bent them from the plain aim
        Gizmos.color = ShotColour;
        Gizmos.DrawLine(data.PlanePosition, data.ShotPoint);
        Gizmos.DrawWireSphere(data.ShotPoint, 0.5f);

        Gizmos.color = RangeColour;
        Gizmos.DrawLine(data.AimPoint, data.ShotPoint);

        if (drawLabels)
            Debug_Gizmos.Label(data.ShotPoint + lift, "Shots go here", ShotColour);

        if (!data.HasTarget)
            return;

        Gizmos.color = TargetColour;
        Gizmos.DrawWireSphere(data.TargetPoint, 0.6f);
        Gizmos.DrawLine(data.PlanePosition, data.TargetPoint);
        Debug_Gizmos.Circle(data.TargetPoint, stickyRadius);

        if (drawLabels)
            Debug_Gizmos.Label(data.TargetPoint + Vector3.right * stickyRadius + lift, $"Target (Sticky {stickyRadius:0.0} m, +{stickiness:0.00})", TargetColour);
    }
#endif
}
