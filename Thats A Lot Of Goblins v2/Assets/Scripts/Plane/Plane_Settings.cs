using UnityEngine;

// Every flight tunable in one asset, so it can be tuned in play mode and the values stick.
[CreateAssetMenu(menuName = "Goblins/Plane Settings", fileName = "Plane_Settings")]
public class Plane_Settings : ScriptableObject
{
    [Header("Speed")]
    [Tooltip("Top speed, reached when flying straight.")]
    [Min(0f)] public float straightSpeed = 22f;

    [Tooltip("Speed when turning at the max turn rate.")]
    [Min(0f)] public float turningSpeed = 13f;

    [Tooltip("How much of a turn it takes to slow down. 1 slows evenly with the turn, higher only slows in hard turns.")]
    [Min(0.1f)] public float turnSlowdownCurve = 1.5f;

    [Tooltip("How quickly the plane picks up speed on a straight, per second.")]
    [Min(0f)] public float acceleration = 1.2f;

    [Tooltip("How quickly the plane bleeds speed going into a turn, per second. Higher than acceleration feels punchier.")]
    [Min(0f)] public float deceleration = 3f;

    [Header("Turning")]
    [Tooltip("Fastest the plane can turn, degrees per second.")]
    [Min(0f)] public float maxTurnRate = 140f;

    [Tooltip("Turn rate asked for per degree of heading error. Higher commits to turns harder, lower gives lazier arcs.")]
    [Min(0f)] public float turnGain = 4f;

    [Tooltip("How quickly the turn rate reaches what's asked for, per second. Higher is snappier, lower feels heavier.")]
    [Min(0f)] public float turnResponse = 8f;

    [Tooltip("How quickly the direction of travel catches up with the nose, per second. Lower gives more drift in turns.")]
    [Min(0f)] public float driftRate = 6f;

    [Tooltip("How quickly overspeed from descending bleeds off, per second. Low lets you carry a descent's speed into the next straight.")]
    [Min(0f)] public float overspeedBleed = 0.6f;

    [Header("Altitude")]
    public float cruiseHeight = 8f;

    [Tooltip("Height descend brings the plane down to and holds it at: low enough to fly through supply loops.")]
    public float lowFlightHeight = 2.5f;

    [Tooltip("Fastest the plane sinks with the descend button fully held, units per second.")]
    [Min(0f)] public float descentRate = 3f;

    [Tooltip("Fastest the plane climbs back to cruise height when descend is released, units per second.")]
    [Min(0f)] public float climbRate = 2f;

    [Tooltip("Climb speed per unit below cruise height, so the climb eases in as it nears cruise.")]
    [Min(0f)] public float climbGain = 1f;

    [Tooltip("How quickly the vertical speed changes, per second. Lower makes descents and climbs ease in more.")]
    [Min(0f)] public float verticalResponse = 4f;

    [Tooltip("Extra speed while descending at the full rate. Trading height for speed is the skill.")]
    [Min(0f)] public float descentSpeedBonus = 6f;
}
