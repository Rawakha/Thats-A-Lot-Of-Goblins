using UnityEngine;

// One pooled context message ("BOOOOOM", "WIPED OUT THE DURSLEYS"): the root of the message prefab.
// UI_ContextMessages owns the pool and moves every live message in its one LateUpdate; this only holds the message's
// references and its animation state. The animation (scale, rise, rotation, fade) moves this root, so the text and
// its shadow move as one; the text itself only gets its words and colour.
//
// Setup: the prefab is a RectTransform (pivot in the middle) with this on it, and two children: the text and its
// shadow. Put UI_Shadowed on the text (Alt+Shift+S makes the shadow) and drag it into Text.
//
// GC: none. No subscriptions.
public class UI_ContextMessage : MonoBehaviour
{
    [Tooltip("The message text (UI_Shadowed on it, so its shadow matches).")]
    [SerializeField] private UI_Shadowed text;

    private RectTransform rect;

    public UI_Shadowed Text => text;

    // Taken on first use, so it works even if the prefab is saved inactive
    public RectTransform Rect => rect != null ? rect : rect = (RectTransform)transform;

    // Animation state, run by UI_ContextMessages
    internal Vector3 Point;             // where in the world it started
    internal float Start;
    internal float Hold;                // seconds before it starts going
    internal float Size;                // the scale it settles at
    internal Vector2 Drift;             // canvas units it rises and drifts over its life
    internal Color Colour;
    internal Core_SpringFloatState Scale;
    internal Core_SpringFloatState Angle;
    internal bool IsScream;
    internal Horde_Goblin Screamer;
    internal int ScreamHandle;
    internal float NextPunch;
}
