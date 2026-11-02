using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Alt+Shift+S (GameObject > UI > Add Drop Shadow, also on the Hierarchy's right-click menu): gives each selected
// text or image a drop shadow in one go. The shadow is a copy of the graphic only (no other components, no
// children), called "<name> Shadow", placed just above it in the hierarchy so it draws behind, coloured with the UI
// Style's shadow colour and not a raycast target. The main gets a UI_Shadowed pointing at it, with the style's
// default offset (+5, -5); change it there. One undo step removes it all again.
// The shortcut can be rebound in Edit > Shortcuts.
public static class UI_ShadowMenu
{
    private const string MenuPath = "GameObject/UI/Add Drop Shadow &#s";

    [MenuItem(MenuPath, false, 0)]
    private static void AddDropShadow()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add Drop Shadow");

        foreach (GameObject selected in Selection.gameObjects)
            AddTo(selected);

        Undo.CollapseUndoOperations(group);
    }

    [MenuItem(MenuPath, true)]
    private static bool CanAddDropShadow()
    {
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (selected.GetComponent<Graphic>() != null)
                return true;
        }

        return false;
    }

    private static void AddTo(GameObject target)
    {
        Graphic main = target.GetComponent<Graphic>();

        if (main == null || target.transform.parent == null)
            return;

        UI_Shadowed shadowed = target.GetComponent<UI_Shadowed>();

        if (shadowed != null && shadowed.Shadow != null)
        {
            Debug.Log($"{target.name} already has a shadow ({shadowed.Shadow.name}).", shadowed.Shadow);
            return;
        }

        UI_Style style = UI_Style.Default;

        // A copy of the graphic alone, just above the main so it draws behind
        GameObject copy = Object.Instantiate(target, target.transform.parent, false);
        copy.name = target.name + " Shadow";
        copy.transform.SetSiblingIndex(target.transform.GetSiblingIndex());
        Strip(copy, main.GetType());
        Undo.RegisterCreatedObjectUndo(copy, "Add Drop Shadow");

        Graphic shadow = copy.GetComponent<Graphic>();
        shadow.color = style.shadowColour;
        shadow.raycastTarget = false;

        if (shadowed == null)
            shadowed = Undo.AddComponent<UI_Shadowed>(target);

        Undo.RecordObject(shadowed, "Add Drop Shadow");
        SerializedObject serialized = new SerializedObject(shadowed);
        serialized.FindProperty("shadow").objectReferenceValue = shadow;
        serialized.FindProperty("offset").vector2Value = style.shadowOffset;
        serialized.ApplyModifiedProperties();

        // Place it now, so the offset shows straight away
        RectTransform mainRect = (RectTransform)target.transform;
        RectTransform shadowRect = (RectTransform)copy.transform;
        shadowRect.anchoredPosition = mainRect.anchoredPosition + style.shadowOffset;
    }

    // Everything but the transform, the canvas renderer and the graphic itself; children too (TMP rebuilds its own)
    private static void Strip(GameObject copy, System.Type graphicType)
    {
        for (int i = copy.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(copy.transform.GetChild(i).gameObject);

        // A few passes, since a component can't go while another still requires it
        for (int pass = 0; pass < 4; pass++)
        {
            bool removedAny = false;
            Component[] components = copy.GetComponents<Component>();

            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];

                if (component == null || component is Transform || component is CanvasRenderer
                    || component.GetType() == graphicType)
                    continue;

                if (CanRemove(copy, component))
                {
                    Object.DestroyImmediate(component);
                    removedAny = true;
                }
            }

            if (!removedAny)
                break;
        }
    }

    // Whether nothing else left on the object requires this component's type
    private static bool CanRemove(GameObject owner, Component component)
    {
        System.Type type = component.GetType();

        foreach (Component other in owner.GetComponents<Component>())
        {
            if (other == null || other == component)
                continue;

            foreach (object attribute in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
            {
                RequireComponent require = (RequireComponent)attribute;

                if (Requires(require.m_Type0, type) || Requires(require.m_Type1, type) || Requires(require.m_Type2, type))
                    return false;
            }
        }

        return true;
    }

    private static bool Requires(System.Type required, System.Type type)
    {
        return required != null && required.IsAssignableFrom(type);
    }
}
