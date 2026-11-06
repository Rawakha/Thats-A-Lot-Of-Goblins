using UnityEditor;
using UnityEngine;

// The inspector and Scene view status for every bake group and for any Level_Tool without its own editor. A tool's
// editor derives from Level_ToolEditor and only adds help text and handles.
// Also the Bake On Save menu toggle. GC: editor only (inspector UI and one label per repaint). No subscriptions.
[CustomEditor(typeof(Level_BakeGroup), true)]
public class Level_BakeGroupEditor : Editor
{
    private static readonly string[] Hidden = { "m_Script", "makeCollider", "saveMeshAsAsset" };

    protected virtual string HelpText => "Put static mesh pieces, or other level tools, in Editable Sources. Bake combines them into one " +
        "renderer. Save the scene after baking.";

    // A plain group has nothing to drag, so it just shows where its bake sits. Tools replace this with their handles.
    protected virtual void DrawHandles()
    {
        var group = (Level_BakeGroup)target;
        MeshFilter baked = group.Output != null ? group.Output.GetComponentInChildren<MeshFilter>(true) : null;

        if (baked == null || baked.sharedMesh == null)
            return;

        Bounds bounds = baked.sharedMesh.bounds;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Level_ToolFeedback.Footprint(group.transform.localToWorldMatrix, new[]
        {
            new Vector3(min.x, 0f, min.z), new Vector3(max.x, 0f, min.z), new Vector3(max.x, 0f, max.z), new Vector3(min.x, 0f, max.z)
        }, true);
    }

    public override void OnInspectorGUI()
    {
        var group = (Level_BakeGroup)target;
        Level_ToolInspector.Help(HelpText);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, Hidden);
            serializedObject.ApplyModifiedProperties();

            if (group is Level_Tool tool && !tool.HasLook && GUILayout.Button("Assign Default Style and Palette"))
                tool.Configure(Level_ToolCreation.DefaultStyle(), Level_ToolCreation.DefaultPalette());

            Level_ToolInspector.BakeControls(group, serializedObject);
        }
    }

    protected virtual void OnSceneGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var group = (Level_BakeGroup)target;
        DrawHandles();
        Level_ToolFeedback.Status(group);
    }

    private const string BakeOnSaveMenu = "Tools/Goblins/Bake Stale Level Groups On Save";

    [MenuItem(BakeOnSaveMenu)]
    private static void ToggleBakeOnSave() => Level_BakeGroup.BakeOnSave = !Level_BakeGroup.BakeOnSave;

    [MenuItem(BakeOnSaveMenu, true)]
    private static bool ShowBakeOnSave()
    {
        Menu.SetChecked(BakeOnSaveMenu, Level_BakeGroup.BakeOnSave);
        return true;
    }

    [MenuItem("Tools/Goblins/Bake All Level Groups")]
    private static void BakeAllFromTools() => Level_BakeGroup.BakeAll();

    [MenuItem("GameObject/Level/Common/Bake All Level Groups", false, 48)]
    private static void BakeAllFromLevelMenu() => Level_BakeGroup.BakeAll();

    [MenuItem("GameObject/Level/Common/Bake Group", false, 47)]
    private static void Create(MenuCommand command)
    {
        var obj = Level_ToolCreation.Create("Level Bake Group", command);
        Undo.AddComponent<Level_BakeGroup>(obj).Generate();
    }
}

// The base editor for a Level_Tool: override HelpText and DrawHandles, and call Changed when handles edit something.
public abstract class Level_ToolEditor : Level_BakeGroupEditor
{
    protected Level_Tool Tool => (Level_Tool)target;

    // Sources regenerate once the drag is released, so the handles stay quick
    protected void Changed() => Tool.RequestRefresh();
}
