using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Scene view editing for Level_TreeArea, with the area selected: the shared outline handles (Level_OutlineHandles). The
// outline moves live; the trees are rebuilt when you let go (placing hundreds of prefabs every drag frame is slow).
// GC: editor-only handle/Undo work, no runtime allocations or new subscriptions.
[CustomEditor(typeof(Level_TreeArea))]
public class Level_TreeAreaEditor : InspectorButtonEditor
{
    private bool pending;

    public override void OnInspectorGUI()
    {
        Level_ToolInspector.Help($"Scene view: {Level_ToolInspector.NodeHelp} Add tree prefabs below. Trees are generated: Rebuild replaces them.");
        base.OnInspectorGUI();
    }

    private void OnSceneGUI()
    {
        if (Application.isPlaying)
            return;

        Level_TreeArea area = (Level_TreeArea)target;

        if (Level_OutlineHandles.Draw(area, area.transform, area.Corners, area.Snap))
        {
            // A click (add, delete) rebuilds now; a drag waits for the release
            if (GUIUtility.hotControl == 0)
                area.QueueRebuild();
            else
                pending = true;
        }

        Level_ToolFeedback.Built(area.transform);

        if (pending && (Event.current.rawType == EventType.MouseUp || GUIUtility.hotControl == 0))
        {
            pending = false;
            area.QueueRebuild();
        }
    }

    private void OnDisable()
    {
        if (pending && target != null)
            ((Level_TreeArea)target).QueueRebuild();

        pending = false;
    }
}
