using System.Collections.Generic;
using UnityEngine;

public enum UI_HitMarkerType
{
    Normal,
    Kill
}

// Hit markers over the world points that were hit, following their point on screen while the camera moves. Kills
// are bigger, red and can use their own sprite (image and shadow); each hit can play a sound at its point. Made for
// a horde:
//   - Merging: a hit near a marker that's still showing (within Merge Radius on screen) doesn't make a new one; it
//     re-punches that marker and keeps it alive. Sustained fire on a clump is one marker throbbing with every hit
//     instead of a pile. A kill merged into a normal marker turns it into a kill marker.
//   - Springs: each marker pops in from nothing past its size with a random rotation kick and wobbles back, so no two
//     look the same; re-punches add more kick. After Hold seconds without a hit it shrinks away.
//   - Markers come from a pool filled at start; past Pool Size the oldest is reused. One LateUpdate here moves every
//     live marker; the markers have no Update.
//
// Anything can play one: UI_HitMarkers.TryPlay(point, type).
//
// GC: none per hit or per frame. One UpdateManager subscription (OnLateUpdate).
[DefaultExecutionOrder(Utilities.ExecutionOrder.DependantSingletons)]
public class UI_HitMarkers : MonoBehaviour
{
    private struct Live
    {
        public UI_HitMarker Marker;
        public Vector3 Point;
        public Vector2 Screen;      // where it was last placed, for merging
        public float Start;
        public float LastHit;
        public float Size;
        public bool Kill;
        public Core_SpringFloatState Scale;
        public Core_SpringFloatState Angle;
    }

    [Header("References")]
    [Tooltip("The camera the world points are seen through. Empty uses the main camera.")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Canvas canvas;
    [Tooltip("Where markers are placed. Empty uses the canvas.")]
    [SerializeField] private RectTransform container;

    [Header("Marker")]
    [SerializeField] private UI_HitMarker prefab;
    [Tooltip("Most markers on screen at once. Past this the oldest is reused.")]
    [SerializeField, Min(1)] private int poolSize = 24;

    [SerializeField] private float normalScale = 0.5f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private float killScale = 0.8f;
    [SerializeField] private Color killColor = Color.red;
    [Tooltip("Sprite for kill markers, on both the image and its shadow. Empty keeps the prefab's sprite.")]
    [SerializeField] private Sprite killSprite;

    [Header("Sound")]
    [Tooltip("Played at the hit point with each hit marker. Optional.")]
    [SerializeField] private SoundData hitSound;
    [Tooltip("Played instead on kills. Empty uses the hit sound.")]
    [SerializeField] private SoundData killSound;

    [Header("Merging")]
    [Tooltip("Screen pixels: a hit this close to a marker that's still showing re-punches it instead of making a new one. 0 never merges.")]
    [SerializeField, Min(0f)] private float mergeRadius = 60f;

    [Header("Springs")]
    [Tooltip("Scale speed a new marker starts with (times its size): how far it overshoots before settling.")]
    [SerializeField, Min(0f)] private float popKick = 20f;

    [Tooltip("Scale speed each merged hit adds (times its size).")]
    [SerializeField, Min(0f)] private float repunchKick = 8f;

    [Tooltip("The most a marker grows past its size from stacked punches (0.6 is 60%).")]
    [SerializeField, Min(0f)] private float maxOverScale = 0.6f;

    [Tooltip("How the scale springs. A low damper overshoots and wobbles more.")]
    [SerializeField] private Core_SpringSettings scaleSpring = new Core_SpringSettings(400f, 16f);

    [Tooltip("Rotation speed (degrees per second) each hit kicks it by, in a random direction.")]
    [SerializeField, Min(0f)] private float rotateKick = 400f;

    [Tooltip("The furthest it rotates either way, in degrees.")]
    [SerializeField, Min(0f)] private float maxAngle = 30f;

    [Tooltip("How the rotation springs back to straight.")]
    [SerializeField] private Core_SpringSettings rotateSpring = new Core_SpringSettings(300f, 12f);

    [Header("Life")]
    [Tooltip("Seconds a marker stays after its last hit before it shrinks away.")]
    [SerializeField, Min(0f)] private float hold = 0.15f;

    [Tooltip("Seconds it takes to shrink away.")]
    [SerializeField, Min(0.01f)] private float exitDuration = 0.2f;

    private readonly Stack<UI_HitMarker> free = new Stack<UI_HitMarker>();
    private readonly List<Live> live = new List<Live>();
    private Camera canvasCamera;

    public static UI_HitMarkers Instance { get; private set; }

    public static bool TryPlay(Vector3 worldPoint, UI_HitMarkerType type)
    {
        if (Instance == null)
            return false;

        Instance.Play(worldPoint, type);
        return true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"{name}: A second UI_HitMarkers was found, this one is disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (viewCamera == null)
            viewCamera = Camera.main;

        if (canvas == null || prefab == null)
        {
            Debug.LogError($"{name}: UI_HitMarkers needs a Canvas and a marker prefab.", this);
            enabled = false;
            return;
        }

        if (container == null)
            container = (RectTransform)canvas.transform;

        canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        live.Capacity = poolSize;

        for (int i = 0; i < poolSize; i++)
        {
            UI_HitMarker marker = Instantiate(prefab, container, false);
            marker.gameObject.SetActive(false);
            free.Push(marker);
        }
    }

    private void OnEnable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate += Tick;
    }

    private void OnDisable()
    {
        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Play(Vector3 worldPoint, UI_HitMarkerType type)
    {
        if (!enabled || viewCamera == null)
            return;

        Vector3 screen = viewCamera.WorldToScreenPoint(worldPoint);

        // Behind the camera: nothing to show
        if (screen.z <= 0f)
            return;

        bool kill = type == UI_HitMarkerType.Kill;
        float now = Time.unscaledTime;
        Utilities.TryPlaySound(kill && killSound != null ? killSound : hitSound, worldPoint);

        int near = Nearest(screen, now);

        if (near >= 0)
        {
            Repunch(near, kill, now);
            return;
        }

        Spawn(worldPoint, screen, kill, now);
    }

    // The closest marker still showing within Merge Radius of this screen point, or -1
    private int Nearest(Vector2 screen, float now)
    {
        if (mergeRadius <= 0f)
            return -1;

        int best = -1;
        float bestSqr = mergeRadius * mergeRadius;

        for (int i = 0; i < live.Count; i++)
        {
            // One that's already shrinking away doesn't take more hits
            if (now - live[i].LastHit > hold)
                continue;

            float distanceSqr = (live[i].Screen - screen).sqrMagnitude;

            if (distanceSqr <= bestSqr)
            {
                best = i;
                bestSqr = distanceSqr;
            }
        }

        return best;
    }

    // Another hit on a showing marker: more kick, kept alive, and turned into a kill marker by a kill
    private void Repunch(int index, bool kill, float now)
    {
        Live entry = live[index];
        entry.LastHit = now;

        if (kill && !entry.Kill)
            SetLook(ref entry, true);

        entry.Scale.velocity += repunchKick * entry.Size;
        entry.Angle.velocity += RandomSign() * rotateKick;
        live[index] = entry;
    }

    private void Spawn(Vector3 worldPoint, Vector2 screen, bool kill, float now)
    {
        UI_HitMarker marker;

        if (free.Count > 0)
        {
            marker = free.Pop();
        }
        else
        {
            // All in use: take over the oldest
            int oldest = 0;

            for (int i = 1; i < live.Count; i++)
            {
                if (live[i].Start < live[oldest].Start)
                    oldest = i;
            }

            marker = live[oldest].Marker;
            RemoveAt(oldest);
        }

        Live entry = new Live
        {
            Marker = marker,
            Point = worldPoint,
            Screen = screen,
            Start = now,
            LastHit = now
        };

        SetLook(ref entry, kill);

        // From nothing with a kick, so it shoots past its size and springs back
        entry.Scale = new Core_SpringFloatState(0f) { velocity = popKick * entry.Size };
        entry.Angle = new Core_SpringFloatState(0f) { velocity = RandomSign() * rotateKick };

        marker.Rect.localScale = Vector3.zero;
        marker.Rect.localRotation = Quaternion.identity;
        marker.Rect.SetAsLastSibling();
        marker.gameObject.SetActive(true);

        live.Add(entry);
        Place(ref entry);
        live[live.Count - 1] = entry;
    }

    private void SetLook(ref Live entry, bool kill)
    {
        entry.Kill = kill;
        entry.Size = kill ? killScale : normalScale;
        entry.Marker.Image.color = kill ? killColor : normalColor;
        entry.Marker.SetSprite(killSprite, kill);
    }

    // Every live marker: springs, placement over its world point, and shrinking away once its hits stop
    private void Tick()
    {
        float now = Time.unscaledTime;
        float deltaTime = Time.unscaledDeltaTime;

        for (int i = live.Count - 1; i >= 0; i--)
        {
            Live entry = live[i];
            float quiet = now - entry.LastHit;

            if (quiet >= hold + exitDuration)
            {
                entry.Marker.gameObject.SetActive(false);
                free.Push(entry.Marker);
                RemoveAt(i);
                continue;
            }

            float leaving = Mathf.Clamp01((quiet - hold) / exitDuration);
            float target = leaving > 0f ? 0f : entry.Size;

            if (deltaTime > 0f)
            {
                Core_Math.Spring(ref entry.Scale, target, scaleSpring, deltaTime);
                Core_Math.Spring(ref entry.Angle, 0f, rotateSpring, deltaTime);
            }

            // Capped, so a long burst can't blow it up; the speed is dropped at the cap
            float most = entry.Size * (1f + maxOverScale);

            if (entry.Scale.value > most)
            {
                entry.Scale.value = most;
                entry.Scale.velocity = Mathf.Min(entry.Scale.velocity, 0f);
            }

            if (Mathf.Abs(entry.Angle.value) > maxAngle)
            {
                entry.Angle.value = Mathf.Sign(entry.Angle.value) * maxAngle;
                entry.Angle.velocity = 0f;
            }

            float scale = Mathf.Max(0f, entry.Scale.value) * (1f - leaving * leaving);

            // Behind the camera: shrunk to nothing, which hides every image in it (shadow included)
            if (!Place(ref entry))
                scale = 0f;

            entry.Marker.Rect.localScale = new Vector3(scale, scale, scale);
            entry.Marker.Rect.localRotation = Quaternion.Euler(0f, 0f, entry.Angle.value);
            live[i] = entry;
        }
    }

    // Over its world point. False while that's behind the camera.
    private bool Place(ref Live entry)
    {
        Vector3 screen = viewCamera.WorldToScreenPoint(entry.Point);

        if (screen.z <= 0f)
            return false;

        entry.Screen = screen;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screen, canvasCamera, out Vector2 local))
            entry.Marker.Rect.anchoredPosition = local;

        return true;
    }

    private static float RandomSign() => Random.value < 0.5f ? -1f : 1f;

    private void RemoveAt(int index)
    {
        int last = live.Count - 1;
        live[index] = live[last];
        live.RemoveAt(last);
    }
}
