using UnityEditor;
using UnityEngine;

// Live inspector authoring for Horde_EyeCards. Add it to an Eyes object under Head; create the cards once,
// then sliders update the real child transforms. Undo restores both settings and child objects together.
// GC: editor-only inspector/Undo allocations on edits. No callbacks, per-frame logic or subscriptions.
[CustomEditor(typeof(Horde_EyeCards))]
public class Horde_EyeCardsEditor : InspectorButtonEditor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Put this on an Eyes object under the goblin, with local +Z pointing out of the face. " +
            "Click Create / Update Eye Cards, then use the sliders to place the two quads. Sliders control the " +
            "child transforms; move the Eyes parent to position the whole pair. The render bake combines both " +
            "cards and attaches them to Head, preserving the authored placement even if Eyes is beside Head. " +
            "Open prefab assets in Prefab Mode to edit them.", MessageType.Info);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(target)))
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
                ((Horde_EyeCards)target).Rebuild();
            DrawInspectorButtons();
            if (GUILayout.Button("Create Two Eyes / Cyclops Variation Group"))
                CreateVariations((Horde_EyeCards)target);
        }
    }

    private static void CreateVariations(Horde_EyeCards eyes)
    {
        if (eyes.GetComponentInParent<Horde_VariationGroup>(true) != null)
        {
            Debug.LogWarning("This eye layout already belongs to a variation group. Add options there.", eyes);
            return;
        }
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Eye Variations");
        Transform previousParent = eyes.transform.parent;
        GameObject holder = new GameObject("Eye Variations");
        Undo.RegisterCreatedObjectUndo(holder, "Create Eye Variations");
        Undo.SetTransformParent(holder.transform, previousParent, "Parent Eye Variations");
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;
        holder.transform.localScale = Vector3.one;
        holder.layer = eyes.gameObject.layer;
        // Identity wrapper preserves the user's Eyes local Y=1.2 and every existing card transform.
        Undo.SetTransformParent(eyes.transform, holder.transform, "Move Eye Option");
        GameObject cyclops = new GameObject("Cyclops");
        Undo.RegisterCreatedObjectUndo(cyclops, "Create Cyclops Option");
        Undo.SetTransformParent(cyclops.transform, holder.transform, "Parent Cyclops Option");
        cyclops.transform.localPosition = eyes.transform.localPosition;
        cyclops.transform.localRotation = eyes.transform.localRotation;
        cyclops.transform.localScale = eyes.transform.localScale;
        cyclops.layer = eyes.gameObject.layer;
        Undo.AddComponent<Horde_EyeCards>(cyclops).ConfigureCyclopsFrom(eyes);
        var group = Undo.AddComponent<Horde_VariationGroup>(holder);
        Undo.RecordObject(group, "Set Eye Options");
        group.options.Add(new Horde_VariationGroup.Option { name = "Two Eyes", root = eyes.gameObject, weight = 85f });
        group.options.Add(new Horde_VariationGroup.Option { name = "Cyclops", root = cyclops, weight = 15f });
        Horde_VariationGroupEditor.Preview(group, 0);
        Undo.CollapseUndoOperations(undo);
        Selection.activeGameObject = holder;
    }
}
