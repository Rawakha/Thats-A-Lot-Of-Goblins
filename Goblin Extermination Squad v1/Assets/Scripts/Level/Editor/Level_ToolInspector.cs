using UnityEditor;
using UnityEngine;

// The inspector block shared by bake tools: status, any error, Edit Sources, Bake, Regenerate and the collider options.
// Failures show here as well as in the Console, where they were easy to miss. Also holds the one way tools explain
// their handles, so the wording stays the same everywhere.
//
// Setup: Level_BakeGroupEditor draws it for every bake group. GC: editor only (inspector UI).
public static class Level_ToolInspector
{
    public const string NodeHelp = "White squares move, green dots add, Ctrl/Cmd-click deletes.";

    public static void Help(string text) => EditorGUILayout.HelpBox(text, MessageType.Info);

    public static void BakeControls(Level_BakeGroup group, SerializedObject data)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);

        Color previous = GUI.contentColor;
        GUI.contentColor = Level_ToolFeedback.StatusColour(group);
        EditorGUILayout.LabelField("Status", Level_ToolFeedback.StatusText(group));
        GUI.contentColor = previous;

        if (!string.IsNullOrEmpty(group.LastError))
            EditorGUILayout.HelpBox(group.LastError, MessageType.Error);

        if (!string.IsNullOrEmpty(group.SourceProblem))
            EditorGUILayout.HelpBox(group.SourceProblem, MessageType.Warning);

        if (group.NavigationReminder && group.Status != Level_BakeStatus.NeedsBake)
            EditorGUILayout.HelpBox("The collider changed. Bake the area's navigation again if goblins walk here.", MessageType.Info);

        data.Update();
        EditorGUILayout.PropertyField(data.FindProperty("makeCollider"));
        EditorGUILayout.PropertyField(data.FindProperty("saveMeshAsAsset"));
        data.ApplyModifiedProperties();

        if (GUILayout.Button(group.Status == Level_BakeStatus.NeedsBake ? "Bake Into One Mesh (needed)" : "Bake Into One Mesh", GUILayout.Height(26f)))
            group.Bake();

        if (GUILayout.Button("Bake All Level Groups In Scene"))
            Level_BakeGroup.BakeAll();

        EditorGUILayout.BeginHorizontal();

        if (group.Editing && group.HasBake)
        {
            if (GUILayout.Button("Preview Last Bake"))
                group.SetEditing(false);
        }
        else if (!group.Editing)
        {
            if (GUILayout.Button("Edit Sources"))
                EditSources(group);
        }

        if (group is Level_Tool && GUILayout.Button("Regenerate"))
            group.Generate();

        EditorGUILayout.EndHorizontal();
    }

    // A plain group is filled by hand, so its folder gets selected; a tool stays selected to keep its handles
    private static void EditSources(Level_BakeGroup group)
    {
        if (group.Sources == null && !group.Generate())
            return;

        group.SetEditing(true);

        if (!(group is Level_Tool))
            Selection.activeGameObject = group.Sources;
    }
}
