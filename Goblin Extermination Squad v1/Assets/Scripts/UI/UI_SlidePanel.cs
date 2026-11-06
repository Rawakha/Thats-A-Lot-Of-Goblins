using System;
using DG.Tweening;
using UnityEngine;

// Slides a UI prompt on and off screen: Show brings it in from the top or bottom to where it was authored, Hide sends it
// back and disables it. Reversing mid-slide carries on from where it is. Used by every prompt (area cleared, next wave,
// edge warning), which add this to their Panel for you if it's missing.
//
// Setup: place the panel where it should rest, on screen. Set Side and make Distance enough to take it fully off screen.
//
// GC: none per frame. No subscriptions. One small DOTween allocation per slide.
[RequireComponent(typeof(RectTransform))]
public class UI_SlidePanel : MonoBehaviour
{
    public enum Side
    {
        Bottom,
        Top,
        Right,
        Left
    }

    [Tooltip("The screen edge it comes from and goes back to.")]
    [SerializeField] private Side side = Side.Bottom;

    [Tooltip("How far off its rest position it hides, in canvas units.")]
    [Min(0f)] [SerializeField] private float distance = 300f;

    [Tooltip("Seconds to slide in.")]
    [Min(0f)] [SerializeField] private float inTime = 0.4f;

    [Tooltip("How it slides in. OutBack overshoots and settles.")]
    [SerializeField] private Ease inEase = Ease.OutBack;

    [Tooltip("Seconds to slide out.")]
    [Min(0f)] [SerializeField] private float outTime = 0.25f;

    [Tooltip("How it slides out. InBack pulls back before leaving.")]
    [SerializeField] private Ease outEase = Ease.InBack;

    private RectTransform rect;
    private Vector2 rest;
    private Tween slide;
    private bool ready;

    public bool Showing { get; private set; }
    public float InTime => inTime;
    public float OutTime => outTime;

    private void Init()
    {
        if (ready)
            return;

        ready = true;
        rect = (RectTransform)transform;
        rest = rect.anchoredPosition;
    }

    private void OnDestroy() => slide?.Kill();

    private Vector2 Hidden
    {
        get
        {
            switch (side)
            {
                case Side.Top: return rest + Vector2.up * distance;
                case Side.Right: return rest + Vector2.right * distance;
                case Side.Left: return rest + Vector2.left * distance;
                default: return rest + Vector2.down * distance;
            }
        }
    }

    // Slides in, after delay seconds. Arrived is called once it's landed.
    public void Show(float delay = 0f, Action arrived = null)
    {
        Init();

        if (!gameObject.activeSelf)
        {
            rect.anchoredPosition = Hidden;
            gameObject.SetActive(true);
        }

        Showing = true;
        SlideTo(rest, inTime, inEase, delay).OnComplete(() => arrived?.Invoke());
    }

    // Slides out and disables itself
    public void Hide()
    {
        Init();

        if (!Showing)
            return;

        Showing = false;
        SlideTo(Hidden, outTime, outEase, 0f).OnComplete(() => gameObject.SetActive(false));
    }

    // Slides a panel that was simply on screen (never shown by Show) out and disables it
    public void SlideOff()
    {
        Showing = true;
        Hide();
    }

    // Gone at once, with no slide
    public void HideNow()
    {
        Init();
        slide?.Kill();
        Showing = false;
        rect.anchoredPosition = Hidden;
        gameObject.SetActive(false);
    }

    private Tween SlideTo(Vector2 target, float seconds, Ease ease, float delay)
    {
        slide?.Kill();
        slide = DOTween.To(() => rect.anchoredPosition, value => rect.anchoredPosition = value, target, seconds)
            .SetEase(ease)
            .SetDelay(delay)
            .SetUpdate(true);

        return slide;
    }

    // The panel's slide, added with this side and distance if it hasn't one yet
    public static UI_SlidePanel On(GameObject panel, Side side, float distance)
    {
        if (panel.TryGetComponent(out UI_SlidePanel found))
            return found;

        UI_SlidePanel added = panel.AddComponent<UI_SlidePanel>();
        added.side = side;
        added.distance = distance;
        return added;
    }

    // The panel's slide, added if it hasn't one yet
    public static UI_SlidePanel On(GameObject panel)
    {
        return panel.TryGetComponent(out UI_SlidePanel found) ? found : panel.AddComponent<UI_SlidePanel>();
    }
}
