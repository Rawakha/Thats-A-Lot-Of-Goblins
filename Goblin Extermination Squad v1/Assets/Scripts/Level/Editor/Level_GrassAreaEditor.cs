using UnityEditor;
using UnityEngine;

// Grass outlines use the shared outline handles (Level_OutlineHandles). The parent surface bakes on release.
// Editor Undo/handles only, no runtime cost.
[CustomEditor(typeof(Level_GrassArea))]
public class Level_GrassAreaEditor : InspectorButtonEditor
{
    public override void OnInspectorGUI()
    {
        Level_ToolInspector.Help($"{Level_ToolInspector.NodeHelp} Move, rotate or scale the whole GameObject to place the patch. " +
            "Keep it under a Grass Surface. An outline can't be dragged into crossing itself.");
        if (((Level_GrassArea)target).GetComponentInParent<Level_GroundSurface>() == null)
            EditorGUILayout.HelpBox("This area needs a Level_GroundSurface parent.", MessageType.Warning);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            base.OnInspectorGUI();
    }

    private void OnSceneGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Level_GrassArea area = (Level_GrassArea)target;
        if (Level_OutlineHandles.Draw(area, area.transform, area.Nodes, area.Snap))
            Level_GroundSurface.RequestRebuilds();
        Level_ToolFeedback.Built(area.transform);
    }
}
