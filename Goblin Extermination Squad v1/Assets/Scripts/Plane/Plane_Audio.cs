using UnityEngine;
using UnityEngine.Audio;

// The plane's sound. Three loops are rented from the AudioManager and driven by the flight every frame:
//   engine (low and high layers crossfaded by speed, pitch from speed, acceleration and descent),
//   wind (speed, flying low and turning).
// One-shots go through AudioManager.RequestSound: a whoosh when snapping into a hard turn.
//
// Listener: give it a listener transform (the object with the AudioListener) and it's kept on the plane, facing the
// way the camera faces, so sounds pan by where they are on screen and the plane is always the loudest thing.
//
// GC: none per frame. One-shots allocate inside AudioManager (a coroutine each). One UpdateManager subscription
// (OnLateUpdate), added on enable.
[RequireComponent(typeof(Plane_Controller))]
public class Plane_Audio : MonoBehaviour
{
    private struct Loop
    {
        public AudioSource Source;
        public AudioMixerGroup OriginalGroup;
        public float BaseVolume;
        public float BasePitch;
        public float Volume;
        public float Pitch;
    }

    [SerializeField] private Plane_AudioSettings settings;

    [Header("Listener")]
    [Tooltip("An empty object with the AudioListener, never the camera. Kept on the plane, facing like the camera. " +
             "Empty leaves it alone.")]
    [SerializeField] private Transform listener;
    [SerializeField] private Camera viewCamera;

    [Header("Fade Out")]
    [Tooltip("Seconds the loops take to fade away when FadeOut is called (the results screen coming up).")]
    [Min(0.01f)] [SerializeField] private float fadeOutTime = 1f;

    private Plane_Controller plane;
    private bool fadingOut;

    private Loop engineLow;
    private Loop engineHigh;
    private Loop wind;
    private bool rented;

    private float lastSpeed;
    private float acceleration;
    private float fade;
    private bool whooshArmed = true;
    private float whooshCooldown;

    private void Awake()
    {
        plane = GetComponent<Plane_Controller>();

        if (viewCamera == null)
            viewCamera = Camera.main;

        // The listener is moved onto the plane every frame, so it must never be the camera itself
        if (listener != null && listener.GetComponent<Camera>() != null)
        {
            Debug.LogError($"{name}: Plane_Audio's listener is a camera and would be dragged onto the plane. Move the " +
                           "AudioListener to its own empty object and assign that instead. Listener ignored.", this);
            listener = null;
        }
    }

    private void OnEnable()
    {
        if (settings == null)
        {
            Debug.LogError($"{name}: Plane_Audio has no Plane_AudioSettings.", this);
            return;
        }

        if (UpdateManager.Instance == null)
        {
            Debug.LogError($"{name}: No UpdateManager in the scene, the plane audio won't run.", this);
            return;
        }

        lastSpeed = plane.Speed;
        fade = 0f;

        UpdateManager.Instance.OnLateUpdate += LateTick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= LateTick;

        ReturnLoops();
    }

    // The loops fade away and are handed back, and this stops
    public void FadeOut() => fadingOut = true;

    private void LateTick()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        if (fadingOut)
        {
            fade = Mathf.MoveTowards(fade, 0f, deltaTime / fadeOutTime);
            ScaleLoop(ref engineLow);
            ScaleLoop(ref engineHigh);
            ScaleLoop(ref wind);

            if (fade <= 0f)
                enabled = false;

            return;
        }

        // The AudioManager may start after this, so loops are rented on the first tick it's ready
        if (!rented)
            RentLoops();

        FollowListener();

        Plane_Settings flight = plane.Settings;
        float topSpeed = Mathf.Max(0.01f, flight.straightSpeed + flight.descentSpeedBonus);
        float speed01 = Mathf.Clamp01(plane.Speed / topSpeed);
        float turn = Mathf.Clamp01(Mathf.Abs(plane.TurnRate) / Mathf.Max(1f, flight.maxTurnRate));
        float descent = flight.descentRate > 0f ? Mathf.Clamp01(-plane.VerticalSpeed / flight.descentRate) : 0f;
        float low = Mathf.InverseLerp(flight.cruiseHeight, flight.lowFlightHeight, plane.Altitude);

        acceleration = Core_Math.Damp(acceleration, (plane.Speed - lastSpeed) / deltaTime, 8f, deltaTime);
        lastSpeed = plane.Speed;
        fade = settings.fadeInTime > 0f ? Mathf.MoveTowards(fade, 1f, deltaTime / settings.fadeInTime) : 1f;

        // Engine: pitch climbs with speed, revs with acceleration, whines in a descent
        float enginePitch = Mathf.Lerp(settings.enginePitchIdle, settings.enginePitchTop, speed01)
            + acceleration * settings.enginePitchPerAcceleration
            + descent * settings.engineDescentPitch;

        // Equal-power crossfade so the engine doesn't dip in the middle
        float high = engineHigh.Source != null ? speed01 : 0f;
        float lowLayer = Mathf.Cos(high * Mathf.PI * 0.5f);
        float highLayer = Mathf.Sin(high * Mathf.PI * 0.5f);

        Drive(ref engineLow, lowLayer, enginePitch, settings.engineResponse, deltaTime);
        Drive(ref engineHigh, highLayer, enginePitch, settings.engineResponse, deltaTime);

        // Wind: speed squared so cruising is calm and top speed roars, more when low and in hard turns
        float windVolume = speed01 * speed01 * Mathf.Lerp(1f, settings.windLowBoost, low) * (1f + turn * settings.windTurnBoost);
        float windPitch = Mathf.Lerp(settings.windPitchSlow, settings.windPitchTop, speed01);
        Drive(ref wind, windVolume, windPitch, settings.windResponse, deltaTime);

        UpdateWhoosh(turn, deltaTime);
    }

    // Snapping into a hard turn whooshes once; it re-arms after the turn eases off
    private void UpdateWhoosh(float turn, float deltaTime)
    {
        whooshCooldown -= deltaTime;

        if (!whooshArmed)
        {
            if (turn < settings.whooshReset)
                whooshArmed = true;

            return;
        }

        if (turn < settings.whooshThreshold || whooshCooldown > 0f)
            return;

        Play(SoundData.Get(settings.turnWhoosh), 1f);
        whooshArmed = false;
        whooshCooldown = settings.whooshCooldown;
    }

    private void Play(SoundConfig sound, float volumeScale)
    {
        if (sound != null && sound.HasClips && AudioManager.Instance != null)
            AudioManager.Instance.RequestSound(sound, transform.position, volumeScale);
    }

    private void Drive(ref Loop loop, float volume, float pitch, float response, float deltaTime)
    {
        if (loop.Source == null)
            return;

        loop.Source.transform.position = transform.position;
        loop.Volume = Core_Math.Damp(loop.Volume, volume, response, deltaTime);
        loop.Pitch = Core_Math.Damp(loop.Pitch, pitch, response, deltaTime);

        loop.Source.volume = loop.BaseVolume * loop.Volume * fade;
        loop.Source.pitch = loop.BasePitch * loop.Pitch;
    }

    private void ScaleLoop(ref Loop loop)
    {
        if (loop.Source != null)
            loop.Source.volume = loop.BaseVolume * loop.Volume * fade;
    }

    private void FollowListener()
    {
        if (listener == null)
            return;

        Quaternion facing = viewCamera != null ? viewCamera.transform.rotation : listener.rotation;
        listener.SetPositionAndRotation(transform.position, facing);
    }

    private void RentLoops()
    {
        if (AudioManager.Instance == null)
            return;

        engineLow = RentLoop(SoundData.Get(settings.engineLow));
        engineHigh = RentLoop(SoundData.Get(settings.engineHigh));
        wind = RentLoop(SoundData.Get(settings.wind));
        rented = true;
    }

    private Loop RentLoop(SoundConfig sound)
    {
        Loop loop = default;

        if (sound == null || !sound.HasClips || sound.audioClips[0] == null)
            return loop;

        AudioSource source = AudioManager.Instance.RentSource();

        if (source == null)
        {
            Debug.LogWarning($"{name}: AudioManager has no free sources for the plane loops; raise its pool size.", this);
            return loop;
        }

        loop.Source = source;
        loop.OriginalGroup = source.outputAudioMixerGroup;
        loop.BaseVolume = sound.volume;
        loop.BasePitch = sound.pitch;
        loop.Pitch = 1f;

        // Not parented to the plane: the AudioManager re-parents sources when they're returned, which Unity refuses
        // while the plane is being deactivated. The loops follow the plane's position in LateTick instead.
        source.transform.position = transform.position;
        source.clip = sound.audioClips[0];
        source.loop = true;
        source.volume = 0f;
        source.spatialBlend = sound.spatialBlend;

        if (settings.mixerGroup != null)
            source.outputAudioMixerGroup = settings.mixerGroup;

        // Start each loop somewhere different so layers built from similar clips don't phase
        source.time = Random.Range(0f, source.clip.length);
        source.Play();
        return loop;
    }

    private void ReturnLoops()
    {
        ReturnLoop(ref engineLow);
        ReturnLoop(ref engineHigh);
        ReturnLoop(ref wind);
        rented = false;
    }

    private static void ReturnLoop(ref Loop loop)
    {
        if (loop.Source != null)
        {
            loop.Source.outputAudioMixerGroup = loop.OriginalGroup;

            if (AudioManager.Instance != null)
                AudioManager.Instance.ReturnSource(loop.Source);
        }

        loop = default;
    }
}
