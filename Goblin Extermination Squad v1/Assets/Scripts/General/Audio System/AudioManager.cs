using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

// Pooled AudioSources for every sound in the game.
//   One-shots: RequestSound queues a request; once a frame the highest-priority ones (up to maxSimultaneousSounds)
//     are played. A playing one-shot is returned to the pool when its clip has finished.
//   Tracked: RequestTracked plays a sound that follows a transform; adjust or stop it by handle.
//   Rented: RentSource hands a source out for the caller to drive (engine loops), ReturnSource takes it back.
//
// Sources stay active the whole time and are only played and stopped: toggling GameObjects on every sound is far
// more expensive than a stopped AudioSource.
//
// GC: none per sound. Requests, playing one-shots and tracked sounds live in arrays sized once in Awake; finished
// one-shots are found by end time, no coroutines. PlayFaded allocates (DOTween callback). One UpdateManager
// subscription (OnUpdate).
[DefaultExecutionOrder(Utilities.ExecutionOrder.DependantSingletons)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Source Pool")]
    [SerializeField] private AudioSource audioSourcePrefab;
    [SerializeField] private int audioSourcePoolSize = 15;

    [Header("Sound Settings")]
    [SerializeField] private float maxDistance = 50f;
    [Tooltip("Most one-shots started per frame. The highest priorities win.")]
    [SerializeField] private int maxSimultaneousSounds = 15;
    [Tooltip("Most requests queued per frame. Extra requests in a frame are dropped.")]
    [SerializeField, Min(1)] private int maxRequestsPerFrame = 64;
    [SerializeField, Min(0)] private int maxTrackedSounds = 10;
    [SerializeField] private Transform listenerTransform;

    [Header("Debugging")]
    [SerializeField] private bool useDebugGizmos;
    [SerializeField] private float gizmoSize = 0.1f;

    private struct SoundRequest
    {
        public AudioClip Clip;
        public Vector3 Position;
        public float Priority;
        public bool Loop;
        public float Volume;
        public float Pitch;
        public float PitchRange;
        public float VolumeRange;
        public float SpatialBlend;
        public bool Additive;
    }

    private struct PlayingSound
    {
        public AudioSource Source;
        public AudioClip Clip;
        public float EndTime;           // unscaled time the clip should have finished; checked against isPlaying after
        public bool InDictionary;       // non-additive sounds are found by clip so repeats can be skipped
        public float Priority;          // for stealing: when every source is busy, the lowest goes first
        public float StartTime;
    }

    private struct TrackedSound
    {
        public int Handle;
        public AudioSource Source;
        public Transform Follow;
        public float BaseVolume;
    }

    #region Private Fields
    private Stack<AudioSource> audioSources;
    private SoundRequest[] requests;
    private int requestCount;
    private PlayingSound[] playing;
    private int playingCount;
    private TrackedSound[] tracked;
    private int trackedCount;
    private AudioSource preview;

    private readonly Dictionary<AudioClip, AudioSource> playingSounds = new Dictionary<AudioClip, AudioSource>();
    private readonly HashSet<AudioSource> rentedSources = new HashSet<AudioSource>();
    private int nextTrackedHandle = 1;
    #endregion

    #region Initialize
    private void Awake()
    {
        if (!OnInitialize())
        {
            enabled = false;
            return;
        }

        preview = gameObject.AddComponent<AudioSource>();
        preview.playOnAwake = false;
    }

    private void Start()
    {
        if (listenerTransform == null && Camera.main != null)
            listenerTransform = Camera.main.transform;
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnUpdate += Tick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnUpdate -= Tick;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    protected bool OnInitialize()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("More than one Audio Manager in Scene: " + Instance.name);
            return false;
        }

        Instance = this;

        InitializePool();

        return true;
    }

    private void InitializePool()
    {
        audioSources = new Stack<AudioSource>(audioSourcePoolSize);
        requests = new SoundRequest[maxRequestsPerFrame];
        playing = new PlayingSound[audioSourcePoolSize];
        tracked = new TrackedSound[Mathf.Max(1, maxTrackedSounds)];

        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            AudioSource audioSource = Instantiate(audioSourcePrefab, transform);
            audioSource.gameObject.SetActive(true);
            audioSource.playOnAwake = false;
            audioSource.Stop();
            audioSources.Push(audioSource);
        }
    }
    #endregion

    private void Tick()
    {
        ReleaseFinished();
        ProcessSoundRequests();
        UpdateTrackedSounds();
    }

    #region Sound Requesting
    public void RequestSound(AudioClip clip, Vector3 position, float priority = 0f, float volume = 1f, float pitch = 1f, float pitchRange = 0f, float volumeRange = 0f, bool loopSound = false, bool additive = false)
    {
        if (clip == null || !InRange(position))
            return;

        Enqueue(new SoundRequest
        {
            Clip = clip,
            Position = position,
            Priority = priority,
            Loop = loopSound,
            Volume = volume,
            Pitch = pitch,
            PitchRange = pitchRange,
            VolumeRange = volumeRange,
            Additive = additive
        });
    }

    // Whether it stacks with itself comes from the config's additive setting
    public void RequestSound(SoundConfig soundConfig, Vector3 position, float volumeScale = 1f)
    {
        AudioClip clip = soundConfig != null && soundConfig.HasClips
            ? soundConfig.audioClips[Random.Range(0, soundConfig.audioClips.Length)]
            : null;

        if (clip == null)
        {
            Debug.LogWarning("Sound or AudioClip is null. Skipping sound request.");
            return;
        }

        // Out of range is normal (distant impacts), so it's skipped quietly. 2D sounds (spatial blend 0: UI, hit
        // markers) sound the same everywhere, so they're never too far away.
        if (soundConfig.spatialBlend > 0f && !InRange(position))
            return;

        Enqueue(new SoundRequest
        {
            Clip = clip,
            Position = position,
            Priority = soundConfig.priority,
            Loop = soundConfig.loopSound,
            Volume = soundConfig.volume * volumeScale,
            Pitch = soundConfig.pitch,
            PitchRange = soundConfig.pitchRange,
            VolumeRange = soundConfig.volumeRange * volumeScale,
            SpatialBlend = soundConfig.spatialBlend,
            Additive = soundConfig.additive
        });
    }

    public void StopSound(AudioClip clip)
    {
        if (clip == null || !playingSounds.TryGetValue(clip, out AudioSource source))
            return;

        for (int i = 0; i < playingCount; i++)
        {
            if (playing[i].Source == source)
            {
                ReleasePlaying(i);
                return;
            }
        }

        playingSounds.Remove(clip);
    }

    private bool InRange(Vector3 position)
    {
        return listenerTransform == null || (position - listenerTransform.position).sqrMagnitude <= maxDistance * maxDistance;
    }

    private void Enqueue(in SoundRequest request)
    {
        if (requestCount < requests.Length)
            requests[requestCount++] = request;
    }
    #endregion

    #region Sound Processing
    private void ProcessSoundRequests()
    {
        if (requestCount == 0)
            return;

        // Only sort when some have to be dropped. Insertion sort: tiny lists, no allocation, and equal priorities
        // keep the order they were requested in.
        if (requestCount > maxSimultaneousSounds)
        {
            for (int i = 1; i < requestCount; i++)
            {
                SoundRequest current = requests[i];
                int j = i - 1;

                while (j >= 0 && requests[j].Priority < current.Priority)
                {
                    requests[j + 1] = requests[j];
                    j--;
                }

                requests[j + 1] = current;
            }
        }

        int soundsToPlay = Mathf.Min(requestCount, maxSimultaneousSounds);

        for (int i = 0; i < soundsToPlay; i++)
            PlaySound(in requests[i]);

        // Drop clip references so finished requests don't hold them
        for (int i = 0; i < requestCount; i++)
            requests[i].Clip = null;

        requestCount = 0;
    }

    private void PlaySound(in SoundRequest request)
    {
        if (!request.Additive
            && playingSounds.TryGetValue(request.Clip, out AudioSource existingSource)
            && existingSource != null && existingSource.isPlaying)
        {
            // Already playing: high priority restarts it, anything else is skipped
            if (request.Priority > 0.5f)
            {
                Configure(existingSource, request);
                existingSource.Play();
                RefreshEndTime(existingSource);
            }

            return;
        }

        // Every source busy (a long burst of gunfire holds a lot of them): take over the lowest-priority, oldest
        // one-shot, as long as it isn't more important than this one
        if ((audioSources.Count == 0 || playingCount >= playing.Length) && !StealFor(request.Priority))
            return;

        AudioSource audioSource = audioSources.Pop();
        Configure(audioSource, request);
        audioSource.Play();

        if (!request.Additive)
            playingSounds[request.Clip] = audioSource;

        // Looping one-shots play until StopSound; give them no end time
        playing[playingCount++] = new PlayingSound
        {
            Source = audioSource,
            Clip = request.Clip,
            EndTime = request.Loop ? float.PositiveInfinity : Time.unscaledTime + ClipDuration(request.Clip, audioSource.pitch),
            InDictionary = !request.Additive,
            Priority = request.Priority,
            StartTime = Time.unscaledTime
        };
    }

    // Frees the playing one-shot with the lowest priority (the oldest of those) if it's no more important than
    // 'priority'. Looping one-shots are never taken.
    private bool StealFor(float priority)
    {
        int victim = -1;

        for (int i = 0; i < playingCount; i++)
        {
            ref PlayingSound sound = ref playing[i];

            if (sound.Source.loop || sound.Priority > priority)
                continue;

            if (victim < 0 || sound.Priority < playing[victim].Priority
                || (sound.Priority == playing[victim].Priority && sound.StartTime < playing[victim].StartTime))
                victim = i;
        }

        if (victim < 0)
            return false;

        ReleasePlaying(victim);
        return audioSources.Count > 0;
    }

    private static void Configure(AudioSource source, in SoundRequest request)
    {
        source.transform.position = request.Position;
        source.clip = request.Clip;
        source.loop = request.Loop;
        source.volume = Mathf.Clamp01(request.Volume + Random.Range(-request.VolumeRange, request.VolumeRange));
        source.pitch = request.Pitch + Random.Range(-request.PitchRange, request.PitchRange);
        source.spatialBlend = request.SpatialBlend;
    }

    private void RefreshEndTime(AudioSource source)
    {
        for (int i = 0; i < playingCount; i++)
        {
            if (playing[i].Source != source)
                continue;

            if (!source.loop)
                playing[i].EndTime = Time.unscaledTime + ClipDuration(source.clip, source.pitch);

            return;
        }
    }

    private static float ClipDuration(AudioClip clip, float pitch)
    {
        return clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
    }

    // Returns one-shots whose clip has finished. The end time gate means isPlaying is only asked once a sound
    // should be done, not every frame for every sound.
    private void ReleaseFinished()
    {
        float now = Time.unscaledTime;

        for (int i = playingCount - 1; i >= 0; i--)
        {
            ref PlayingSound sound = ref playing[i];

            if (now < sound.EndTime)
                continue;

            // Still going (audio paused, or a hitch): check again shortly
            if (sound.Source.isPlaying)
            {
                sound.EndTime = now + 0.1f;
                continue;
            }

            ReleasePlaying(i);
        }
    }

    private void ReleasePlaying(int index)
    {
        PlayingSound sound = playing[index];

        if (sound.InDictionary && playingSounds.TryGetValue(sound.Clip, out AudioSource current) && current == sound.Source)
            playingSounds.Remove(sound.Clip);

        sound.Source.Stop();
        sound.Source.clip = null;
        audioSources.Push(sound.Source);

        int last = playingCount - 1;
        playing[index] = playing[last];
        playing[last] = default;
        playingCount--;
    }
    #endregion

    #region Tracked Sounds
    private void UpdateTrackedSounds()
    {
        for (int i = trackedCount - 1; i >= 0; i--)
        {
            ref TrackedSound sound = ref tracked[i];

            if (sound.Follow == null || !sound.Source.isPlaying)
            {
                ReleaseTracked(i);
                continue;
            }

            sound.Source.transform.position = sound.Follow.position;
        }
    }

    public int RequestTracked(SoundConfig config, Transform follow)
    {
        if (config == null || follow == null || !config.HasClips)
            return 0;

        if (trackedCount >= maxTrackedSounds || audioSources.Count == 0)
            return 0;

        AudioClip clip = config.audioClips[Utilities.Random(config.audioClips.Length)];

        if (clip == null)
            return 0;

        Vector3 position = follow.position;

        if (!InRange(position))
            return 0;

        AudioSource source = audioSources.Pop();
        source.transform.position = position;
        source.clip = clip;
        source.loop = config.loopSound;
        source.pitch = config.pitch + Random.Range(-config.pitchRange, config.pitchRange);
        source.spatialBlend = config.spatialBlend;

        float volume = Mathf.Clamp01(config.volume + Random.Range(-config.volumeRange, config.volumeRange));
        source.volume = volume;
        source.Play();

        int handle = nextTrackedHandle++;

        tracked[trackedCount++] = new TrackedSound
        {
            Handle = handle,
            Source = source,
            Follow = follow,
            BaseVolume = volume
        };

        return handle;
    }

    public void SetTrackedVolume(int handle, float normalized)
    {
        int index = FindTracked(handle);

        if (index >= 0)
            tracked[index].Source.volume = tracked[index].BaseVolume * Mathf.Clamp01(normalized);
    }

    public bool IsTrackedPlaying(int handle)
    {
        int index = FindTracked(handle);
        return index >= 0 && tracked[index].Source.isPlaying;
    }

    public void StopTracked(int handle)
    {
        int index = FindTracked(handle);

        if (index >= 0)
            ReleaseTracked(index);
    }

    // A handful of tracked sounds at most, so a scan beats a dictionary
    private int FindTracked(int handle)
    {
        if (handle == 0)
            return -1;

        for (int i = 0; i < trackedCount; i++)
        {
            if (tracked[i].Handle == handle)
                return i;
        }

        return -1;
    }

    private void ReleaseTracked(int index)
    {
        AudioSource source = tracked[index].Source;
        source.Stop();
        source.clip = null;
        audioSources.Push(source);

        int last = trackedCount - 1;
        tracked[index] = tracked[last];
        tracked[last] = default;
        trackedCount--;
    }
    #endregion

    #region Rented Sources
    public AudioSource RentSource()
    {
        if (audioSources.Count == 0)
            return null;

        AudioSource source = audioSources.Pop();
        rentedSources.Add(source);

        return source;
    }

    public void ReturnSource(AudioSource source, bool killTweens = true)
    {
        if (source == null || !rentedSources.Remove(source))
            return;

        if (killTweens)
            source.DOKill();

        source.Stop();

        source.clip = null;
        source.loop = audioSourcePrefab.loop;
        source.volume = audioSourcePrefab.volume;
        source.pitch = audioSourcePrefab.pitch;
        source.spatialBlend = audioSourcePrefab.spatialBlend;
        source.outputAudioMixerGroup = audioSourcePrefab.outputAudioMixerGroup;

        // Only re-parent when it was moved; Unity refuses re-parenting while the old parent is being deactivated
        if (source.transform.parent != transform)
            source.transform.SetParent(transform, false);

        audioSources.Push(source);
    }

    public Tween PlayFaded(SoundConfig config, Vector3 position, float fadeDuration, Ease ease = Ease.InQuad)
    {
        if (config == null || !config.HasClips)
            return null;

        AudioSource source = RentSource();
        if (source == null)
            return null;

        source.transform.position = position;
        source.clip = config.audioClips[Random.Range(0, config.audioClips.Length)];
        source.volume = Mathf.Clamp01(config.volume + Random.Range(-config.volumeRange, config.volumeRange));
        source.pitch = config.pitch + Random.Range(-config.pitchRange, config.pitchRange);
        source.spatialBlend = config.spatialBlend;
        source.Play();

        return source.DOFade(0f, fadeDuration)
                     .SetEase(ease)
                     .OnKill(() => ReturnSource(source, killTweens: false));
    }
    #endregion

    #region Editor
#if UNITY_EDITOR
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

        for (int i = 0; i < playingCount; i++)
            Gizmos.DrawWireSphere(playing[i].Source.transform.position, gizmoSize);
    }

    private void OnDrawGizmosSelected()
    {
        if (listenerTransform != null)
        {
            Gizmos.color = Color.grey;
            Gizmos.DrawWireSphere(listenerTransform.position, maxDistance);
        }
    }
#endif
#endregion
}
