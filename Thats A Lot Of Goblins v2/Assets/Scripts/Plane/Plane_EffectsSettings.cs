using UnityEngine;

// How the plane's flight effects react to the flight, in one asset so it can be tuned in play mode.
// The effects themselves (particle system, trail renderers) are authored in the scene; their own rate, width and
// colours are the "full strength" look, and these values decide how much of it shows.
// Speed amounts are 0-1 of top speed (straight speed + descent bonus).
[CreateAssetMenu(menuName = "Goblins/Plane Effects Settings", fileName = "Plane_EffectsSettings")]
public class Plane_EffectsSettings : ScriptableObject
{
    [Header("Speed Lines")]
    [Tooltip("Speed amount where speed lines start to appear. They reach full rate at top speed.")]
    [Range(0f, 1f)] public float speedLinesStart = 0.5f;

    [Tooltip("Rate multiplier at the low-flight height. Skimming the ground should streak.")]
    [Min(0f)] public float speedLinesLowBoost = 1.5f;

    [Tooltip("Extra rate, as a fraction of full, with descend fully held.")]
    [Min(0f)] public float speedLinesDiveBoost = 0.5f;

    [Tooltip("How quickly the speed lines follow, per second.")]
    [Min(0f)] public float speedLinesResponse = 6f;

    [Header("Wing Trails")]
    [Tooltip("Speed amount where the wing trails start. They're full at top speed.")]
    [Range(0f, 1f)] public float trailSpeedStart = 0.6f;

    [Tooltip("Turn amount (0-1 of max turn rate) where hard turns start pulling trails, whatever the speed.")]
    [Range(0f, 1f)] public float trailTurnStart = 0.4f;

    [Tooltip("Extra strength on the wing on the outside of the turn, which is moving faster. 0 keeps both even.")]
    [Min(0f)] public float trailOuterBoost = 0.5f;

    [Tooltip("Strength the trails stop emitting below. What's already drawn fades out on its own.")]
    [Range(0f, 1f)] public float trailMinStrength = 0.05f;

    [Tooltip("How quickly the trails follow, per second.")]
    [Min(0f)] public float trailResponse = 8f;
}
