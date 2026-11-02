using UnityEngine;

[CreateAssetMenu(fileName = "SoundData", menuName = "Audio/Sound Data")]
public class SoundData : ScriptableObject
{
    public SoundConfig soundConfig;

    // The config to play from a SoundData slot, or null when the slot is empty or has no clips (skip it quietly)
    public static SoundConfig Get(SoundData data)
    {
        return data != null && data.soundConfig != null && data.soundConfig.HasClips ? data.soundConfig : null;
    }
}