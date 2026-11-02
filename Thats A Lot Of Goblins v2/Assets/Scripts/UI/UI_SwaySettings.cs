using UnityEngine;

// How the HUD sways with the plane's flight, in one asset so it can be tuned in play mode. Read by UI_SwayDriver;
// each UI_Sway element scales these with its own weights. Distances are canvas units (reference pixels with a
// Canvas Scaler), angles are degrees.
[CreateAssetMenu(menuName = "Goblins/UI Sway Settings", fileName = "UI_SwaySettings")]
public class UI_SwaySettings : ScriptableObject
{
    [Header("Turn")]
    [Tooltip("Sideways slide at the plane's max turn rate, against the turn (turning right slides the HUD left), as if it has weight. Negative slides with the turn.")]
    public float turnOffset = 10f;

    [Tooltip("Tilt at the max turn rate, leaning into the turn like the plane banks. Negative leans away.")]
    public float turnTilt = 2f;

    [Header("Speed Change")]
    [Tooltip("Push at full acceleration: speeding up presses the HUD down, slowing down lets it float up. Negative flips it. Holding a steady speed doesn't move it.")]
    public float speedOffset = 6f;

    [Tooltip("Acceleration (units per second, per second) that gives the full push.")]
    [Min(0.01f)] public float fullAcceleration = 8f;

    [Tooltip("How quickly the measured acceleration follows the plane, per second. Lower smooths out small speed wobbles.")]
    [Min(0f)] public float accelerationSmoothing = 6f;

    [Header("Dive")]
    [Tooltip("Spread outward from the screen centre while diving, like tunnel vision rushing down. Pulling out springs the HUD back in. Negative pulls it inward.")]
    public float diveSpread = 8f;

    [Tooltip("Descent speed (units per second) that gives the full spread. Levelling out at the low-flight height ends the dive.")]
    [Min(0.01f)] public float fullDiveSpeed = 10f;

    [Header("Springs")]
    [Tooltip("How the slide, push and spread follow their targets. Strength pulls toward the target, damper stops it; a low damper overshoots and wobbles back.")]
    public Core_SpringSettings offsetSpring = new Core_SpringSettings(120f, 12f);

    [Tooltip("How the tilt follows its target.")]
    public Core_SpringSettings tiltSpring = new Core_SpringSettings(120f, 12f);

    [Tooltip("The furthest an element ever moves from its rest position, after its weights. 0 means no limit.")]
    [Min(0f)] public float maxOffset = 30f;
}
