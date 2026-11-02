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

    [Tooltip("How far ahead the guns hit the ground in ground mode.")]
    [SerializeField, Min(0.1f)] private float strafeDistance = 14f;

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
    [SerializeField, Min(1f)] private float assistRange = 70f;

    [Tooltip("Half-angle of the cone (flat, round the plane's heading) it looks in, degrees.")]
    [SerializeField, Range(1f, 90f)] private float assistAngle = 25f;

    [Tooltip("How much of the way to the target the aim bends: 0 none, 1 dead on.")]
    [SerializeField, Range(0f, 1f)] private float assistStrength = 0.8f;

    [Tooltip("How quickly the aim swings onto (and off) a target, per second.")]
    [SerializeField, Min(0f)] private float assistResponse = 12f;

    [Tooltip("Furthest the assist turns the guns sideways from the heading, degrees.")]
    [SerializeField, Range(0f, 45f)] private float maxAssistYaw = 15f;

    [Tooltip("Furthest the assist tips the guns down, degrees.")]
    [SerializeField, Range(0f, 80f)] private float maxAssistPitch = 40f;

    [Tooltip("Score bonus for staying on the last target, so the aim doesn't flick between goblins.")]
    [SerializeField, Min(0f)] private float stickiness = 0.15f;

    [Tooltip("How close a target must be to the last one to count as the same, metres.")]
    [SerializeField, Min(0f)] private float stickyRadius = 3f;

    [Header("Assist Lead")]
    [Tooltip("Shot speed the lead and the crosshair are worked out for, m/s. Match the machine gun's Projectile Speed.")]
    [SerializeField, Min(1f)] private float shotSpeed = 110f;

    [Tooltip("How much of the plane's velocity shots keep. Match the machine gun's Inherit Velocity.")]
    [SerializeField, Range(0f, 1f)] private float shotInheritVelocity = 1f;

    [Header("Gizmos")]
    [Tooltip("Draw the assist's cone and range on the ground (cyan), its yaw limits (blue), its steepest pitch " +
             "(magenta), the strafe point (yellow) and, in play, the aim and target (red). Shows while the plane is " +
             "selected; Always shows it all the time. Works in edit mode too, from the plane's own position and facing.")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawAlways;

    private Plane_Controller plane;
    private Plane_Input input;
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

        float strafePitch = Mathf.Min(Mathf.Atan2(Mathf.Max(position.y - floor, 0f), strafeDistance) * Mathf.Rad2Deg, maxStrafePitch);
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

    // The assist's reach on the ground (cyan: cone and range, blue: how far it may turn the guns), the steepest it
    // may tip them (magenta), the strafe point (yellow) and, in play, the aim line and current target (red)
    private void DrawAim()
    {
        if (!drawGizmos)
            return;

        bool live = Application.isPlaying && plane != null;
        Vector3 position = live ? data.PlanePosition : transform.position;
        float heading = live ? data.Heading : transform.eulerAngles.y;
        float floor = Combat_Projectiles.Instance != null ? Combat_Projectiles.Instance.FloorHeight : 0f;
        float height = Mathf.Max(position.y - floor, 0f);
        Vector3 below = new Vector3(position.x, floor, position.z);
        Vector3 forward = Debug_Gizmos.Direction(heading);

        if (assist)
        {
            // Range is measured from the plane, so on the ground it reaches less far the higher the plane flies
            float reach = height < assistRange ? Mathf.Sqrt(assistRange * assistRange - height * height) : 0f;

            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
            Debug_Gizmos.Wedge(below, heading, assistAngle, reach);

            Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.8f);
            Gizmos.DrawLine(below, below + Debug_Gizmos.Direction(heading - maxAssistYaw) * reach);
            Gizmos.DrawLine(below, below + Debug_Gizmos.Direction(heading + maxAssistYaw) * reach);

            // The steepest the assist may tip the guns, down to where that meets the ground
            if (maxAssistPitch > 0.1f && height > 0f)
            {
                float along = height / Mathf.Tan(maxAssistPitch * Mathf.Deg2Rad);
                Gizmos.color = new Color(1f, 0.3f, 1f, 0.8f);
                Gizmos.DrawLine(position, below + forward * along);
            }
        }

        // Where ground mode strafes
        float strafePitch = Mathf.Min(Mathf.Atan2(height, strafeDistance) * Mathf.Rad2Deg, maxStrafePitch);
        float strafeAlong = strafePitch > 0.1f ? height / Mathf.Tan(strafePitch * Mathf.Deg2Rad) : strafeDistance;
        Vector3 strafePoint = below + forward * strafeAlong;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(strafePoint, 0.75f);

        if (!live)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.35f);
            Gizmos.DrawLine(position, strafePoint);
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(data.PlanePosition, data.HasGroundPoint ? data.GroundPoint : data.PlanePosition + data.Direction * 40f);

        if (data.HasGroundPoint)
            Gizmos.DrawWireSphere(data.GroundPoint, 0.5f);

        if (data.HasTarget)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(data.TargetPoint, 0.6f);
            Gizmos.DrawLine(data.PlanePosition, data.TargetPoint);
        }
    }
#endif
}
