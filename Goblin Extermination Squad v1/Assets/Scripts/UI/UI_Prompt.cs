using System;
using UnityEngine;

// One prompt in the prompt system (see UI_Prompts): a panel that wants to be on screen or doesn't. Whatever script owns
// the prompt calls SetWanted; the system shows the highest Priority one that's wanted and slides the rest out of the way,
// bringing each back when the ones above it go. Equal priority: the newest wins.
//
// The owning scripts (wave prompt, edge warning, area cleared) add this to their Panel with a sensible priority if it
// isn't there. Add it yourself to change a priority or the slide. New prompts: put this on the panel, call SetWanted, and
// listen to Landed to pop the text in.
//
// Priorities in use: edge warning 100, area cleared 50, wave prompt 10.
//
// GC: none per frame. No subscriptions.
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UI_Prompt : MonoBehaviour
{
    [Tooltip("Higher shows over lower. Edge warning 100, area cleared 50, wave prompt 10.")]
    [SerializeField] private int priority;

    [Tooltip("When another prompt takes over from this one, the share of this prompt's slide-out it waits for before " +
             "sliding in. 0 slides in as this leaves, 1 waits until it's gone.")]
    [Range(0f, 1f)] [SerializeField] private float swapWait = 0.5f;

    private UI_SlidePanel slide;
    private Action landed;
    private bool wanted;

    public int Priority => priority;
    public bool Wanted => wanted;
    public bool Visible => slide != null && slide.Showing;

    // Where it came in this time (the newer, the higher among equals); set by the system
    internal int Order;

    // It has finished sliding in (every time: it slides in again after a higher prompt goes)
    public event Action Landed;

    private void Init()
    {
        if (slide != null)
            return;

        slide = UI_SlidePanel.On(gameObject);
        slide.HideNow();
        landed = () => Landed?.Invoke();
    }

    private void Awake() => Init();

    private void OnDestroy() => UI_Prompts.Remove(this);

    // The prompt, added to the panel with this priority if the panel has none. A UI_Prompt you added keeps its own.
    public static UI_Prompt On(GameObject panel, int defaultPriority)
    {
        if (panel.TryGetComponent(out UI_Prompt found))
            return found;

        UI_Prompt added = panel.AddComponent<UI_Prompt>();
        added.priority = defaultPriority;
        return added;
    }

    // Wants to be on screen, or doesn't. Instant gets it off now, with no slide.
    public void SetWanted(bool value, bool instant = false)
    {
        Init();

        if (value)
        {
            if (!wanted)
            {
                wanted = true;
                UI_Prompts.Add(this);
            }

            return;
        }

        if (wanted)
        {
            wanted = false;
            UI_Prompts.Remove(this);
        }

        if (instant)
            slide.HideNow();
    }

    // Called by the system
    internal void Display(bool show, float delay)
    {
        Init();

        if (show)
            slide.Show(delay, landed);
        else
            slide.Hide();
    }

    // How long a prompt taking over from this one waits before sliding in
    internal float SwapDelay => slide != null ? slide.OutTime * swapWait : 0f;
}
