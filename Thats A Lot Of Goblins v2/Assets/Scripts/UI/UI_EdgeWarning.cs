using UnityEngine;

// The "turn back" warning: a prompt (priority 100, over everything else) that wants to be up from the warning line until
// the plane turns back or control is taken at the respawn line. It pops and pulses when it lands and pops as it leaves.
//
// Setup: put this on an object that stays active (the HUD canvas is fine). Make a Panel child holding the text, with
// UI_Punch on the text, and assign Run_Edge, Panel and Punch. A UI_Prompt on the Panel (added at play if missing) sets
// the priority and a UI_SlidePanel the slide.
//
// GC: none per frame. No UpdateManager subscription of its own; UI_Punch subscribes while it pulses. Subscribes to
// Run_Edge.WarningChanged and the prompt's Landed.
public class UI_EdgeWarning : MonoBehaviour
{
    [SerializeField] private Run_Edge edge;

    [Tooltip("The panel that slides in and out.")]
    [SerializeField] private GameObject panel;

    [Tooltip("Pops and pulses the warning: a UI_Punch on the text. Optional.")]
    [SerializeField] private UI_Punch punch;

    [Tooltip("Strength of the pop as it lands and as it leaves, as a share of the UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float popStrength = 2f;

    [Tooltip("How much its size pulses while up (0.05 is 5%).")]
    [Min(0f)] [SerializeField] private float pulse = 0.05f;

    [Tooltip("How fast it pulses, radians per second.")]
    [Min(0f)] [SerializeField] private float pulseSpeed = 10f;

    [Tooltip("Played as it lands. Optional; make it 2D.")]
    [SerializeField] private SoundData showSound;

    private UI_Prompt prompt;

    private void Awake()
    {
        if (panel == null)
            return;

        prompt = UI_Prompt.On(panel, 100);
        prompt.SetWanted(false, true);
    }

    private void OnEnable()
    {
        if (edge == null || panel == null)
        {
            Debug.LogWarning($"{name}: UI_EdgeWarning needs a Run_Edge and a Panel.", this);
            return;
        }

        prompt.Landed += Landed;
        edge.WarningChanged += OnWarning;
    }

    private void OnDisable()
    {
        if (edge != null)
            edge.WarningChanged -= OnWarning;

        if (prompt != null)
            prompt.Landed -= Landed;
    }

    private void OnWarning(float amount)
    {
        bool show = amount > 0f;

        if (show == prompt.Wanted)
            return;

        if (!show && punch != null)
        {
            punch.SetIdle(0f, 0f, 0f, 0f);
            punch.Punch(popStrength);
        }

        prompt.SetWanted(show);
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
}
