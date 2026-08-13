using UnityEngine;

[System.Serializable]
public class SoundConfig
{
    public AudioClip[] audioClips;
    public float priority = 0f;
    public float volume = 0f;
    public bool loopSound = false;

    [Header("Sound Variation")]
    public float pitchRange;
    public float volumeRange;
}