using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Juice for a Unity Button with a TextMeshPro label. Hovering (or selecting with a pad) springs it up, punches it, and
// starts an idle rock and pulse; pressing squashes it; clicking punches it hard and plays a sound. The text's own colour
// can change while hovered and fades back to the colour it was authored with. The Button still does the clicking, so
// onClick works as normal; its own colour tint is switched off here.
//
// Setup: on the Button object (it stays still, so it's the hit area): a Graphic with Raycast Target on, e.g. an Image
// with alpha 0. Make the label a child with Raycast Target off, and add a UI_Punch to it (or to a child holding the
// label and a background), with Unscaled Time on. Assign Label and Punch. The moving part mustn't be the hit area, or
// the edge of the button flickers between hovered and not as it scales and rocks.
//
// GC: none per frame. One Button.onClick listener. One UpdateManager subscription (OnLateUpdate), only while the colour
// is fading. UI_Punch subscribes while it moves.
[RequireComponent(typeof(Button))]
public class UI_Button : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Tooltip("The button's text. Its UI_Shadowed, if it has one, is used so the shadow keeps up.")]
    [SerializeField] private TMP_Text label;

    [Tooltip("Springs the button: a UI_Punch on the label or a child that holds it, not on this object.")]
    [SerializeField] private UI_Punch punch;

    [Header("Hover")]
    [Tooltip("The size it springs up to while hovered.")]
    [Min(0f)] [SerializeField] private float hoverScale = 1.15f;

    [Tooltip("The kick as the hover starts, as a share of the UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float hoverPunch = 1f;

    [Header("Idle While Hovered")]
    [Tooltip("Degrees it rocks either way. 0 for no rock.")]
    [Min(0f)] [SerializeField] private float rockDegrees = 4f;

    [Tooltip("How fast it rocks, radians per second.")]
    [Min(0f)] [SerializeField] private float rockSpeed = 5f;

    [Tooltip("How much the size pulses (0.03 is 3%). 0 for no pulse.")]
    [Min(0f)] [SerializeField] private float breathe = 0.03f;

    [Tooltip("How fast it pulses, radians per second.")]
    [Min(0f)] [SerializeField] private float breatheSpeed = 8f;

    [Header("Press and Click")]
    [Tooltip("The size it squashes to while held down.")]
    [Min(0f)] [SerializeField] private float pressScale = 0.92f;

    [Tooltip("The kick on a click, as a share of the UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float clickPunch = 3f;

    [Header("Text Colour")]
    [Tooltip("Change the text's own colour while hovered or selected.")]
    [SerializeField] private bool changeColour = true;

    [Tooltip("The text's colour while hovered or selected.")]
    [SerializeField] private Color selectedColour = new Color(1f, 0.75f, 0.25f);

    [Tooltip("Seconds the colour takes to change either way. 0 is instant.")]
    [Min(0f)] [SerializeField] private float colourFade = 0.08f;

    [Header("Sound")]
    [Tooltip("Played when the hover starts. Optional; make it 2D.")]
    [SerializeField] private SoundData hoverSound;

    [Tooltip("Played on a click. Optional; make it 2D.")]
    [SerializeField] private SoundData clickSound;

    private Button button;
    private UI_Shadowed shadowed;
    private Color baseColour = Color.white;
    private float colourAmount;
    private bool hovered;
    private bool selected;
    private bool pressed;
    private bool highlighted;
    private bool fading;

    private void Reset()
    {
        label = GetComponentInChildren<TMP_Text>();
        punch = GetComponentInChildren<UI_Punch>();
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        if (label != null)
        {
            shadowed = label.GetComponent<UI_Shadowed>();
            baseColour = label.color;
        }

        if (punch != null && punch.gameObject == gameObject)
            Debug.LogWarning($"{name}: UI_Button's UI_Punch is on the hit area, so the hover will flicker at the edges. " +
                             "Put it on a child.", this);
    }

    private void OnEnable() => button.onClick.AddListener(OnClicked);

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClicked);
        StopFading();

        hovered = false;
        selected = false;
        pressed = false;
        highlighted = false;
        colourAmount = 0f;
        ShowColour();

        if (punch != null)
        {
            punch.SetIdle(0f, 0f, 0f, 0f);
            punch.SetTargetScale(1f, true);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;

        // A mouse click leaves the button selected; let go of that, or it stays lit after the mouse has moved off
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);

        Refresh();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        pressed = true;
        Refresh();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        Refresh();
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        Refresh();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        Refresh();
    }

    private void OnClicked()
    {
        if (punch != null)
            punch.Punch(clickPunch);

        Utilities.TryPlaySound(clickSound, transform.position);
    }

    // Works out the look from hovered, selected and pressed, and only kicks when the hover really starts
    private void Refresh()
    {
        bool lit = (hovered || selected) && button.interactable;
        bool started = lit && !highlighted;
        bool changed = lit != highlighted;
        highlighted = lit;

        if (punch != null)
        {
            punch.SetTargetScale(lit ? (pressed ? pressScale : hoverScale) : 1f);

            if (changed)
            {
                if (lit)
                    punch.SetIdle(rockDegrees, rockSpeed, breathe, breatheSpeed);
                else
                    punch.SetIdle(0f, 0f, 0f, 0f);
            }

            if (started)
                punch.Punch(hoverPunch);
        }

        if (started)
            Utilities.TryPlaySound(hoverSound, transform.position);

        if (changed && changeColour && label != null)
            StartFading();
    }

    private void StartFading()
    {
        if (colourFade <= 0f || UpdateManager.Instance == null)
        {
            colourAmount = highlighted ? 1f : 0f;
            ShowColour();
            return;
        }

        if (fading)
            return;

        fading = true;
        UpdateManager.Instance.OnLateUpdate += FadeColour;
    }

    private void StopFading()
    {
        if (!fading)
            return;

        fading = false;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= FadeColour;
    }

    private void FadeColour()
    {
        float target = highlighted ? 1f : 0f;
        colourAmount = Mathf.MoveTowards(colourAmount, target, Time.unscaledDeltaTime / colourFade);
        ShowColour();

        if (colourAmount == target)
            StopFading();
    }

    // Sets the text's own colour, through its UI_Shadowed if it has one
    private void ShowColour()
    {
        if (label == null)
            return;

        Color colour = Color.Lerp(baseColour, selectedColour, colourAmount);

        if (shadowed != null)
            shadowed.SetColor(colour);
        else
            label.color = colour;
    }
}
