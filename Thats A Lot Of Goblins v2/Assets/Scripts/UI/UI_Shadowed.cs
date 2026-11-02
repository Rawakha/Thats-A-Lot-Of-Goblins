using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A HUD text or image with a drop-shadow copy behind it, treated as one thing. Put this on the main text or image
// and point it at its shadow; then code only ever talks to this:
//   - SetText / SetSprite / SetFill change the main and the shadow together, so the shadow always matches (Copy Fill
//     off keeps the shadow whole while the main fills, e.g. a cooldown filling over a full silhouette);
//   - SetColor / SetDimmed change the main only (the shadow keeps its own colour): dimmed is the shared UI_Style look
//     for anything not selected or unavailable, so every panel dims the same way;
//   - the shadow sits Offset away from the main and follows its movement, rotation, scale and size every frame, so
//     kicks, sway and tweens on the main carry the shadow along. The offset doesn't turn with the main, like a real
//     drop shadow under a light that stays put;
//   - turning this component's object off hides the shadow too.
// In edit mode the shadow also copies the main's text (font, size, alignment) or sprite as you edit, so it's always
// a matching copy.
//
// Setup: select a text or image and press Alt+Shift+S (GameObject > UI > Add Drop Shadow): it makes the shadow and
// adds this. By hand: the shadow is a sibling placed above the main in the hierarchy (so it draws behind); a sibling
// called "<main's name> Shadow" is found on its own. Don't put the main and the shadow both under a layout group: the
// layout would place the shadow on its own.
//
// GC: none in play. Setting text with a number doesn't allocate (TMP's SetText). One subscription to
// Canvas.willRenderCanvases while enabled; the shadow only moves on frames when the main has moved.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public class UI_Shadowed : MonoBehaviour
{
    [Tooltip("The copy drawn behind. Found on its own when it's a sibling called \"<this name> Shadow\".")]
    [SerializeField] private Graphic shadow;

    [Tooltip("Where the shadow sits from the main, in canvas units: +X right, -Y down.")]
    [SerializeField] private Vector2 offset = new Vector2(5f, -5f);

    [Tooltip("The shadow's size as a multiple of the main's: above 1 a bigger shadow peeks out all round, below 1 a smaller one.")]
    [Min(0f)] [SerializeField] private float shadowScale = 1f;

    [Tooltip("Images: SetSprite changes the shadow's sprite too. Off for a shadow with its own sprite.")]
    [SerializeField] private bool copySprite = true;

    [Tooltip("Images: the shadow fills like the main (same fill type, method and amount). Off: the shadow stays whole while the main fills.")]
    [SerializeField] private bool copyFill = true;

    [Tooltip("How this element looks dimmed. Empty uses the project's UI Style (Resources/UI/UI_Style).")]
    [SerializeField] private UI_Style style;

    private Graphic main;
    private TMP_Text mainText;
    private TMP_Text shadowText;
    private Image mainImage;
    private Image shadowImage;
    private RectTransform mainRect;
    private RectTransform shadowRect;

    private Color baseColour;
    private float authoredAlpha;
    private float shadowAlpha;
    private bool dimmed;
    private bool ready;
    private bool placed;

    public Graphic Main { get { Init(); return main; } }
    public Graphic Shadow => shadow;
    public RectTransform Rect { get { Init(); return mainRect; } }
    public bool Dimmed => dimmed;

    public Vector2 Offset
    {
        get => offset;
        set { offset = value; placed = false; }
    }

    private void Awake()
    {
        Init();
    }

    private void OnEnable()
    {
        Init();

        if (shadow != null && Application.isPlaying)
            shadow.enabled = true;

        placed = false;
        Canvas.willRenderCanvases += Follow;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= Follow;

        if (shadow != null && Application.isPlaying)
            shadow.enabled = false;
    }

    // Callers can use this before its Awake (a panel's OnEnable can run first), so everything starts here
    private void Init()
    {
        if (ready)
            return;

        main = GetComponent<Graphic>();

        if (main == null)
            return;

        ready = true;
        mainText = main as TMP_Text;
        mainImage = main as Image;
        mainRect = main.rectTransform;
        baseColour = main.color;
        authoredAlpha = baseColour.a;
        BindShadow();
    }

    private void BindShadow()
    {
        if (shadow == main)
            shadow = null;

        shadowText = shadow as TMP_Text;
        shadowImage = shadow as Image;
        shadowRect = shadow != null ? shadow.rectTransform : null;
        shadowAlpha = shadow != null ? shadow.color.a : 1f;

        if (shadow != null && shadowRect.parent != mainRect.parent)
            Debug.LogWarning($"{name}: UI_Shadowed's shadow isn't a sibling, so following may look off.", this);

        // Images: the shadow fills the same way, so cooldown fills and the like match; or stays whole
        if (mainImage != null && shadowImage != null && !copyFill)
        {
            shadowImage.fillAmount = 1f;
        }
        else if (mainImage != null && shadowImage != null)
        {
            shadowImage.type = mainImage.type;
            shadowImage.fillMethod = mainImage.fillMethod;
            shadowImage.fillOrigin = mainImage.fillOrigin;
            shadowImage.fillClockwise = mainImage.fillClockwise;
            shadowImage.fillAmount = mainImage.fillAmount;
        }

        placed = false;
    }

    public void SetText(string text)
    {
        Init();

        if (mainText != null)
            mainText.SetText(text);

        if (shadowText != null)
            shadowText.SetText(text);
    }

    // Numbers don't allocate: SetText("{0}", ammo)
    public void SetText(string format, float arg0)
    {
        Init();

        if (mainText != null)
            mainText.SetText(format, arg0);

        if (shadowText != null)
            shadowText.SetText(format, arg0);
    }

    public void SetText(string format, float arg0, float arg1)
    {
        Init();

        if (mainText != null)
            mainText.SetText(format, arg0, arg1);

        if (shadowText != null)
            shadowText.SetText(format, arg0, arg1);
    }

    public void SetSprite(Sprite sprite)
    {
        Init();

        if (mainImage != null)
            mainImage.sprite = sprite;

        if (copySprite && shadowImage != null)
            shadowImage.sprite = sprite;
    }

    // Images with a Filled type: 0-1 on the main, and on the shadow with Copy Fill
    public void SetFill(float amount)
    {
        Init();

        if (mainImage != null)
            mainImage.fillAmount = amount;

        if (copyFill && shadowImage != null)
            shadowImage.fillAmount = amount;
    }

    // The main's colour (the shadow keeps its own; its alpha follows the main's)
    public void SetColor(Color colour)
    {
        Init();
        baseColour = colour;
        ApplyColour();
    }

    // The shared dimmed look on the main only, for anything not selected or unavailable
    public void SetDimmed(bool value)
    {
        Init();

        if (dimmed == value)
            return;

        dimmed = value;
        ApplyColour();
    }

    private void ApplyColour()
    {
        UI_Style look = style != null ? style : UI_Style.Default;
        main.color = dimmed ? look.Dim(baseColour, mainText != null) : baseColour;

        if (shadow == null)
            return;

        Color shadowColour = shadow.color;
        shadowColour.a = authoredAlpha > 0f ? shadowAlpha * baseColour.a / authoredAlpha : shadowAlpha;
        shadow.color = shadowColour;
    }

    // Just before the canvas draws, after every script has moved things this frame
    private void Follow()
    {
        if (!ready || shadow == null)
            return;

        bool editing = !Application.isPlaying;
        bool changed = editing && CopyContent();

        if (!placed || mainRect.hasChanged)
        {
            placed = true;
            mainRect.hasChanged = false;
            changed |= Place();
        }

        // Only real changes are marked, so the scene isn't dirtied every frame and saves normally
        if (editing && changed)
            MarkDirty();
    }

    // Puts the shadow on the main plus the offset; only writes what differs. True if anything moved.
    private bool Place()
    {
        Vector2 position = mainRect.anchoredPosition + offset;

        if (shadowRect.anchorMin == mainRect.anchorMin && shadowRect.anchorMax == mainRect.anchorMax
            && shadowRect.pivot == mainRect.pivot && shadowRect.sizeDelta == mainRect.sizeDelta
            && shadowRect.anchoredPosition == position && shadowRect.localRotation == mainRect.localRotation
            && shadowRect.localScale == mainRect.localScale * shadowScale)
            return false;

        shadowRect.anchorMin = mainRect.anchorMin;
        shadowRect.anchorMax = mainRect.anchorMax;
        shadowRect.pivot = mainRect.pivot;
        shadowRect.sizeDelta = mainRect.sizeDelta;
        shadowRect.anchoredPosition = position;
        shadowRect.localRotation = mainRect.localRotation;
        shadowRect.localScale = mainRect.localScale * shadowScale;
        return true;
    }

    // Edit mode: changes made from code have to be marked, or the scene doesn't save them
    private void MarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(shadowRect);
        UnityEditor.EditorUtility.SetDirty(shadow);
#endif
    }

    // Edit mode only: keep the shadow a copy of what's being edited on the main. True if anything changed.
    private bool CopyContent()
    {
        bool changed = false;

        if (mainText != null && shadowText != null)
        {
            if (shadowText.text != mainText.text)
            {
                shadowText.text = mainText.text;
                changed = true;
            }

            // Auto size works the size out itself, so its range is copied, not the size it picked
            bool auto = mainText.enableAutoSizing;
            bool sizeDiffers = auto
                ? shadowText.fontSizeMin != mainText.fontSizeMin || shadowText.fontSizeMax != mainText.fontSizeMax
                : shadowText.fontSize != mainText.fontSize;

            if (shadowText.font != mainText.font || sizeDiffers || shadowText.enableAutoSizing != auto
                || shadowText.alignment != mainText.alignment || shadowText.fontStyle != mainText.fontStyle
                || shadowText.characterSpacing != mainText.characterSpacing)
            {
                shadowText.font = mainText.font;
                shadowText.enableAutoSizing = auto;

                if (auto)
                {
                    shadowText.fontSizeMin = mainText.fontSizeMin;
                    shadowText.fontSizeMax = mainText.fontSizeMax;
                }
                else
                {
                    shadowText.fontSize = mainText.fontSize;
                }

                shadowText.alignment = mainText.alignment;
                shadowText.fontStyle = mainText.fontStyle;
                shadowText.characterSpacing = mainText.characterSpacing;
                changed = true;
            }
        }

        if (copySprite && mainImage != null && shadowImage != null && shadowImage.sprite != mainImage.sprite)
        {
            shadowImage.sprite = mainImage.sprite;
            changed = true;
        }

        return changed;
    }

#if UNITY_EDITOR
    // Finds a sibling called "<name> Shadow" when the shadow isn't set, and re-places it when the offset changes
    private void OnValidate()
    {
        if (shadow == null && transform.parent != null)
        {
            Transform found = transform.parent.Find(name + " Shadow");

            if (found != null && found != transform)
                shadow = found.GetComponent<Graphic>();
        }

        if (ready)
            BindShadow();
    }
#endif
}
