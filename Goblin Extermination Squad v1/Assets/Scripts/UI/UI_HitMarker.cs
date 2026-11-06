using UnityEngine;
using UnityEngine.UI;

// One hit marker on the canvas: just its references. UI_HitMarkers pools these, places them over the world point they
// mark and animates them; this has no Update of its own. Ported from the Imps project's HitMarkerUI.
// Image is tinted (white hits, red kills); Shadow keeps its own colour. Both swap to the kill sprite on kills.
//
// GC: none. No subscriptions.
public class UI_HitMarker : MonoBehaviour
{
    [SerializeField] private RectTransform rect;
    [SerializeField] private Image image;
    [Tooltip("Optional: the shadow behind the image. It swaps sprite with the image but isn't tinted.")]
    [SerializeField] private Image shadow;

    private Sprite imageSprite;
    private Sprite shadowSprite;
    private bool cached;

    public RectTransform Rect => rect;
    public Image Image => image;

    // Kill sprite on kills, the prefab's own otherwise. Null kill sprite keeps the prefab's.
    public void SetSprite(Sprite killSprite, bool kill)
    {
        if (!cached)
        {
            imageSprite = image != null ? image.sprite : null;
            shadowSprite = shadow != null ? shadow.sprite : null;
            cached = true;
        }

        bool swap = kill && killSprite != null;

        if (image != null)
            image.sprite = swap ? killSprite : imageSprite;

        if (shadow != null)
            shadow.sprite = swap ? killSprite : shadowSprite;
    }

    private void Reset()
    {
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
    }
}
