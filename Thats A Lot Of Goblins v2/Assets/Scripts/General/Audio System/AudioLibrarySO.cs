using UnityEngine;

[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Audio/Audio Library")]
public class AudioLibrarySO : ScriptableObject
{
    public AudioClip[] audioClips;
}