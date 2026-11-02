using UnityEngine;

// Every Plane_Visuals tunable in one asset, so the feel can be tuned in play mode and the values stick.
[CreateAssetMenu(menuName = "Goblins/Plane Visual Settings", fileName = "Plane_VisualSettings")]
public class Plane_VisualSettings : ScriptableObject
{
    [Header("Bank")]
    [Tooltip("Degrees of roll per degree/second of turn. Higher leans harder into turns.")]
    [Min(0f)] public float bankPerTurnRate = 0.35f;
    [Range(0f, 90f)] public float maxBank = 55f;

    [Tooltip("How fast the plane rolls into a bank.")]
    [Min(0f)] public float bankStiffness = 120f;

    [Tooltip("1 rolls in with no overshoot. Lower swings past level when coming out of a turn, which reads as weight.")]
    [Range(0f, 2f)] public float bankDampingRatio = 0.45f;

    [Header("Pitch")]
    [Tooltip("Degrees of nose down per unit/second of descent (and nose up when climbing).")]
    [Min(0f)] public float pitchPerVerticalSpeed = 2.5f;
    [Range(0f, 90f)] public float maxPitch = 30f;
    [Min(0f)] public float pitchStiffness = 150f;
    [Range(0f, 2f)] public float pitchDampingRatio = 0.55f;

    [Header("Turn Sink")]
    [Tooltip("How far the model drops in a full-rate turn, so hard turns feel like they pull G.")]
    [Min(0f)] public float turnSink = 0.25f;

    [Header("Air Sway")]
    [Tooltip("How far the model drifts up and down, in units. Built from three uneven waves so it never visibly repeats.")]
    [Min(0f)] public float swayHeight = 0.4f;
    [Tooltip("How far the model drifts side to side, in units.")]
    [Min(0f)] public float swaySide = 0.3f;

    [Tooltip("Up-and-down sways per second. Slow (0.2-0.4) reads as floating on air.")]
    [Min(0f)] public float swayFrequency = 0.3f;
    [Tooltip("Side-to-side speed as a fraction of the up-and-down one. Not 1, so the two never sync into a loop.")]
    [Min(0f)] public float swaySideFrequency = 0.7f;

    [Tooltip("Degrees of nose up per unit/second of rise, so the nose leads the plane up and over each swell.")]
    [Min(0f)] public float swayPitch = 6f;
    [Tooltip("Degrees of roll per unit/second of sideways drift: it leans into where it's drifting.")]
    [Min(0f)] public float swayRoll = 14f;
    [Tooltip("Degrees of yaw per unit/second of sideways drift: the nose points slightly into the drift.")]
    [Min(0f)] public float swayYaw = 5f;

    [Header("Turbulence")]
    [Tooltip("Small, quicker roll wobble on top of the sway, degrees.")]
    [Min(0f)] public float turbulenceRoll = 1.5f;
    [Min(0f)] public float turbulencePitch = 0.8f;
    [Min(0f)] public float turbulenceFrequency = 0.9f;

    [Header("Sway Calming")]
    [Tooltip("How much sway is removed in a full-rate turn (0 none, 1 all). The bank carries the motion there.")]
    [Range(0f, 1f)] public float swayTurnQuiet = 0.3f;

    [Tooltip("How much sway is removed in a full descent, so low passes feel steadier.")]
    [Range(0f, 1f)] public float swayDiveQuiet = 0.4f;

    [Header("Squash & Stretch")]
    [Tooltip("Stretch along the nose per unit/second² of acceleration. Braking into a turn squashes it.")]
    [Min(0f)] public float stretchPerAcceleration = 0.012f;

    [Tooltip("Squash per unit/second² of vertical acceleration. Pulling out of a dive squashes, tipping into one stretches.")]
    [Min(0f)] public float squashPerVerticalAcceleration = 0.0006f;

    [Tooltip("Largest squash or stretch, as a fraction of the model's size.")]
    [Range(0f, 0.5f)] public float maxSquash = 0.12f;

    [Tooltip("How much thinner the model gets per unit of stretch, to roughly keep its volume.")]
    [Range(0f, 1f)] public float squashWidth = 0.5f;
    [Min(0f)] public float squashStiffness = 250f;
    [Range(0f, 2f)] public float squashDampingRatio = 0.4f;

    [Header("Kicks")]
    [Tooltip("Spring that settles kicks (bomb drops, hits). Low damping makes them wobble out.")]
    [Min(0f)] public float kickStiffness = 200f;
    [Range(0f, 2f)] public float kickDampingRatio = 0.35f;

    [Header("Propeller")]
    [Tooltip("Degrees per second when the plane is barely moving.")]
    [Min(0f)] public float propellerIdleSpeed = 1200f;

    [Tooltip("Extra degrees per second per unit of speed. Keep the top end under ~5000 or it starts to strobe.")]
    [Min(0f)] public float propellerSpeedPerUnit = 60f;

    [Header("Control Surfaces")]
    [Tooltip("Aileron angle at full-rate turns. Make it negative if they flap the wrong way on your model.")]
    public float maxAileron = 25f;
    [Tooltip("Rudder angle at full-rate turns. Make it negative if it swings the wrong way.")]
    public float maxRudder = 20f;

    [Tooltip("How quickly the control surfaces follow the turn, per second.")]
    [Min(0f)] public float surfaceResponse = 14f;
}
