#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HierarchySeparator
{
    static HierarchySeparator()
    {
        EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnGUI;
    }

    private static void OnGUI(EntityId entityId, Rect rect)
    {
        GameObject obj = EditorUtility.EntityIdToObject(entityId) as GameObject;

        if (obj == null)
            return;

        if (obj.name.StartsWith("---"))
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

            EditorGUI.LabelField(
                rect,
                obj.name.Replace("-", ""),
                new GUIStyle()
                {
                    normal = { textColor = Color.white },
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                }
            );
        }
    }
}
#endif