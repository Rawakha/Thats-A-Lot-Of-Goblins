using UnityEngine;

[System.Serializable]
public class SoundConfig
{
    public AudioClip[] audioClips;
    public float priority = 0f;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(-3f, 3f)] public float pitch = 1f;
    public bool loopSound = false;

    [Tooltip("On: every play is a new voice, so rapid repeats overlap (gunfire, impacts). Off: while this clip is " +
             "still playing, new plays are skipped (priority above 0.5 restarts it instead), for sounds that must not stack.")]
    public bool additive = true;

    [Tooltip("0 is 2D: same volume everywhere, no panning (UI, music, the player's own plane). 1 is fully 3D: pans and " +
             "fades with distance from the listener (impacts, explosions). In between mixes the two.")]
    [Range(0f, 1f)] public float spatialBlend = 1f;

    [Header("Sound Variation")]
    public float pitchRange;
    public float volumeRange;

    // False for an empty slot, so callers can skip it quietly instead of the manager warning every play
    public bool HasClips => audioClips != null && audioClips.Length > 0;
}