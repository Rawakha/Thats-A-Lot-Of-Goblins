using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Editor tool: resizes a whole UI panel as if its transform were scaled, but writes the result into the elements
// themselves, so every transform can stay at 1,1,1. Put it on a parent (the Weapon Panel), set Scale and press Bake
// Scale: the parent and everything under it come out Scale times bigger, then Scale goes back to 1.
//
// Baked (on the parent and every child):
//   - rect sizes and positions (anchors are fractions, so they're unchanged; offsets and sizes are multiplied);
//   - TextMeshPro font size, auto size min/max and margins;
//   - sliced/tiled image borders (Pixels Per Unit Multiplier), so 9-slices keep their look;
//   - layout groups' padding, spacing and grid cell sizes, and layout elements' sizes;
//   - Outline/Shadow effect distances and UI_Shadowed offsets.
// Bake Transform Scale does the same with the scale already on this object's transform, then sets it back to 1, for
// a panel that was scaled by hand.
//
// Preview toggles a look at the result before baking: it scales this object's transform by Scale for now (and follows
// Scale as you change it), and turning it off puts the transform back. Baking ends the preview first.
//
// Uniform scale only (fonts can't be stretched one way). Padding is whole pixels, so it rounds. One undo step.
// Nothing runs in play: the component does nothing at runtime and can be removed after baking.
//
// GC/subscriptions: none at runtime.
[DisallowMultipleComponent]
public class UI_ScaleBaker : MonoBehaviour
{
    [Tooltip("How much bigger (above 1) or smaller (below 1) Bake Scale makes this panel and everything under it.")]
    [Min(0.01f)] [SerializeField] private float scale = 1f;

    // Saved, so a preview survives a script reload or scene save and can still be undone exactly
    [SerializeField, HideInInspector] private bool previewing;
    [SerializeField, HideInInspector] private Vector3 scaleBeforePreview = Vector3.one;

#if UNITY_EDITOR
    [InspectorButton("Toggle Preview")]
    private void TogglePreview()
    {
        UnityEditor.Undo.RecordObject(transform, "Toggle UI Scale Preview");

        if (previewing)
        {
            EndPreview();
            return;
        }

        previewing = true;
        scaleBeforePreview = transform.localScale;
        ShowPreview();
    }

    private void ShowPreview()
    {
        transform.localScale = new Vector3(scaleBeforePreview.x * scale, scaleBeforePreview.y * scale, scaleBeforePreview.z);
    }

    private void EndPreview()
    {
        if (!previewing)
            return;

        previewing = false;
        transform.localScale = scaleBeforePreview;
    }

    // Changing Scale while previewing updates the preview
    private void OnValidate()
    {
        // After the inspector change, not during it (Unity warns about moving rect transforms inside OnValidate)
        if (previewing)
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && previewing)
                    ShowPreview();
            };
    }

    [InspectorButton("Bake Scale")]
    private void BakeScale()
    {
        UnityEditor.Undo.RecordObject(transform, "Bake UI Scale");
        EndPreview();

        if (Mathf.Approximately(scale, 1f))
            return;

        Bake(scale, "Bake UI Scale");
        scale = 1f;
    }

    [InspectorButton("Bake Transform Scale")]
    private void BakeTransformScale()
    {
        UnityEditor.Undo.RecordObject(transform, "Bake UI Transform Scale");
        EndPreview();

        Vector3 current = transform.localScale;

        if (!Mathf.Approximately(current.x, current.y))
            Debug.LogWarning($"{name}: the transform isn't scaled evenly; baking its X scale ({current.x}).", this);

        if (Mathf.Approximately(current.x, 1f))
            return;

        Bake(current.x, "Bake UI Transform Scale");
        transform.localScale = new Vector3(1f, 1f, current.z);
    }

    private void Bake(float factor, string undoName)
    {
        UnityEditor.Undo.RegisterFullObjectHierarchyUndo(gameObject, undoName);

        // The parent grows around its pivot, the same as scaling it would
        if (transform is RectTransform root)
        {
            Vector2 size = root.rect.size;
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x * factor);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y * factor);
            BakeContent(root.gameObject, factor);
        }

        // Children: anchors are fractions of the parent's (now bigger) rect, so only their pixel offsets scale
        foreach (RectTransform child in GetComponentsInChildren<RectTransform>(true))
        {
            if (child == transform)
                continue;

            child.anchoredPosition *= factor;
            child.sizeDelta *= factor;
            BakeContent(child.gameObject, factor);
        }

        foreach (Component component in GetComponentsInChildren<Component>(true))
        {
            if (component == null)
                continue;

            // Marked so the scene (or prefab instance) saves the baked values
            UnityEditor.EditorUtility.SetDirty(component);

            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(component))
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }

    // Everything on one object that's measured in pixels
    private static void BakeContent(GameObject target, float factor)
    {
        foreach (TMP_Text text in target.GetComponents<TMP_Text>())
        {
            text.fontSize *= factor;
            text.fontSizeMin *= factor;
            text.fontSizeMax *= factor;
            text.margin *= factor;
        }

        foreach (Image image in target.GetComponents<Image>())
        {
            if (image.type == Image.Type.Sliced || image.type == Image.Type.Tiled)
                image.pixelsPerUnitMultiplier /= factor;
        }

        foreach (LayoutGroup group in target.GetComponents<LayoutGroup>())
        {
            RectOffset padding = group.padding;
            group.padding = new RectOffset(
                Mathf.RoundToInt(padding.left * factor), Mathf.RoundToInt(padding.right * factor),
                Mathf.RoundToInt(padding.top * factor), Mathf.RoundToInt(padding.bottom * factor));

            if (group is HorizontalOrVerticalLayoutGroup line)
                line.spacing *= factor;

            if (group is GridLayoutGroup grid)
            {
                grid.cellSize *= factor;
                grid.spacing *= factor;
            }
        }

        foreach (LayoutElement element in target.GetComponents<LayoutElement>())
        {
            // -1 means "not set"; leave those alone
            if (element.minWidth > 0f) element.minWidth *= factor;
            if (element.minHeight > 0f) element.minHeight *= factor;
            if (element.preferredWidth > 0f) element.preferredWidth *= factor;
            if (element.preferredHeight > 0f) element.preferredHeight *= factor;
        }

        foreach (Shadow effect in target.GetComponents<Shadow>())
            effect.effectDistance *= factor;

        foreach (UI_Shadowed shadowed in target.GetComponents<UI_Shadowed>())
            shadowed.Offset *= factor;
    }
#endif
}
