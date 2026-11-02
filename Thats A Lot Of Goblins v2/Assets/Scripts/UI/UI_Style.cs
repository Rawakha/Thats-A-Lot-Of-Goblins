using UnityEngine;

// The HUD's shared look rules, written once and used by every UI_Shadowed element. For now: how a dimmed element
// (not selected, unavailable) looks. Dimming darkens and desaturates the main text or image only; shadows stay as
// they are, so the outline stays crisp.
//
// Setup: Create > Goblins/UI Style, saved as Resources/UI/UI_Style so every element finds it. Without one, the
// defaults below are used. An element can be given a different style of its own.
//
// GC: the default is loaded once, the first time it's needed.
[CreateAssetMenu(menuName = "Goblins/UI Style", fileName = "UI_Style")]
public class UI_Style : ScriptableObject
{
    private const string ResourcePath = "UI/UI_Style";

    [Header("Shadows")]
    [Tooltip("The colour Add Drop Shadow (Alt+Shift+S) gives a new shadow.")]
    public Color shadowColour = new Color(0f, 0f, 0f, 1f);

    [Tooltip("The offset Add Drop Shadow gives a new shadow, in canvas units: +X right, -Y down.")]
    public Vector2 shadowOffset = new Vector2(5f, -5f);

    [Header("Dimmed Text")]
    [Tooltip("Brightness of dimmed text, as a share of its colour: 0.75 turns white into a light grey.")]
    [Range(0f, 1f)] public float textBrightness = 0.75f;

    [Tooltip("Colour left in dimmed text: 1 keeps it, 0 turns it grey.")]
    [Range(0f, 1f)] public float textSaturation = 1f;

    [Header("Dimmed Images")]
    [Tooltip("Brightness of dimmed images, as a share of their colour. Keep it high enough that the colour is still recognisable.")]
    [Range(0f, 1f)] public float imageBrightness = 0.55f;

    [Tooltip("Colour left in dimmed images: 1 keeps it, 0 turns them grey.")]
    [Range(0f, 1f)] public float imageSaturation = 0.7f;

    private static UI_Style loaded;
    private static UI_Style fallback;
    private static bool searched;

    // The project's style (Resources/UI/UI_Style), or the defaults
    public static UI_Style Default
    {
        get
        {
            if (loaded != null)
                return loaded;

            if (!searched)
            {
                searched = true;
                loaded = Resources.Load<UI_Style>(ResourcePath);

                if (loaded != null)
                    return loaded;
            }

            if (fallback == null)
            {
                fallback = CreateInstance<UI_Style>();
                fallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return fallback;
        }
    }

    // A colour as it looks dimmed: darker and less saturated, same alpha
    public Color Dim(Color colour, bool isText)
    {
        float brightness = isText ? textBrightness : imageBrightness;
        float saturation = isText ? textSaturation : imageSaturation;

        Color.RGBToHSV(colour, out float h, out float s, out float v);
        Color dimmed = Color.HSVToRGB(h, s * saturation, v * brightness);
        dimmed.a = colour.a;
        return dimmed;
    }
}
