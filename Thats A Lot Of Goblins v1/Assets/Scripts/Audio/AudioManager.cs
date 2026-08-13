using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : GameManagerBase
{
    public static AudioManager Instance;

    [Header("Audio Source Pool")]
    [SerializeField] private AudioSource audioSourcePrefab;
    [SerializeField] private int audioSourcePoolSize = 15;

    [Header("Sound Settings")]
    [SerializeField] private float maxDistance = 50f;
    [SerializeField] private int maxSimultaneousSounds = 15;
    [SerializeField] private Transform listenerTransform;

    [Header("Debugging")]
    [SerializeField] private bool useDebugGizmos;
    [SerializeField] private float gizmoSize = 0.1f;

    private Queue<AudioSource> audioSources;
    private List<SoundRequest> soundRequests;
    private AudioSource preview;

    private Dictionary<AudioClip, AudioSource> playingSounds;

    private void Awake()
    {
        preview = gameObject.AddComponent<AudioSource>();
        preview.playOnAwake = false;
    }

    private void Start()
    {
        if (listenerTransform == null)
            listenerTransform = Camera.main.transform;
    }

    protected override bool OnInitialize(GameLevelBootstrap levelBootstrap)
    {
        if (Instance != null)
        {
            Debug.LogError("More than one Audio Manager in Scene: " + Instance.name);
            return false;
        }

        Instance = this;
        soundRequests = new List<SoundRequest>();
        playingSounds = new Dictionary<AudioClip, AudioSource>();

        InitializePool();

        return true;
    }

    private void Update()
    {
        ProcessSoundRequests();
    }

    private void InitializePool()
    {
        audioSources = new Queue<AudioSource>();

        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            AudioSource audioSource = Instantiate(audioSourcePrefab, transform);
            audioSource.gameObject.SetActive(false);
            audioSources.Enqueue(audioSource);
        }
    }

    public void RequestSound(AudioClip clip, Vector3 position, float priority = 0f, float volume = 1f, float pitchRange = 0f, float volumeRange = 0f, bool loopSound = false, bool additive = false)
    {
        if (clip == null || Vector3.Distance(position, listenerTransform.position) > maxDistance) return;

        soundRequests.Add(new SoundRequest(clip, position, priority, loopSound, volume, pitchRange, volumeRange));
    }

    public void RequestSound(SoundConfig soundConfig, Vector3 position, bool additive = false)
    {
        var clip = soundConfig.audioClips.Length > 0 ? soundConfig.audioClips[Random.Range(0, soundConfig.audioClips.Length)] : null;

        if (clip == null)
        {
            Debug.LogWarning("Sound or AudioClip is null. Skipping sound request.");
            return;
        }

        if (Vector3.Distance(position, listenerTransform.position) > maxDistance)
        {
            Debug.LogWarning("Sound is too far from listener. Skipping sound request.");
            return;
        }

        soundRequests.Add(new SoundRequest(clip, position, soundConfig.priority, soundConfig.loopSound, soundConfig.volume, soundConfig.pitchRange, soundConfig.volumeRange, additive));
    }

    private void ProcessSoundRequests()
    {
        soundRequests.Sort((a, b) => b.priority.CompareTo(a.priority));

        int soundsToPlay = (int)Mathf.Min(soundRequests.Count, maxSimultaneousSounds);

        for (int i = 0; i < soundsToPlay; i++)
        {
            PlaySound(
                soundRequests[i].audioClip,
                soundRequests[i].position,
                soundRequests[i].priority,
                soundRequests[i].loopSound,
                soundRequests[i].volume,
                soundRequests[i].pitchRange,
                soundRequests[i].volumeRange,
                soundRequests[i].additive
            );
        }

        soundRequests.Clear();
    }

    private void PlaySound(AudioClip clip, Vector3 position, float priority, bool loopSound, float volume, float pitchRange, float volumeRange, bool additive = false)
    {
        if (!additive)
        {
            if (playingSounds.TryGetValue(clip, out var existingSource) && existingSource != null && existingSource.isPlaying)
            {
                if (priority > 0.5f)
                {
                    existingSource.Stop();
                    existingSource.transform.position = position;
                    existingSource.loop = loopSound;
                    existingSource.volume = Mathf.Clamp(volume + Random.Range(-volumeRange, volumeRange), 0f, 1f);
                    existingSource.pitch = 1f + Random.Range(-pitchRange, pitchRange);
                    existingSource.Play();
                }
                return;
            }
        }

        if (audioSources.Count == 0) return;

        AudioSource audioSource = audioSources.Dequeue();
        audioSource.transform.position = position;
        audioSource.clip = clip;
        audioSource.loop = loopSound;
        audioSource.volume = Mathf.Clamp(volume + Random.Range(-volumeRange, volumeRange), 0f, 1f);
        audioSource.pitch = 1f + Random.Range(-pitchRange, pitchRange);
        audioSource.gameObject.SetActive(true);
        audioSource.Play();

        playingSounds[clip] = audioSource;

        if (!loopSound)
        {
            StartCoroutine(ReturnToPoolAfterPlaying(audioSource, clip, !additive));
        }
    }

    private IEnumerator ReturnToPoolAfterPlaying(AudioSource audioSource, AudioClip clip, bool trackInDictionary)
    {
        yield return new WaitWhile(() => audioSource.isPlaying);
        audioSource.gameObject.SetActive(false);
        audioSources.Enqueue(audioSource);

        if (trackInDictionary)
            playingSounds.Remove(clip);
    }

    public void StopSound(AudioClip clip)
    {
        if (playingSounds.ContainsKey(clip))
        {
            AudioSource audioSource = playingSounds[clip];
            audioSource.Stop();
            audioSource.gameObject.SetActive(false);
            audioSources.Enqueue(audioSource);
            playingSounds.Remove(clip);
        }
    }

    private class SoundRequest
    {
        public AudioClip audioClip;
        public Vector3 position;
        public float priority;
        public bool loopSound;
        public float volume;
        public float pitchRange;
        public float volumeRange;
        public bool additive;

        public SoundRequest(AudioClip audioClip, Vector3 position, float priority = 0f, bool loopSound = false, float volume = 1f, float pitchRange = 0f, float volumeRange = 0f, bool additive = false)
        {
            this.audioClip = audioClip;
            this.position = position;
            this.priority = priority;
            this.loopSound = loopSound;
            this.volume = volume;
            this.pitchRange = pitchRange;
            this.volumeRange = volumeRange;
            this.additive = additive;
        }
    }

    public void Preview(AudioClip clip, float volume, bool loop, float priority, float pitchVar, float volumeVar)
    {
        if (!clip) return;
        preview.loop = loop;
        preview.volume = Mathf.Clamp01(volume + UnityEngine.Random.Range(-volumeVar, volumeVar));
        preview.pitch = 1f + UnityEngine.Random.Range(-pitchVar, pitchVar);
        preview.priority = Mathf.Clamp((int)priority, 0, 256);
        preview.clip = clip;
        preview.Play();
    }

    public void StopPreview() => preview?.Stop();

    private void OnDrawGizmos()
    {
        if (Application.isPlaying == false || !useDebugGizmos) return;

        Gizmos.color = Color.yellow;
        foreach (var sound in playingSounds)
        {
            Gizmos.DrawWireSphere(sound.Value.transform.position, gizmoSize);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (listenerTransform != null)
        {
            Gizmos.color = Color.grey;
            Gizmos.DrawWireSphere(listenerTransform.position, maxDistance);
        }
    }
}