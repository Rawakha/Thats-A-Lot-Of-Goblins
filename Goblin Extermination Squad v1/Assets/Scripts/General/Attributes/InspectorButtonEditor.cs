#if UNITY_EDITOR

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonoBehaviour), true)]
[CanEditMultipleObjects]
public class InspectorButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        DrawInspectorButtons();
    }

    protected void DrawInspectorButtons()
    {
        MethodInfo[] methods = target.GetType().GetMethods(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic
        );

        bool addedSpacing = false;

        foreach (MethodInfo method in methods)
        {
            InspectorButtonAttribute attribute =
                method.GetCustomAttribute<InspectorButtonAttribute>();

            if (attribute == null)
                continue;

            if (method.GetParameters().Length > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{method.Name} cannot be used as an Inspector button because it has parameters.",
                    MessageType.Warning
                );

                continue;
            }

            if (method.ReturnType != typeof(void))
            {
                EditorGUILayout.HelpBox(
                    $"{method.Name} cannot be used as an Inspector button because it does not return void.",
                    MessageType.Warning
                );

                continue;
            }

            // Hidden while its ShowIf bool is false
            if (!string.IsNullOrEmpty(attribute.ShowIf))
            {
                SerializedProperty condition = serializedObject.FindProperty(attribute.ShowIf);

                if (condition != null && condition.propertyType == SerializedPropertyType.Boolean
                    && !condition.hasMultipleDifferentValues && !condition.boolValue)
                    continue;
            }

            if (!addedSpacing)
            {
                EditorGUILayout.Space();
                addedSpacing = true;
            }

            string buttonLabel = string.IsNullOrWhiteSpace(attribute.Label)
                ? ObjectNames.NicifyVariableName(method.Name)
                : attribute.Label;

            using (new EditorGUI.DisabledScope(attribute.PlayModeOnly && !Application.isPlaying))
            {
                if (!GUILayout.Button(buttonLabel))
                    continue;

                foreach (UnityEngine.Object selectedTarget in targets)
                {
                    if (!Application.isPlaying)
                        Undo.RecordObject(selectedTarget, buttonLabel);

                    try
                    {
                        method.Invoke(selectedTarget, null);
                    }
                    catch (TargetInvocationException exception)
                    {
                        Debug.LogException(
                            exception.InnerException ?? exception,
                            selectedTarget
                        );
                    }

                    if (!Application.isPlaying)
                        EditorUtility.SetDirty(selectedTarget);
                }
            }
        }
    }
}

// The same buttons on ScriptableObject assets (the editor above only covers MonoBehaviours)
[CustomEditor(typeof(ScriptableObject), true)]
[CanEditMultipleObjects]
public class InspectorButtonAssetEditor : InspectorButtonEditor
{
}

#endif