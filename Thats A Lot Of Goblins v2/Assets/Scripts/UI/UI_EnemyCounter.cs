using UnityEngine;

// The goblins-remaining counter (top right): how many goblins this wave still has, walking or yet to come out of the
// holes. Kills are gathered over the frame and shown together in LateUpdate, so the number jumps down by however
// many died (a bomb drops it by 30 in one go) and the counter punches once, harder the more died, with a colour flash
// that fades back. Goblins escaping into their goal also count it down, with a smaller punch. A new wave refills it
// with its own pop.
//
// Setup: put this on the counter text (with a UI_Shadowed for its shadow and a UI_Punch for the kicks), assign the
// Wave_Director, the text's UI_Shadowed and the UI_Punch.
//
// GC: none per frame (TMP's SetText with a number doesn't allocate). One UpdateManager subscription (OnLateUpdate) and
// one to Wave_Director.GoblinKilled.
public class UI_EnemyCounter : MonoBehaviour
{
    [SerializeField] private Wave_Director director;

    [Tooltip("The counter text (UI_Shadowed on it, so its shadow matches).")]
    [SerializeField] private UI_Shadowed text;

    [Tooltip("Kicks the counter: usually a UI_Punch on the text.")]
    [SerializeField] private UI_Punch punch;

    [Tooltip("{0} is the number left, e.g. \"{0}\" or \"GOBLINS {0}\".")]
    [SerializeField] private string format = "{0}";

    [Header("Kill Punch")]
    [Tooltip("Punch for one kill in a frame, as a share of the UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float killPunch = 0.6f;

    [Tooltip("Extra punch per extra kill in the same frame, so a bomb slams it.")]
    [Min(0f)] [SerializeField] private float punchPerKill = 0.15f;

    [Tooltip("The hardest a single frame of kills can punch.")]
    [Min(0f)] [SerializeField] private float maxPunch = 4f;

    [Tooltip("Punch when goblins escape into their goal (no kill).")]
    [Min(0f)] [SerializeField] private float escapePunch = 0.3f;

    [Tooltip("Punch when a new wave fills the counter back up.")]
    [Min(0f)] [SerializeField] private float refillPunch = 2f;

    [Header("Flash")]
    [Tooltip("The colour the number flashes on kills, fading back to its own.")]
    [SerializeField] private Color flashColour = new Color(1f, 0.25f, 0.2f);

    [Tooltip("Seconds the flash takes to fade back.")]
    [Min(0.01f)] [SerializeField] private float flashDuration = 0.25f;

    [Tooltip("Kills in one frame for a full-strength flash; fewer flash fainter.")]
    [Min(1)] [SerializeField] private int fullFlashKills = 5;

    [Header("Sound")]
    [Tooltip("Played once per frame that had kills. Optional; make it 2D.")]
    [SerializeField] private SoundData tickSound;

    private int shown = -1;
    private int killsThisFrame;
    private Color baseColour = Color.white;
    private float flash;            // 0-1, fading out
    private float flashStrength;

    private void Awake()
    {
        if (text != null && text.Main != null)
            baseColour = text.Main.color;
    }

    private void OnEnable()
    {
        if (director == null || text == null)
        {
            Debug.LogWarning($"{name}: UI_EnemyCounter needs a Wave_Director and a UI_Shadowed text.", this);
            return;
        }

        director.GoblinKilled += OnKilled;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate += Tick;
    }

    private void OnDisable()
    {
        if (director != null)
            director.GoblinKilled -= OnKilled;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;
    }

    private void OnKilled(Horde_Goblin goblin)
    {
        killsThisFrame++;
    }

    private void Tick()
    {
        int left = director.Alive + director.ToSpawn;
        int kills = killsThisFrame;
        killsThisFrame = 0;

        if (left != shown)
        {
            bool first = shown < 0;
            bool down = !first && left < shown;
            shown = left;
            text.SetText(format, left);

            if (!first && punch != null)
            {
                if (!down)
                    punch.Punch(refillPunch);
                else if (kills > 0)
                    punch.Punch(Mathf.Min(killPunch + punchPerKill * (kills - 1), maxPunch));
                else
                    punch.Punch(escapePunch);
            }

            if (down && kills > 0)
            {
                flash = 1f;
                flashStrength = Mathf.Clamp01((float)kills / fullFlashKills);
                Utilities.TryPlaySound(tickSound, director.transform.position);
            }
        }

        ShowFlash(Time.unscaledDeltaTime);
    }

    // The flash colour fading back to the text's own
    private void ShowFlash(float deltaTime)
    {
        if (flash <= 0f)
            return;

        flash = Mathf.Max(0f, flash - deltaTime / flashDuration);
        text.SetColor(Color.Lerp(baseColour, flashColour, flash * flashStrength));
    }
}
