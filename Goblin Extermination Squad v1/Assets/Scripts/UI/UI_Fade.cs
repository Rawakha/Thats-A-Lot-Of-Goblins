using TMPro;
using UnityEngine;

// The full-screen cover: the fade, and the loading screen. A Canvas Group fades it and everything under it. It starts covered
// as the scene wakes, so nothing shows until Run_Edge fades in once the area is ready. Transitions cover at once and show
// the loading text with its dots animating ("Loading.", "Loading..", "Loading..."), which fades away with the cover.
// Keeps time unscaled, so hitstop can't stall it. The dots can't move during the moment the scene itself reloads.
//
// Setup: an Image stretched over the whole canvas in the same colour as the Game Over background, fully opaque, Raycast
// Target off, with this on it (it adds a Canvas Group if there isn't one). Keep it above the HUD but below the Game Over
// screen in the hierarchy. Put a text as a child and assign it as Loading Content.
//
// GC: none per frame; the loading strings are built once. One UpdateManager subscription (OnLateUpdate), only while fading
// or loading.
public class UI_Fade : MonoBehaviour
{
    [Tooltip("Seconds the cover takes to fade away, showing the game.")]
    [Min(0f)] [SerializeField] private float fadeInTime = 0.4f;

    [Tooltip("Seconds the cover takes to fade over the game (the map-limit respawn).")]
    [Min(0f)] [SerializeField] private float fadeOutTime = 0.5f;

    [Tooltip("Shown while covered for loading: a child with the text on it. Optional.")]
    [SerializeField] private GameObject loadingContent;

    [Tooltip("The loading text, without its dots.")]
    [SerializeField] private string loadingLine = "Loading";

    [Tooltip("Seconds between each step of the dots.")]
    [Min(0.02f)] [SerializeField] private float dotInterval = 0.3f;

    private CanvasGroup group;
    private TMP_Text loadingText;
    private UI_Shadowed loadingShadowed;
    private string[] loadingSteps;
    private float from;
    private float to;
    private float duration;
    private float time;
    private float dotTime;
    private int dotStep;
    private bool fading;
    private bool loading;
    private bool ticking;

    public bool Done => !fading;

    private void Awake() => Cover();

    private void Init()
    {
        if (group != null)
            return;

        if (!TryGetComponent(out group))
            group = gameObject.AddComponent<CanvasGroup>();

        group.interactable = false;
        group.blocksRaycasts = false;

        loadingSteps = new[] { loadingLine + ".", loadingLine + "..", loadingLine + "..." };

        if (loadingContent != null)
        {
            loadingShadowed = loadingContent.GetComponentInChildren<UI_Shadowed>(true);
            loadingText = loadingContent.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void OnDisable() => StopTicking();

    // Fully covered at once, showing the loading text
    public void Cover()
    {
        Set(1f);

        if (loadingContent == null)
            return;

        loadingContent.SetActive(true);
        loading = true;
        dotTime = 0f;
        dotStep = 0;
        ShowDots();
        StartTicking();
    }

    // Jumps straight to this amount of cover, 0 clear to 1 covered
    public void Set(float alpha)
    {
        Init();
        fading = false;
        to = alpha;
        group.alpha = alpha;

        if (alpha <= 0f)
            EndLoading();

        if (!loading)
            StopTicking();
    }

    public void FadeIn() => FadeTo(0f, fadeInTime);

    public void FadeOut() => FadeTo(1f, fadeOutTime);

    // Fades to this amount of cover over this many seconds
    public void FadeTo(float alpha, float seconds)
    {
        Init();

        if (seconds <= 0f || UpdateManager.Instance == null)
        {
            Set(alpha);
            return;
        }

        from = group.alpha;
        to = alpha;
        duration = seconds;
        time = 0f;
        fading = true;
        StartTicking();
    }

    private void Tick()
    {
        float deltaTime = Time.unscaledDeltaTime;

        if (loading)
        {
            dotTime += deltaTime;

            if (dotTime >= dotInterval)
            {
                dotTime -= dotInterval;
                dotStep = (dotStep + 1) % loadingSteps.Length;
                ShowDots();
            }
        }

        if (fading)
        {
            time += deltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, time / duration));

            if (time >= duration)
            {
                fading = false;

                if (to <= 0f)
                    EndLoading();
            }
        }

        if (!fading && !loading)
            StopTicking();
    }

    private void ShowDots()
    {
        if (loadingShadowed != null)
            loadingShadowed.SetText(loadingSteps[dotStep]);
        else if (loadingText != null)
            loadingText.text = loadingSteps[dotStep];
    }

    private void EndLoading()
    {
        loading = false;

        if (loadingContent != null)
            loadingContent.SetActive(false);
    }

    private void StartTicking()
    {
        if (ticking || UpdateManager.Instance == null)
            return;

        ticking = true;
        UpdateManager.Instance.OnLateUpdate += Tick;
    }

    private void StopTicking()
    {
        if (!ticking)
            return;

        ticking = false;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;
    }
}
