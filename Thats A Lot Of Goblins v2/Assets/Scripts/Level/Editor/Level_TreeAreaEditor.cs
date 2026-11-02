using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Scene view editing for Level_TreeArea, with the area selected:
//   White squares: drag to move a corner along the ground (snaps to the area's Snap grid).
//   Green dots: click to add a corner halfway along an edge.
//   Hold Ctrl (Cmd on Mac): corners turn into red dots; click one to delete it (an area keeps at least 3).
// The outline moves live; the trees are rebuilt when you let go (placing hundreds of prefabs every drag frame is slow).
// GC: editor-only handle/Undo work, no runtime allocations or new subscriptions.
[CustomEditor(typeof(Level_TreeArea))]
public class Level_TreeAreaEditor : InspectorButtonEditor
{
    private static readonly Color OutlineColour = Level_Handles.Outline;
    private static readonly Color AddColour = Level_Handles.Add;
    private static readonly Color DeleteColour = Level_Handles.Delete;

    private bool pending;

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Scene view: drag white squares to move corners, click green dots to add one, hold Ctrl and click a " +
            "corner to delete it. Add tree prefabs below. Trees are generated: Rebuild replaces them.", MessageType.Info);

        base.OnInspectorGUI();
    }

    private void OnSceneGUI()
    {
        if (Application.isPlaying)
            return;

        Level_TreeArea area = (Level_TreeArea)target;
        List<Vector3> corners = area.Corners;
        Transform transform = area.transform;
        Event current = Event.current;
        bool deleting = EditorGUI.actionKey;
        bool changed = false;
        int count = corners.Count;

        // Ctrl doesn't repaint on its own, so the delete dots would only show once the mouse moved over a handle
        if (current.type == EventType.MouseMove || current.type == EventType.KeyDown || current.type == EventType.KeyUp)
            SceneView.RepaintAll();

        Handles.color = OutlineColour;

        for (int i = 0; i < count; i++)
            Handles.DrawAAPolyLine(4f, transform.TransformPoint(Flat(corners[i])), transform.TransformPoint(Flat(corners[(i + 1) % count])));

        for (int i = 0; i < count && !changed; i++)
        {
            Vector3 world = transform.TransformPoint(Flat(corners[i]));
            float size = HandleUtility.GetHandleSize(world);

            if (deleting)
            {
                Handles.color = count > 3 ? DeleteColour : Color.grey;

                if (Handles.Button(world, Quaternion.identity, size * 0.08f, size * 0.12f, Handles.DotHandleCap) && count > 3)
                {
                    Undo.RecordObject(area, "Delete Tree Area Corner");
                    corners.RemoveAt(i);
                    changed = true;
                }

                continue;
            }

            Handles.color = Color.white;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Level_Handles.Move(world, transform);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(area, "Move Tree Area Corner");
                corners[i] = Snapped(Flat(transform.InverseTransformPoint(moved)), area.Snap);
                changed = true;
            }
        }

        // Green dots halfway along each edge
        if (!deleting && !changed)
        {
            Handles.color = AddColour;

            for (int i = 0; i < count; i++)
            {
                Vector3 middle = (Flat(corners[i]) + Flat(corners[(i + 1) % count])) * 0.5f;
                Vector3 world = transform.TransformPoint(middle);
                float size = HandleUtility.GetHandleSize(world) * 0.06f;

                if (Handles.Button(world, Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap))
                {
                    Undo.RecordObject(area, "Add Tree Area Corner");
                    corners.Insert(i + 1, middle);
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(area);
            PrefabUtility.RecordPrefabInstancePropertyModifications(area);

            // A click (add, delete) rebuilds now; a drag waits for the release
            if (GUIUtility.hotControl == 0)
                area.RequestRebuild();
            else
                pending = true;
        }

        if (pending && (current.rawType == EventType.MouseUp || GUIUtility.hotControl == 0))
        {
            pending = false;
            area.RequestRebuild();
        }
    }

    private void OnDisable()
    {
        if (pending && target != null)
            ((Level_TreeArea)target).RequestRebuild();
        pending = false;
    }

    private static Vector3 Flat(Vector3 position) => new Vector3(position.x, 0f, position.z);

    private static Vector3 Snapped(Vector3 position, float grid)
    {
        return Level_Handles.SnapPlanar(position, grid);
    }
}
