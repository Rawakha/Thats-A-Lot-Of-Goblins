using UnityEngine;
using UnityEngine.InputSystem;

// Tells the player how to start the next wave: a prompt (priority 10, so the edge warning and area cleared show over it)
// that wants to be up when the wave is ready for Next Wave, pops and pulses gently when it lands, and pops and slides
// away as soon as the wave starts. The key's name comes from the Next Wave action, so rebinding it changes the text. It
// shows the action's first binding for now, not whichever device is in use.
//
// Setup: put this on an object that stays active (the HUD canvas is fine). Make a Panel child with a text that has
// UI_Shadowed and UI_Punch, and assign Panel, Text and Punch. A UI_Prompt on the Panel (added at play if missing) sets
// the priority and a UI_SlidePanel the slide. It finds each area's Wave_Director as the area loads. Needs Wait For Input
// on in the Wave Settings.
//
// GC: none per frame. No UpdateManager subscription of its own; UI_Punch subscribes while it pulses. Subscribes to
// Area_Root.Changed, the current Wave_Director's WaitingForInput and WaveStarted, and the prompt's Landed. Builds the
// text string each time it shows.
public class UI_WavePrompt : MonoBehaviour
{
    [Tooltip("The panel that slides in and out.")]
    [SerializeField] private GameObject panel;

    [SerializeField] private UI_Shadowed text;

    [Tooltip("Pops and pulses the prompt. A UI_Punch on the text. Optional.")]
    [SerializeField] private UI_Punch punch;

    [Tooltip("{0} is the key, {1} the wave number.")]
    [SerializeField] private string format = "PRESS {0} FOR WAVE {1}";

    [Tooltip("Strength of the pop as it lands, as a share of the UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float popStrength = 2f;

    [Tooltip("Strength of the pop when the wave starts, as it slides away.")]
    [Min(0f)] [SerializeField] private float hidePop = 2.5f;

    [Tooltip("How much its size pulses while waiting (0.04 is 4%). 0 for no pulse.")]
    [Min(0f)] [SerializeField] private float pulse = 0.04f;

    [Tooltip("How fast it pulses, radians per second.")]
    [Min(0f)] [SerializeField] private float pulseSpeed = 5f;

    [Tooltip("Played as it lands. Optional; make it 2D.")]
    [SerializeField] private SoundData showSound;

    private Wave_Director director;
    private UI_Prompt prompt;

    private void Awake()
    {
        if (panel == null)
            return;

        prompt = UI_Prompt.On(panel, 10);
        prompt.SetWanted(false, true);
    }

    private void OnEnable()
    {
        if (panel == null || text == null)
        {
            Debug.LogWarning($"{name}: UI_WavePrompt needs a Panel and a Text.", this);
            return;
        }

        prompt.Landed += Landed;
        Area_Root.Changed += Bind;
        Bind(Area_Root.Current);
    }

    private void OnDisable()
    {
        Area_Root.Changed -= Bind;
        Bind(null);

        if (prompt != null)
            prompt.Landed -= Landed;
    }

    // A new area loaded (or the old one went, with null)
    private void Bind(Area_Root root)
    {
        if (director != null)
        {
            director.WaitingForInput -= Show;
            director.WaveStarted -= Hide;
        }

        director = root != null ? root.Director : null;

        if (prompt != null)
            prompt.SetWanted(false, true);

        if (director == null)
            return;

        director.WaitingForInput += Show;
        director.WaveStarted += Hide;
    }

    private void Show(int wave)
    {
        string key = director.NextWaveAction != null
            ? director.NextWaveAction.action.GetBindingDisplayString(0).ToUpperInvariant()
            : "NEXT WAVE";

        text.SetText(string.Format(format, key, wave));
        prompt.SetWanted(true);
    }

    // It's on screen: pop and pulse (again, each time it comes back after a higher prompt)
    private void Landed()
    {
        if (punch != null)
        {
            punch.Punch(popStrength);
            punch.SetIdle(0f, 0f, pulse, pulseSpeed);
        }

        Utilities.TryPlaySound(showSound, transform.position);
    }

    // Pops as it slides away
    private void Hide(int wave)
    {
        if (punch != null)
        {
            punch.SetIdle(0f, 0f, 0f, 0f);
            punch.Punch(hidePop);
        }

        prompt.SetWanted(false);
    }
}
