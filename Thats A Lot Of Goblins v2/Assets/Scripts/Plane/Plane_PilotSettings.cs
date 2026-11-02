using UnityEngine;

// How the pilot moves, in one asset so it can be tuned in play mode.
// Angles are degrees, offsets are units in the pilot's parent space, accelerations are units/second².
[CreateAssetMenu(menuName = "Goblins/Plane Pilot Settings", fileName = "Plane_PilotSettings")]
public class Plane_PilotSettings : ScriptableObject
{
    [Header("Head Look")]
    [Tooltip("Head yaw per degree between where the plane points and where the player is steering. " +
             "The head gets there long before the plane does, so it leads the turn.")]
    [Min(0f)] public float lookPerDegree = 0.6f;

    [Tooltip("Extra head yaw per degree/second of turn, so a held turn keeps the head looking into it.")]
    [Min(0f)] public float lookPerTurnRate = 0.15f;

    [Tooltip("Furthest the head turns either way.")]
    [Range(0f, 120f)] public float maxLookYaw = 55f;

    [Tooltip("Head tips down this far with descend fully held, watching the ground.")]
    [Range(0f, 60f)] public float diveLookDown = 18f;

    [Tooltip("Head roll against the bank at a full-rate turn, keeping it nearer the horizon like a real pilot.")]
    [Range(0f, 45f)] public float counterRoll = 12f;

    [Tooltip("Snappier than the plane's turn so the head visibly leads.")]
    [Min(0f)] public float lookStiffness = 160f;
    [Range(0f, 2f)] public float lookDampingRatio = 0.6f;

    [Header("Inertia")]
    [Tooltip("How quickly the measured acceleration settles, per second. Lower smooths out jitter but reacts later.")]
    [Min(0f)] public float accelerationSmoothing = 15f;

    [Tooltip("Body lean per unit/second² of acceleration: thrown outward in turns, back when speeding up, " +
             "forward when slowing.")]
    [Min(0f)] public float bodyLeanPerAcceleration = 0.25f;
    [Range(0f, 45f)] public float maxBodyLean = 14f;

    [Tooltip("Body slide per unit/second² of acceleration, squashing into the seat when pulling out of a dive.")]
    [Min(0f)] public float bodyShiftPerAcceleration = 0.0015f;
    [Min(0f)] public float maxBodyShift = 0.06f;

    [Min(0f)] public float bodyStiffness = 120f;
    [Range(0f, 2f)] public float bodyDampingRatio = 0.45f;

    [Header("Head Wobble")]
    [Tooltip("Head lean per unit/second² of acceleration, on top of the body. Loose and bouncy, like a bobblehead.")]
    [Min(0f)] public float headLeanPerAcceleration = 0.35f;
    [Range(0f, 45f)] public float maxHeadLean = 20f;

    [Tooltip("Seconds the head lags the plane's pitch and roll: when the model rocks (sway, recoil, kicks) the head " +
             "is left behind and springs back.")]
    [Min(0f)] public float headRotationLag = 0.05f;

    [Min(0f)] public float headWobbleStiffness = 90f;
    [Range(0f, 2f)] public float headWobbleDampingRatio = 0.3f;

    [Header("Breathing")]
    [Tooltip("Height of the slow breathing rise on the body, in units.")]
    [Min(0f)] public float breathHeight = 0.012f;
    [Tooltip("Breaths per second.")]
    [Min(0f)] public float breathRate = 0.3f;
    [Tooltip("Head nod with each breath, degrees.")]
    [Min(0f)] public float breathNod = 1.5f;

    [Header("Wind Buffet")]
    [Tooltip("Head shake at top speed, degrees. The air hitting an open cockpit.")]
    [Min(0f)] public float buffetAngle = 1.2f;
    [Tooltip("Extra buffet multiplier at the low-flight height.")]
    [Min(0f)] public float buffetLowBoost = 1.8f;
    [Min(0f)] public float buffetFrequency = 9f;
}
