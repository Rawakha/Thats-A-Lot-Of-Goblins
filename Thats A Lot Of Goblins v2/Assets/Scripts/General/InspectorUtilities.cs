#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;

public static class InspectorUtilities
{
    public static void InspectorButton(string text, Action action, UnityEngine.Object obj = null)
    {
        if (GUILayout.Button(text))
        {
            if (obj != null) Undo.RecordObject(obj, text);
            action?.Invoke();
            if (obj != null) EditorUtility.SetDirty(obj);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
#endif
        }
    }
}
#endif