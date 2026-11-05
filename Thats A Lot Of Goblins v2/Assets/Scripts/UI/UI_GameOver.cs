using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The results screen, for a clear and a fail: hidden until the area ends, then fades in after a short delay and punches its
// title. The title is picked at random from the Pass or Fail list, and the message underneath tells what happened: on a
// fail one of the area's own Fail Messages (Area_Settings), or the list here if it has none; {0} is the goblins that got away in this area.
// A pass has Next Area and Restart; a fail has only Restart. Weapons go off as the area ends. When the screen appears the
// plane stops where it is and its sound fades out (Fade Out Time on Plane_Audio), and the Hide On Game Over objects switch off (put the crosshair there, so the system cursor comes back for the buttons).
//
// Setup: put this on an object that stays active (the HUD canvas is fine). Build a Panel child with a Canvas Group, a title
// text, a message text (UI_Shadowed on both) and two buttons (UI_Button works). Assign them with the Run_State, Run_Edge and
// the plane's Plane_Input. The scene must be in Build Settings.
//
// GC: none per frame. One UpdateManager subscription (OnUpdate) only while the panel is up and there's a Restart Action;
// one each to Run_State.RunFailed and AreaCleared. When it shows, the message string and one small DOTween allocation.
public class UI_GameOver : MonoBehaviour
{
    [SerializeField] private Run_State run;

    [Tooltip("Takes Next Area to the fade and the next area.")]
    [SerializeField] private Run_Edge edge;

    [Tooltip("Its weapons go off while the screen is up.")]
    [SerializeField] private Plane_Input planeInput;

    [Tooltip("The panel to show. Hidden until the area ends.")]
    [SerializeField] private GameObject panel;

    [Tooltip("A Canvas Group on the panel, for the fade.")]
    [SerializeField] private CanvasGroup group;

    [Header("Text")]
    [Tooltip("The title.")]
    [SerializeField] private UI_Shadowed title;

    [Tooltip("The message under the title. Optional.")]
    [SerializeField] private UI_Shadowed message;

    [Tooltip("Titles for a clear, one picked at random.")]
    [SerializeField] private string[] passTitlesLines =
    {
        "AREA SECURED",
        "NOT BAD, PILOT",
        "SOMEONE WILL HAVE TO CLEAN THAT",
        "THEY HAD FAMILIES",
        "THE WAR CRIMES ARE GOING WELL",
        "ONLY SEVEN BILLION TO GO",
        "PEACE, OF A KIND"
    };

    [Tooltip("Titles for a fail, one picked at random.")]
    [SerializeField] private string[] failTitlesLines =
    {
        "OVERRUN",
        "THAT'S A LOT OF GOBLINS",
        "WELL, THAT WAS EARTH",
        "HUMANITY THANKS YOU FOR TRYING",
        "YOU HAD ONE PLANE",
        "THE GOBLINS SEND THEIR REGARDS"
    };

    [Tooltip("Messages for a clear, one picked at random. {0} is the goblins that got away.")]
    [TextArea] [SerializeField] private string[] passMessagesLines =
    {
        "{0} goblins got away. They will tell the others what they saw.",
        "{0} goblins got away. Their therapist has a waiting list now.",
        "{0} goblins escaped. Command is calling it a total victory.",
        "{0} survivors. Command would like fewer.",
        "{0} goblins got away. They left their children behind. That's on you, in several places.",
        "The ground will be purple for a generation. {0} goblins will remember why."
    };

    [Tooltip("Messages for a clear when no goblins got away, one picked at random.")]
    [TextArea] [SerializeField] private string[] perfectMessageLines =
    {
        "Nobody got away. Nobody will ever know what you did here.",
        "No survivors. No witnesses. Command is thrilled and would like you to stop smiling.",
        "Not one got away. Their holes are very quiet now.",
        "Zero escaped. The goblin census has been simplified.",
        "Nobody got away. It was almost a shame. Almost."
    };

    [Tooltip("Messages for a clear when one goblin got away. {He}, {he} and {his} become he or she, picked per message.")]
    [TextArea] [SerializeField] private string[] oneMessageLines =
    {
        "1 goblin got away. {He} will tell the others what {he} saw.",
        "1 goblin got away. {His} therapist has a waiting list now.",
        "1 goblin escaped. Command is calling it a total victory.",
        "1 survivor. Command would like fewer.",
        "1 goblin got away. {He} left {his} children behind. That's on you, in several places.",
        "The ground will be purple for a generation. 1 goblin will remember why."
    };

    [Tooltip("Messages for a fail when one goblin got away and the area has none of its own. {He}, {he} and {his} work here too.")]
    [TextArea] [SerializeField] private string[] oneFailMessageLines =
    {
        "1 goblin got past you. {He} didn't even run.",
        "1 goblin got through. {He} has already started renaming the streets.",
        "You were the last pilot. 1 goblin has asked if {he} can have the plane."
    };

    [Tooltip("Messages for a fail when the area has none of its own. {0} is the goblins that got away.")]
    [TextArea] [SerializeField] private string[] failMessagesLines =
    {
        "{0} goblins got away, and that was too many.",
        "{0} goblins got past you. Your mother has been informed. So have the goblins.",
        "{0} goblins got through. They've already started renaming the streets.",
        "{0} goblins got through. The last human city now has a very different tax system.",
        "{0} goblins walked past you. They didn't even run.",
        "{0} goblins reached the holes. Command has stopped answering, and started chewing.",
        "You were the last pilot. {0} goblins have asked if they can have the plane."
    };

    [Header("Buttons")]
    [Tooltip("Shown only on a clear.")]
    [SerializeField] private Button nextButton;

    [SerializeField] private Button restartButton;

    [Tooltip("Also restarts from the keyboard or pad. Optional.")]
    [SerializeField] private InputActionReference restartAction;

    [Tooltip("Switched off while the panel is up, e.g. the crosshair (so the cursor shows) and the ability panel.")]
    [SerializeField] private GameObject[] hideOnGameOver;

    [Header("Feel")]
    [Tooltip("Kicks the title as the panel appears. Optional.")]
    [SerializeField] private UI_Punch titlePunch;

    [Tooltip("Seconds from the area ending to the panel starting to appear, so the moment can land.")]
    [Min(0f)] [SerializeField] private float showDelay = 0.4f;

    [Tooltip("Seconds the panel takes to fade in.")]
    [Min(0f)] [SerializeField] private float fadeDuration = 0.25f;

    [Tooltip("Strength of the title's punch, as a share of the UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float punchStrength = 3f;

    [Tooltip("Seconds after the panel appears before the buttons work, so a button still held doesn't skip it.")]
    [Min(0f)] [SerializeField] private float restartDelay = 0.5f;

    [Tooltip("Played as the panel appears on a fail. Optional; make it 2D.")]
    [SerializeField] private SoundData showSound;

    [Tooltip("Played as the panel appears on a clear. Optional; make it 2D.")]
    [SerializeField] private SoundData passSound;

    private Tween fade;
    private bool shown;
    private bool passed;
    private bool ticking;
    private float shownAt;
    private bool used;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (run == null || panel == null)
        {
            Debug.LogWarning($"{name}: UI_GameOver needs a Run_State and a Panel.", this);
            return;
        }

        run.RunFailed += ShowFail;
        run.AreaCleared += ShowPass;

        if (restartButton != null)
            restartButton.onClick.AddListener(Restart);

        if (nextButton != null)
            nextButton.onClick.AddListener(NextArea);
    }

    private void OnDisable()
    {
        if (run != null)
        {
            run.RunFailed -= ShowFail;
            run.AreaCleared -= ShowPass;
        }

        if (restartButton != null)
            restartButton.onClick.RemoveListener(Restart);

        if (nextButton != null)
            nextButton.onClick.RemoveListener(NextArea);

        StopTicking();

        if (restartAction != null)
            restartAction.action.Disable();

        fade?.Kill();
    }

    private void ShowFail() => Show(false);

    private void ShowPass() => Show(true);

    private void Show(bool pass)
    {
        passed = pass;

        if (planeInput != null)
            planeInput.SetWeaponsLocked(true);

        if (title != null)
            title.SetText(Pick(pass ? passTitlesLines : failTitlesLines));

        if (message != null)
            message.SetText(Fill(Pick(MessagesFor(pass))));

        if (nextButton != null)
            nextButton.gameObject.SetActive(pass);

        if (group == null)
        {
            Appear();
            return;
        }

        group.alpha = 0f;
        fade = DOTween.To(() => group.alpha, value => group.alpha = value, 1f, fadeDuration)
            .SetDelay(showDelay)
            .SetUpdate(true)
            .OnStart(Appear);
    }

    // A fail uses the area's own messages when it has them
    private string[] MessagesFor(bool pass)
    {
        int count = run.LeaksThisArea;

        if (pass)
            return count == 0 ? perfectMessageLines : count == 1 ? oneMessageLines : passMessagesLines;

        Area_Settings area = run.Area;
        return area != null && area.failMessages != null && area.failMessages.Length > 0 ? area.failMessages
            : count == 1 ? oneFailMessageLines : failMessagesLines;
    }

    // The count, and one goblin's he or she (the same all through a message)
    private string Fill(string line)
    {
        bool he = Random.value < 0.5f;

        return line.Replace("{0}", run.LeaksThisArea.ToString())
            .Replace("{He}", he ? "He" : "She")
            .Replace("{he}", he ? "he" : "she")
            .Replace("{His}", he ? "His" : "Her")
            .Replace("{his}", he ? "his" : "her");
    }

    private static string Pick(string[] lines)
    {
        return lines != null && lines.Length > 0 ? lines[Random.Range(0, lines.Length)] : string.Empty;
    }

    private void Appear()
    {
        panel.SetActive(true);
        shown = true;
        shownAt = Time.unscaledTime;

        // The plane stops where it is and its sound fades away
        if (planeInput != null)
        {
            if (planeInput.TryGetComponent(out Plane_Controller controller))
                controller.Stop();

            if (planeInput.TryGetComponent(out Plane_Audio planeAudio))
                planeAudio.FadeOut();
        }

        for (int i = 0; i < hideOnGameOver.Length; i++)
        {
            if (hideOnGameOver[i] != null)
                hideOnGameOver[i].SetActive(false);
        }

        if (restartAction != null && UpdateManager.Instance != null)
        {
            restartAction.action.Enable();
            ticking = true;
            UpdateManager.Instance.OnUpdate += Tick;
        }

        if (titlePunch != null)
            titlePunch.Punch(punchStrength);

        Utilities.TryPlaySound(passed ? passSound : showSound, transform.position);
    }

    private void Tick()
    {
        if (restartAction.action.WasPressedThisFrame())
            Restart();
    }

    private void StopTicking()
    {
        if (!ticking)
            return;

        ticking = false;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnUpdate -= Tick;
    }

    private bool Ready => shown && !used && Time.unscaledTime >= shownAt + restartDelay;

    // The Restart button's click. Reloads the Run scene for a clean run.
    public void Restart()
    {
        if (!Ready)
            return;

        used = true;
        StopTicking();
        panel.SetActive(false);

        if (edge != null)
            edge.CoverForLoading();

        run.Restart();
    }

    // The Next Area button's click: the panel gives way to the loading cover
    public void NextArea()
    {
        if (!Ready || !passed || edge == null)
            return;

        used = true;
        StopTicking();
        panel.SetActive(false);
        edge.GoToNextArea();
    }
}
