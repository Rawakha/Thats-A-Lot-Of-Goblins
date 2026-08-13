#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class HierarchySeparatorCreator
{
    [MenuItem("GameObject/Create Hierarchy Separator", false, 0)]
    private static void CreateSeparator(MenuCommand menuCommand)
    {
        GameObject separator = new("---");

        GameObjectUtility.SetParentAndAlign(
            separator,
            menuCommand.context as GameObject
        );

        Undo.RegisterCreatedObjectUndo(
            separator,
            "Create Hierarchy Separator"
        );

        Selection.activeGameObject = separator;
    }
}
#endif