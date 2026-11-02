using UnityEditor;
using UnityEngine;

// Percentage variation authoring/preview. Changes real option GameObjects with Undo; no scene/runtime ticks.
// GC: editor UI strings/arrays and Undo records on interaction only. No event subscriptions.
[CustomEditor(typeof(Horde_VariationGroup))]
public class Horde_VariationGroupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var group = (Horde_VariationGroup)target;
        EditorGUILayout.HelpBox("Each option is a direct child with its own visual objects. Empty means None. " +
            "Changing a Chance redistributes the remaining percentage across the other options. " +
            "One option is chosen per spawn. Preview visibility does not affect chances. " +
            "Restart Play Mode after changing options or chances.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying || EditorUtility.IsPersistent(group)))
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "options");
            SerializedProperty options = serializedObject.FindProperty("options");
            for (int i = 0; i < options.arraySize; i++)
            {
                SerializedProperty option = options.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(option.FindPropertyRelative("name"));
                EditorGUILayout.PropertyField(option.FindPropertyRelative("root"));
                double sum = 0;
                for (int j = 0; j < options.arraySize; j++)
                    sum += Horde_VariationGroup.Weight(options.GetArrayElementAtIndex(j).FindPropertyRelative("weight").floatValue);
                float weight = Horde_VariationGroup.Weight(option.FindPropertyRelative("weight").floatValue);
                float percent = sum > 0 ? (float)(100 * weight / sum) : 0f;
                using (new EditorGUI.DisabledScope(options.arraySize == 1))
                {
                    EditorGUI.BeginChangeCheck();
                    float changed = EditorGUILayout.Slider(new GUIContent("Chance (%)",
                        "Exact spawn chance. The others share the remainder in their current proportions."),
                        options.arraySize == 1 && sum > 0 ? 100f : percent, 0f, 100f);
                    if (EditorGUI.EndChangeCheck()) Redistribute(options, i, changed);
                }
                if (options.arraySize == 1 && sum == 0 && GUILayout.Button("Set Chance to 100%"))
                    Redistribute(options, i, 100f);
                bool remove = GUILayout.Button("Remove Option");
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    // Leave the source object available for reuse, but don't let it become an always-on mesh.
                    GameObject root = option.FindPropertyRelative("root").objectReferenceValue as GameObject;
                    if (root != null && root.transform.parent == group.transform)
                    {
                        Undo.RecordObject(root, "Remove Variation Option");
                        root.SetActive(false);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
                    }
                    options.DeleteArrayElementAtIndex(i);
                    Normalise(options);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }
            serializedObject.ApplyModifiedProperties();
            if (!group.Validate(out string reason))
            {
                EditorGUILayout.HelpBox(reason, MessageType.Error);
                return;
            }
            double total = 0;
            foreach (var option in group.options)
                if (option != null) total += Horde_VariationGroup.Weight(option.weight);
            for (int i = 0; i < group.options.Count; i++)
            {
                var option = group.options[i];
                if (option == null) continue;
                double percent = total > 0 ? 100 * Horde_VariationGroup.Weight(option.weight) / total : 0;
                if (GUILayout.Button($"Preview {option.name} — {percent:0.#}%")) Preview(group, i);
            }
            if (GUILayout.Button("Randomise Preview") && total > 0)
            {
                double roll = Random.value * total;
                for (int i = 0; i < group.options.Count; i++)
                {
                    if (group.options[i] == null) continue;
                    float weight = Horde_VariationGroup.Weight(group.options[i].weight);
                    if (weight > 0 && roll < weight) { Preview(group, i); break; }
                    roll -= weight;
                }
            }
            if (GUILayout.Button("Add Option"))
            {
                Undo.RecordObject(group, "Add Variation Option");
                var root = new GameObject("New Option");
                Undo.RegisterCreatedObjectUndo(root, "Add Variation Option");
                Undo.SetTransformParent(root.transform, group.transform, "Parent Variation Option");
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                root.layer = group.gameObject.layer;
                // Start new options at zero so existing chances are preserved until the user assigns a chance.
                root.SetActive(false);
                group.options.Add(new Horde_VariationGroup.Option { name = root.name, root = root, weight = group.options.Count == 0 ? 100f : 0f });
                serializedObject.Update();
                Normalise(serializedObject.FindProperty("options"));
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(group);
                PrefabUtility.RecordPrefabInstancePropertyModifications(group);
            }
        }
    }

    private static void Redistribute(SerializedProperty options, int selected, float chance)
    {
        var weights = new float[options.arraySize];
        for (int i = 0; i < weights.Length; i++)
            weights[i] = options.GetArrayElementAtIndex(i).FindPropertyRelative("weight").floatValue;
        Horde_VariationGroup.SetPercentage(weights, selected, chance);
        for (int i = 0; i < weights.Length; i++)
            options.GetArrayElementAtIndex(i).FindPropertyRelative("weight").floatValue = weights[i];
    }

    private static void Normalise(SerializedProperty options)
    {
        double total = 0;
        for (int i = 0; i < options.arraySize; i++)
            total += Horde_VariationGroup.Weight(options.GetArrayElementAtIndex(i).FindPropertyRelative("weight").floatValue);
        for (int i = 0; i < options.arraySize; i++)
        {
            var weight = options.GetArrayElementAtIndex(i).FindPropertyRelative("weight");
            weight.floatValue = total > 0 ? (float)(100 * Horde_VariationGroup.Weight(weight.floatValue) / total) : 100f / options.arraySize;
        }
    }

    public static void Preview(Horde_VariationGroup group, int selected)
    {
        Undo.RecordObject(group, "Preview Variation");
        group.preview = selected;
        for (int i = 0; i < group.options.Count; i++)
        {
            GameObject root = group.options[i]?.root;
            if (root == null) continue;
            Undo.RecordObject(root, "Preview Variation");
            root.SetActive(i == selected);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(group);
        EditorUtility.SetDirty(group);
        SceneView.RepaintAll();
    }
}
