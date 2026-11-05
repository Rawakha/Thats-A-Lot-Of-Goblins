using System.Collections.Generic;
using UnityEngine;

// Context messages: instead of a number per kill, the HUD speaks when something worth saying happens. A bomb that
// kills a crowd shouts "BOOOOOM" at the blast; later, frozen goblins get "BRRRRR", and so on. Each context has a
// UI_MessageSet with its lines, how many kills it takes, a cooldown and its look. Sometimes (the set's Dark Chance) a
// rare dark line plays instead ("WIPED OUT THE DURSLEYS"): bigger, its own colour, and it stays longer.
// Messages come from a pool and never replace each other: each plays its whole life on its own (pop in past its size
// and settle, wobble, rise and drift, hold, then shrink and fade). More kills make a bigger message.
//
// Anything can play one: UI_ContextMessages.TryPlay(UI_MessageContext.Explosion, worldPoint, kills). Horde_System
// does for blasts and also starts/stops short labels that follow the mouths of goblins playing tracked screams.
//
// Setup: put this on the HUD (under the canvas), assign the canvas, the message prefab (UI_ContextMessage) and the
// message sets (one per context). Messages are made under Container (empty: the canvas).
//
// GC: a message's text is a string from the set (no building), so none per message. The pool is filled at start; past
// Pool Size the oldest message is reused. One UpdateManager subscription (OnLateUpdate).
[DefaultExecutionOrder(Utilities.ExecutionOrder.DependantSingletons)]
public class UI_ContextMessages : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera the world is seen through. Empty uses the main camera.")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Canvas canvas;
    [Tooltip("Where messages are made. Empty uses the canvas.")]
    [SerializeField] private RectTransform container;
    [SerializeField] private UI_ContextMessage prefab;

    [Tooltip("One per context (Explosion, ...). A context without a set stays quiet.")]
    [SerializeField] private UI_MessageSet[] sets;

    [Tooltip("Most messages on screen at once. Past this the oldest is reused.")]
    [SerializeField, Min(1)] private int poolSize = 12;

    [Header("Pop In")]
    [Tooltip("Scale speed it starts with: how far it overshoots its size before settling.")]
    [SerializeField, Min(0f)] private float popKick = 18f;

    [Tooltip("Dark lines pop this much harder.")]
    [SerializeField, Min(0f)] private float darkPop = 1.4f;

    [Tooltip("How the scale springs. A low damper overshoots and wobbles more.")]
    [SerializeField] private Core_SpringSettings scaleSpring = new Core_SpringSettings(350f, 14f);

    [Header("Wobble")]
    [Tooltip("Rotation speed (degrees per second) each message starts with, alternating left and right.")]
    [SerializeField, Min(0f)] private float rotateKick = 500f;

    [Tooltip("How the rotation springs back to straight.")]
    [SerializeField] private Core_SpringSettings rotateSpring = new Core_SpringSettings(250f, 9f);

    [Header("Move")]
    [Tooltip("Canvas units it starts above the event.")]
    [SerializeField] private float spawnHeight = 30f;

    [Tooltip("Canvas units it rises over its life (fast at first, easing off).")]
    [SerializeField] private float rise = 90f;

    [Tooltip("Random sideways drift either way, canvas units, so overlapping messages fan out.")]
    [SerializeField, Min(0f)] private float drift = 35f;

    [Header("Life")]
    [Tooltip("Seconds a message stays before going (dark lines add their set's Dark Hold).")]
    [SerializeField, Min(0f)] private float hold = 0.8f;

    [Tooltip("Seconds it takes to shrink and fade away.")]
    [SerializeField, Min(0.01f)] private float exitDuration = 0.25f;

    [Header("Screams")]
    [Tooltip("Maximum simultaneous scream labels. Zero disables them.")]
    [SerializeField, Min(0)] private int maxScreams = 3;
    [SerializeField] private string screamText = "AAAAAA!";
    [SerializeField] private Color screamColour = Color.white;
    [SerializeField, Min(0f)] private float screamSize = 0.55f;
    [SerializeField, Min(0f)] private float screamHeight = 10f;
    [SerializeField, Min(0.05f)] private float screamPunchInterval = 0.25f;
    [SerializeField, Min(0f)] private float screamPunch = 5f;

    [Header("Sound")]
    [Tooltip("Played with a dark line. Optional; make it 2D.")]
    [SerializeField] private SoundData darkSound;

    private readonly Stack<UI_ContextMessage> free = new Stack<UI_ContextMessage>();
    private readonly List<UI_ContextMessage> live = new List<UI_ContextMessage>();
    private Camera canvasCamera;
    private float spinSign = 1f;

    // Per set: the last line and dark line picked (no repeats) and when it may speak again
    private int[] lastLine;
    private int[] lastDark;
    private float[] nextAllowed;

    public static UI_ContextMessages Instance { get; private set; }

    public static bool TryPlay(UI_MessageContext context, Vector3 worldPoint, int kills)
    {
        return Instance != null && Instance.Play(context, worldPoint, kills);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"{name}: A second UI_ContextMessages was found, this one is disabled.", this);
            enabled = false;
            return;
        }

        if (viewCamera == null)
            viewCamera = Camera.main;

        if (canvas == null || prefab == null || prefab.Text == null)
        {
            Debug.LogError($"{name}: UI_ContextMessages needs a Canvas and a message prefab (with its Text assigned).", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (container == null)
            container = (RectTransform)canvas.transform;

        canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        live.Capacity = poolSize;

        int setCount = sets != null ? sets.Length : 0;
        lastLine = new int[setCount];
        lastDark = new int[setCount];
        nextAllowed = new float[setCount];

        for (int i = 0; i < setCount; i++)
            lastLine[i] = lastDark[i] = -1;

        for (int i = 0; i < poolSize; i++)
        {
            UI_ContextMessage message = Instantiate(prefab, container, false);
            message.gameObject.SetActive(false);
            free.Push(message);
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

    // Speaks for this context if it has a set, enough kills and isn't cooling down. True if a message played.
    public bool Play(UI_MessageContext context, Vector3 point, int kills)
    {
        if (!enabled || viewCamera == null || viewCamera.WorldToScreenPoint(point).z <= 0f)
            return false;

        int index = SetFor(context);

        if (index < 0)
            return false;

        UI_MessageSet set = sets[index];
        float now = Time.unscaledTime;

        if (kills < set.minKills || now < nextAllowed[index])
            return false;

        bool dark = kills >= set.darkMinKills && set.darkLines.Length > 0 && Random.value < set.darkChance;
        string text = dark ? Pick(set.darkLines, ref lastDark[index]) : Pick(set.lines, ref lastLine[index]);

        if (string.IsNullOrEmpty(text))
            return false;

        nextAllowed[index] = now + set.cooldown;

        float size = Mathf.Min(set.baseSize + set.sizePerKill * (kills - set.minKills), set.maxSize);

        if (dark)
        {
            size *= set.darkSize;
            Utilities.TryPlaySound(darkSound, point);
        }

        Show(point, text, dark ? set.darkColour : set.colour, size, hold + (dark ? set.darkHold : 0f), dark ? darkPop : 1f, now);
        return true;
    }

    private int SetFor(UI_MessageContext context)
    {
        if (sets == null)
            return -1;

        for (int i = 0; i < sets.Length; i++)
        {
            if (sets[i] != null && sets[i].context == context)
                return i;
        }

        return -1;
    }

    // A random line, never the one picked last time (when there's more than one)
    private static string Pick(string[] lines, ref int last)
    {
        if (lines == null || lines.Length == 0)
            return null;

        int index = Random.Range(0, lines.Length);

        if (lines.Length > 1 && index == last)
            index = (index + 1 + Random.Range(0, lines.Length - 1)) % lines.Length;

        last = index;
        return lines[index];
    }

    public void StartScream(Horde_Goblin goblin)
    {
        if (!isActiveAndEnabled || viewCamera == null || free.Count <= 1 ||
            maxScreams <= 0 || string.IsNullOrEmpty(screamText))
            return;

        int count = 0;
        for (int i = 0; i < live.Count; i++)
        {
            if (live[i].Screamer == goblin)
                return;
            if (live[i].IsScream)
                count++;
        }

        Vector3 point = goblin.transform.TransformPoint(goblin.Type.mouthPosition);
        Vector3 viewport = viewCamera.WorldToViewportPoint(point);
        if (count >= maxScreams || viewport.z <= 0f ||
            viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
            return;

        Show(point, screamText, screamColour, screamSize, float.PositiveInfinity, 1f, Time.unscaledTime, goblin);
    }

    public void StopScream(Horde_Goblin goblin)
    {
        for (int i = 0; i < live.Count; i++)
            if (live[i].Screamer == goblin)
                EndScream(live[i], Time.unscaledTime);
    }

    private static void EndScream(UI_ContextMessage message, float now)
    {
        message.Screamer = null;
        message.Hold = now - message.Start;
    }

    private void Show(Vector3 point, string text, Color colour, float size, float holdTime, float punch, float now,
        Horde_Goblin screamer = null)
    {
        UI_ContextMessage message = Take();

        message.Text.SetText(text);
        message.Point = point;
        message.Start = now;
        message.Size = size;
        message.Hold = holdTime;
        message.IsScream = screamer != null;
        message.Screamer = screamer;
        message.ScreamHandle = screamer != null ? screamer.ScreamHandle : 0;
        message.NextPunch = now + (screamer != null ? Random.Range(0.8f, 1.2f) * screamPunchInterval : 0f);
        message.Drift = message.IsScream ? Vector2.zero : new Vector2(Random.Range(-drift, drift), rise);
        message.Colour = colour;
        message.Text.SetColor(colour);

        // Starts at nothing with a kick upward, so it shoots past its size and springs back
        message.Scale = new Core_SpringFloatState(0f) { velocity = popKick * punch * size };
        message.Angle = new Core_SpringFloatState(0f) { velocity = message.IsScream ? 0f : rotateKick * punch * spinSign };
        spinSign = -spinSign;

        message.Rect.SetAsLastSibling();
        message.gameObject.SetActive(true);
        live.Add(message);
        Animate(message, now, 0f);
    }

    // A free message, or the oldest live one if they're all in use
    private UI_ContextMessage Take()
    {
        if (free.Count > 0)
            return free.Pop();

        int oldest = 0;

        for (int i = 1; i < live.Count; i++)
        {
            if (live[i].Start < live[oldest].Start)
                oldest = i;
        }

        UI_ContextMessage message = live[oldest];
        live.RemoveAt(oldest);
        return message;
    }

    private void Tick()
    {
        float now = Time.unscaledTime;
        float deltaTime = Time.unscaledDeltaTime;

        for (int i = live.Count - 1; i >= 0; i--)
        {
            UI_ContextMessage message = live[i];

            if (!Animate(message, now, deltaTime))
            {
                message.Screamer = null;
                message.gameObject.SetActive(false);
                free.Push(message);
                live.RemoveAt(i);
            }
        }
    }

    // One frame of a message's life. False once it's gone.
    private bool Animate(UI_ContextMessage message, float now, float deltaTime)
    {
        if (viewCamera == null)
            return false;

        if (message.IsScream && float.IsPositiveInfinity(message.Hold))
        {
            Horde_Goblin goblin = message.Screamer;
            if (goblin == null || !goblin.IsActive || goblin.ScreamHandle != message.ScreamHandle ||
                AudioManager.Instance == null || !AudioManager.Instance.IsTrackedPlaying(message.ScreamHandle))
            {
                EndScream(message, now);
            }
            else
            {
                message.Point = goblin.transform.TransformPoint(goblin.Type.mouthPosition);
                if (now >= message.NextPunch)
                {
                    message.Scale.velocity = screamPunch * message.Size;
                    message.NextPunch = now + Mathf.Max(0.05f, screamPunchInterval);
                }
            }
        }

        float age = now - message.Start;
        float life = message.Hold + exitDuration;

        if (age >= life)
            return false;

        // Going: the scale springs toward nothing and the text fades
        float leaving = Mathf.Clamp01((age - message.Hold) / exitDuration);
        float target = leaving > 0f ? 0f : message.Size;

        if (deltaTime > 0f)
        {
            Core_Math.Spring(ref message.Scale, target, scaleSpring, deltaTime);
            Core_Math.Spring(ref message.Angle, 0f, rotateSpring, deltaTime);
        }

        float scale = Mathf.Max(0f, message.Scale.value) * (1f - leaving * leaving);
        message.Rect.localScale = new Vector3(scale, scale, 1f);
        message.Rect.localRotation = Quaternion.Euler(0f, 0f, message.Angle.value);

        if (leaving > 0f)
        {
            Color colour = message.Colour;
            colour.a *= 1f - leaving;
            message.Text.SetColor(colour);
        }

        // Over the event on screen, risen and drifted along an ease-out over its life
        Vector3 screen = viewCamera.WorldToScreenPoint(message.Point);

        if (screen.z <= 0f)
            return false;

        float t = Mathf.Clamp01(age / life);
        float eased = 1f - (1f - t) * (1f - t);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screen, canvasCamera, out Vector2 local))
            message.Rect.localPosition = local + new Vector2(0f, message.IsScream ? screamHeight : spawnHeight) + message.Drift * eased;

        return true;
    }
}
