using UnityEngine;

public class PhysicsAudioPlayer : MonoBehaviour
{
    [SerializeField] private Rigidbody body;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip audioClip;

    [Header("Movement Detection")]
    [SerializeField] private float minAngularSpeed = 0.05f;
    [SerializeField] private float maxAngularSpeed = 2.5f;

    [Header("Volume")]
    [SerializeField] private float maxVolume = 0.8f;
    [SerializeField] private float volumeFadeSpeed = 8f;

    [Header("Pitch")]
    [SerializeField] private float minPitch = 0.85f;
    [SerializeField] private float maxPitch = 1.15f;

    private void Reset()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (body == null)
            body = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.clip = audioClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0f;
    }

    private void Update()
    {
        if (body == null || audioSource == null || audioClip == null)
            return;

        float angularSpeed = body.angularVelocity.magnitude;
        float speed01 = Mathf.InverseLerp(minAngularSpeed, maxAngularSpeed, angularSpeed);

        float targetVolume = speed01 * maxVolume;
        audioSource.volume = Mathf.MoveTowards(audioSource.volume, targetVolume, volumeFadeSpeed * Time.deltaTime);
        audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, speed01);

        if (audioSource.volume > 0.01f)
        {
            if (!audioSource.isPlaying)
                audioSource.Play();
        }
        else
        {
            if (audioSource.isPlaying)
                audioSource.Stop();
        }
    }
}