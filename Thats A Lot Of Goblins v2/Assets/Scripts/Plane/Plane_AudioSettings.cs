using UnityEngine;
using UnityEngine.Audio;

// Every plane sound and how it reacts to the flight, in one asset so it can be tuned in play mode.
// Loops use their Sound Data's first clip, volume, pitch and spatial blend as the base; the rest is driven by the
// plane. Keep the loops' spatial blend low (about 0.2) so your own plane stays present in both ears.
// Empty slots are skipped, so the plane works with any subset of sounds.
[CreateAssetMenu(menuName = "Goblins/Plane Audio Settings", fileName = "Plane_AudioSettings")]
public class Plane_AudioSettings : ScriptableObject
{
    [Header("Mix")]
    [Tooltip("Optional mixer group for every plane loop.")]
    public AudioMixerGroup mixerGroup;

    [Tooltip("Seconds the loops take to fade in when the plane spawns.")]
    [Min(0f)] public float fadeInTime = 1f;

    [Header("Engine")]
    [Tooltip("Low engine loop, loudest at low speed.")]
    public SoundData engineLow;
    [Tooltip("High engine loop, crossfaded in toward top speed. Optional.")]
    public SoundData engineHigh;

    [Tooltip("Engine pitch at a standstill and at top speed (straight speed + descent bonus).")]
    public float enginePitchIdle = 0.75f;
    public float enginePitchTop = 1.3f;

    [Tooltip("Extra pitch per unit/second² of acceleration: the engine revs up on straights and eases off into turns.")]
    public float enginePitchPerAcceleration = 0.03f;
    [Tooltip("Extra pitch while descending at the full rate: the dive whine.")]
    public float engineDescentPitch = 0.12f;
    [Tooltip("How quickly the engine pitch follows, per second. Lower sounds heavier.")]
    [Min(0f)] public float engineResponse = 5f;

    [Header("Wind")]
    [Tooltip("Air rushing past, louder with speed.")]
    public SoundData wind;
    [Tooltip("Wind volume multiplier at the low-flight height. Skimming the ground should roar.")]
    [Min(0f)] public float windLowBoost = 1.6f;
    [Tooltip("Extra wind volume in a full-rate turn, as a fraction.")]
    [Min(0f)] public float windTurnBoost = 0.5f;
    public float windPitchSlow = 0.8f;
    public float windPitchTop = 1.2f;
    [Min(0f)] public float windResponse = 4f;

    [Header("Turn Whoosh")]
    [Tooltip("Played when the plane snaps into a hard turn.")]
    public SoundData turnWhoosh;
    [Tooltip("Turn amount (0-1 of max turn rate) that triggers the whoosh.")]
    [Range(0f, 1f)] public float whooshThreshold = 0.65f;
    [Tooltip("Turn amount it has to drop below before it can whoosh again, so holding a turn doesn't repeat it.")]
    [Range(0f, 1f)] public float whooshReset = 0.3f;
    [Min(0f)] public float whooshCooldown = 0.6f;
}
