using UnityEngine;

// Every Camera_Rig tunable in one asset, so the camera can be tuned in play mode and the values stick.
// Speed and height ranges come from the plane's own settings, so retuning the plane keeps the camera in step.
[CreateAssetMenu(menuName = "Goblins/Camera Settings", fileName = "Camera_Settings")]
public class Camera_Settings : ScriptableObject
{
    [Header("Base Framing")]
    [Tooltip("Pitch at cruise height and normal speed. The world never rotates; only the framing changes.")]
    [Range(10f, 90f)] public float pitch = 45f;
    public float yaw = 0f;
    [Min(1f)] public float distance = 40f;
    [Range(10f, 120f)] public float fieldOfView = 60f;

    [Tooltip("0 frames the ground under the plane, 1 frames the plane itself and follows its height.")]
    [Range(0f, 1f)] public float heightFollow = 0.3f;

    [Header("Speed")]
    [Tooltip("Extra distance at top speed, from the plane's turning speed (none) to its straight speed (all).")]
    public float speedDistance = 6f;
    [Tooltip("Extra field of view at top speed.")]
    public float speedFieldOfView = 5f;

    [Tooltip("Extra field of view for overspeed from descending, at the full descent bonus. Shows the speed you earned.")]
    public float overspeedFieldOfView = 5f;
    [Tooltip("Extra distance for overspeed at the full descent bonus.")]
    public float overspeedDistance = 2f;

    [Header("Low Flight")]
    [Tooltip("Pitch change when flying at ground height. Negative flattens the view so the ground races at you.")]
    public float lowPitch = -6f;
    [Tooltip("Distance change at ground height. Negative moves in close.")]
    public float lowDistance = -8f;
    [Tooltip("How quickly low framing comes in as the plane sinks. 1 is even, higher saves it for very low flight.")]
    [Min(0.1f)] public float lowCurve = 1.5f;

    [Header("Framing Spring")]
    [Tooltip("How quickly distance, pitch, field of view and roll follow their targets.")]
    [Min(0f)] public float framingStiffness = 30f;
    [Range(0f, 2f)] public float framingDampingRatio = 0.9f;

    [Header("Follow")]
    [Tooltip("How tightly the camera follows the plane. Lower lags more under acceleration, which shows the speed change.")]
    [Min(0f)] public float followStiffness = 200f;
    [Tooltip("Below 1 the camera overshoots slightly when the plane brakes into a turn.")]
    [Range(0f, 2f)] public float followDampingRatio = 0.85f;

    [Header("Lead")]
    [Tooltip("Seconds of travel to look ahead of the plane.")]
    [Min(0f)] public float velocityLead = 0.6f;
    [Tooltip("Units to look toward where the player is steering, so you see where you're about to strafe.")]
    [Min(0f)] public float inputLead = 3f;
    [Tooltip("How quickly the lead swings. Low makes the frame swing out through turns and settle on the straight.")]
    [Min(0f)] public float leadStiffness = 20f;
    [Range(0f, 2f)] public float leadDampingRatio = 0.75f;

    [Header("Heading Bias")]
    [Tooltip("Degrees the camera yaws toward the plane's heading when flying sideways across the screen, so more of " +
             "the screen is ahead of it. Flying straight up or down the screen adds none. 0 keeps the world fixed.")]
    [Range(0f, 30f)] public float headingYaw = 10f;
    [Tooltip("How quickly the yaw follows. Keep it low so the world drifts round rather than swings.")]
    [Min(0f)] public float headingStiffness = 2f;
    [Range(0f, 2f)] public float headingDampingRatio = 1f;

    [Header("Turn Lean")]
    [Tooltip("Degrees of camera roll per degree/second of turn. Flip the sign if it leans the wrong way. 0 turns it off.")]
    public float rollPerTurnRate = 0.01f;
    [Range(0f, 10f)] public float maxRoll = 1.5f;

    [Header("Shake")]
    [Tooltip("Trauma lost per second. Shake is trauma squared, so small hits barely show and big ones punch.")]
    [Min(0f)] public float traumaDecay = 1.5f;
    [Tooltip("Position shake at full trauma, in units.")]
    [Min(0f)] public float maxShakeOffset = 0.6f;
    [Tooltip("Rotation shake at full trauma, in degrees.")]
    [Min(0f)] public float maxShakeAngle = 2f;
    [Min(0f)] public float shakeFrequency = 18f;

    [Header("Speed Rumble")]
    [Tooltip("Constant fine shake at top speed near the ground, as a fraction of full shake.")]
    [Range(0f, 1f)] public float rumble = 0.12f;
    [Tooltip("How much of the rumble is kept at cruise height. Skimming the ground should hum, cruising shouldn't.")]
    [Range(0f, 1f)] public float rumbleHighScale = 0.15f;
    [Min(0f)] public float rumbleFrequency = 32f;

    [Header("Kicks")]
    [Tooltip("Spring that settles kicks from shots and explosions.")]
    [Min(0f)] public float kickStiffness = 150f;
    [Range(0f, 2f)] public float kickDampingRatio = 0.45f;
}
