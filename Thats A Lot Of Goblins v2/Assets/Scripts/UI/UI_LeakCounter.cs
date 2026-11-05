using UnityEngine;

// The health number, next to the enemy counter: how many leaks are left, counting down to 0, when the run fails. Each
// frame with leaks punches it once (harder the more leaked) and flashes it, and its resting colour slides toward Danger
// Colour as the pool runs out. When health comes back (as the next area fades in) it pops up with a green flash. Blank
// when the settings have no limit.
//
// Setup: put this on the counter text (with a UI_Shadowed for its shadow and a UI_Punch for the kicks), assign the
// Run_State, the text's UI_Shadowed and the UI_Punch.
//
// GC: none per frame (TMP's SetText with numbers doesn't allocate). One UpdateManager subscription (OnLateUpdate) and
// one to Run_State.LeaksChanged.
public class UI_LeakCounter : MonoBehaviour
{
    [SerializeField] private Run_State run;

    [Tooltip("The counter text (UI_Shadowed on it, so its shadow matches).")]
    [SerializeField] private UI_Shadowed text;

    [Tooltip("Kicks the counter: usually a UI_Punch on the text.")]
    [SerializeField] private UI_Punch punch;

    [Tooltip("An icon beside the number (UI_Shadowed on it). Flashes and slides to Danger Colour with the number. Optional.")]
    [SerializeField] private UI_Shadowed icon;

    [Tooltip("Kicks the icon with the number: a UI_Punch on the icon. Optional.")]
    [SerializeField] private UI_Punch iconPunch;

    [Tooltip("{0} is the health left. Just \"{0}\" shows the number alone.")]
    [SerializeField] private string healthFormat = "{0}";

    [Header("Punch")]
    [Tooltip("Punch for one leak in a frame, as a share of the UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float leakPunch = 1.5f;

    [Tooltip("Extra punch per extra leak in the same frame.")]
    [Min(0f)] [SerializeField] private float punchPerLeak = 0.25f;

    [Tooltip("The hardest a single frame of leaks can punch.")]
    [Min(0f)] [SerializeField] private float maxPunch = 4f;

    [Header("Colour")]
    [Tooltip("The colour the number flashes on a leak, fading back.")]
    [SerializeField] private Color flashColour = new Color(1f, 0.25f, 0.2f);

    [Tooltip("Seconds the flash takes to fade back.")]
    [Min(0.01f)] [SerializeField] private float flashDuration = 0.35f;

    [Tooltip("The resting colour when the pool is nearly out.")]
    [SerializeField] private Color dangerColour = new Color(1f, 0.35f, 0.25f);

    [Tooltip("The share of the limit used before the resting colour starts sliding to Danger Colour.")]
    [Range(0f, 1f)] [SerializeField] private float dangerFrom = 0.5f;

    [Header("Health Back")]
    [Tooltip("Punch when health comes back as the next area starts.")]
    [Min(0f)] [SerializeField] private float healthBackPunch = 3f;

    [Tooltip("The colour the number flashes when health comes back.")]
    [SerializeField] private Color healthBackColour = new Color(0.4f, 1f, 0.45f);

    [Tooltip("Seconds from the screen starting to fade in to the number going up.")]
    [Min(0f)] [SerializeField] private float healthBackDelay = 0.3f;

    [Header("Sound")]
    [Tooltip("Played once per frame that had leaks. Optional; make it 2D.")]
    [SerializeField] private SoundData leakSound;

    [Tooltip("Played when health comes back. Optional; make it 2D.")]
    [SerializeField] private SoundData healthBackSound;

    private Color currentFlash;
    private float healthBackTimer = -1f;
    private int leaksThisFrame;
    private Color baseColour = Color.white;
    private Color restColour = Color.white;
    private Color iconBaseColour = Color.white;
    private Color iconRestColour = Color.white;
    private float flash;    // 0-1, fading out

    private void Awake()
    {
        if (text != null && text.Main != null)
            baseColour = text.Main.color;

        if (icon != null && icon.Main != null)
            iconBaseColour = icon.Main.color;

        restColour = baseColour;
        iconRestColour = iconBaseColour;
    }

    private void OnEnable()
    {
        if (run == null || text == null)
        {
            Debug.LogWarning($"{name}: UI_LeakCounter needs a Run_State and a UI_Shadowed text.", this);
            return;
        }

        if (icon == text)
            Debug.LogWarning($"{name}: Icon is the same UI_Shadowed as Text, so the icon can't flash on its own. " +
                             "Assign the icon image's own UI_Shadowed (or leave Icon empty).", this);

        run.LeaksChanged += OnLeak;
        run.HealthRestored += OnHealthRestored;
        run.LimitChanged += Show;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate += Tick;

        Show();
    }

    // Run_State reads its limit in Awake, which may come after this OnEnable; by Start every Awake has run
    private void Start()
    {
        if (run != null && text != null)
            Show();
    }

    private void OnDisable()
    {
        if (run != null)
        {
            run.LeaksChanged -= OnLeak;
            run.HealthRestored -= OnHealthRestored;
            run.LimitChanged -= Show;
        }

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;
    }

    private void OnLeak() => leaksThisFrame++;

    // Health came back as the next area faded in: the number shows what it was until the delay, then pops up
    private void OnHealthRestored() => healthBackTimer = healthBackDelay;

    private void Tick()
    {
        int leaked = leaksThisFrame;
        leaksThisFrame = 0;

        if (healthBackTimer >= 0f)
        {
            healthBackTimer -= Time.unscaledDeltaTime;

            if (healthBackTimer < 0f)
            {
                Show();
                flash = 1f;
                currentFlash = healthBackColour;

                if (punch != null)
                    punch.Punch(healthBackPunch);

                if (iconPunch != null)
                    iconPunch.Punch(healthBackPunch);

                Utilities.TryPlaySound(healthBackSound, transform.position);
            }
        }

        if (leaked > 0)
        {
            Show();
            flash = 1f;
            currentFlash = flashColour;

            float strength = Mathf.Min(leakPunch + punchPerLeak * (leaked - 1), maxPunch);

            if (punch != null)
                punch.Punch(strength);

            if (iconPunch != null)
                iconPunch.Punch(strength);

            Utilities.TryPlaySound(leakSound, transform.position);
        }

        if (flash > 0f)
        {
            flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime / flashDuration);
            text.SetColor(Color.Lerp(restColour, currentFlash, flash));

            if (icon != null)
                icon.SetColor(Color.Lerp(iconRestColour, currentFlash, flash));
        }
    }

    private void Show()
    {
        if (run.LeakLimit <= 0)
        {
            text.SetText(string.Empty);
            return;
        }

        text.SetText(healthFormat, run.LeaksLeft);

        float used = (float)run.Leaks / run.LeakLimit;
        float danger = Mathf.InverseLerp(dangerFrom, 1f, used);
        restColour = Color.Lerp(baseColour, dangerColour, danger);
        text.SetColor(restColour);

        if (icon != null)
        {
            iconRestColour = Color.Lerp(iconBaseColour, dangerColour, danger);
            icon.SetColor(iconRestColour);
        }
    }
}
