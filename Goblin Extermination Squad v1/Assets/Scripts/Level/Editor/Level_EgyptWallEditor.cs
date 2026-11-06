using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Handles and menu item for the Egyptian wall: the shared path handles (white move, green add, Ctrl/Cmd delete, yellow
// height cones, orange thickness cubes), purple spots that place a pillar on a corner, an end or between nodes, and the
// damage handles. A node's "Gap After" leaves the stretch to the next node open.
//
// Damage: the Add Hole and Add Broken Top buttons drop one on the wall nearest where the Scene view looks. Each shows a
// cyan outline of what it removes. White square slides it along the wall (it can't leave the wall), orange cubes set its
// width, yellow cones set its heights (a hole has a top cone and a bottom cone; a broken top has one cone for how tall the
// wall still stands), Ctrl/Cmd shows a red dot to delete it.
//
// GC: editor only (handles, inspector UI, a list per repaint while the spots show, one delayed call per placement).
// No subscriptions.
[CustomEditor(typeof(Level_EgyptWall))]
public class Level_EgyptWallEditor : Level_ToolEditor
{
    private const string SpotsKey = "Level_EgyptWall.ShowSpots";

    private string lastPillars;

    private static bool ShowSpots
    {
        get => SessionState.GetBool(SpotsKey, true);
        set => SessionState.SetBool(SpotsKey, value);
    }

    protected override string HelpText => $"{Level_ToolInspector.NodeHelp} Yellow cones set wall height, orange cubes set thickness. " +
        "Purple spots place a pillar. Tick Gap After on a node to leave the stretch to the next node open. " +
        "Lower and Raise Variation make the top ruined. Add Hole / Add Broken Top make damage, with handles in the Scene view.";

    public override void OnInspectorGUI()
    {
        var wall = (Level_EgyptWall)target;
        ShowSpots = EditorGUILayout.ToggleLeft("Show pillar spots in the Scene view", ShowSpots);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Hole"))
            AddDamage(wall, Level_EgyptDamageKind.Hole);

        if (GUILayout.Button("Add Broken Top"))
            AddDamage(wall, Level_EgyptDamageKind.BrokenTop);

        EditorGUILayout.EndHorizontal();
        base.OnInspectorGUI();
    }

    // On the wall nearest where the Scene view is looking
    private static void AddDamage(Level_EgyptWall wall, Level_EgyptDamageKind kind)
    {
        Vector3 local = wall.transform.InverseTransformPoint(Level_ToolCreation.ScenePivot(wall.transform.position.y));

        if (!wall.SnapToWall(local, out Vector3 point, out _))
            return;

        Undo.RecordObject(wall, "Add Wall Damage");
        wall.Damages.Add(new Level_EgyptWallDamage
        {
            kind = kind,
            position = point,
            bottom = 1f,
            top = kind == Level_EgyptDamageKind.Hole ? Mathf.Max(2f, wall.Height * 0.7f) : Mathf.Max(0.5f, wall.Height * 0.35f)
        });
        EditorUtility.SetDirty(wall);
        wall.RequestRefresh();
    }

    protected override void DrawHandles()
    {
        var wall = (Level_EgyptWall)target;
        Level_PathHandles.Draw(wall, wall.Nodes, false, wall.Height, wall.Thickness, wall.Snap,
            (a, b, position) => new Level_EgyptWallNode
            {
                position = position,
                height = (a.height + b.height) * 0.5f,
                thickness = (a.thickness + b.thickness) * 0.5f
            },
            node => node.gapAfter);

        DrawDamage(wall);

        // Pillars cut the wall, so it regenerates when they change (a removed pillar isn't otherwise noticed). The first
        // repaint also catches a wall left out of date while it wasn't selected.
        if (Event.current.type == EventType.Repaint)
        {
            string pillars = wall.PillarKey();

            if (pillars != lastPillars)
            {
                lastPillars = pillars;
                wall.RequestRefresh();
            }
        }

        if (!ShowSpots || wall.Sources == null)
            return;

        // Where pillars already stand, so those spots hide
        List<Vector3> taken = new List<Vector3>();

        foreach (Level_EgyptPillar pillar in wall.Sources.GetComponentsInChildren<Level_EgyptPillar>(true))
            taken.Add(Level_Handles.Flat(wall.transform.InverseTransformPoint(pillar.transform.position)));

        if (Level_EgyptSpots.Pick(wall, taken, out Level_EgyptSpots.Spot spot))
        {
            // Made after this GUI pass: changing the selection mid-pass breaks the handles
            EditorApplication.delayCall += () =>
            {
                if (wall != null)
                    Level_EgyptPillarEditor.PlaceOnWall(wall, spot);
            };
        }
    }

    private void DrawDamage(Level_EgyptWall wall)
    {
        List<Level_EgyptWallDamage> damages = wall.Damages;
        bool deleting = Event.current.control || Event.current.command;
        Level_Handles.RepaintOnModifierChange();
        serializedObject.Update();
        bool changed = false;

        for (int i = 0; i < damages.Count; i++)
        {
            Level_EgyptWallDamage damage = damages[i];

            if (damage == null || !wall.SnapToWall(damage.position, out Vector3 point, out Vector3 direction))
                continue;

            bool hole = damage.kind == Level_EgyptDamageKind.Hole;
            SerializedProperty item = serializedObject.FindProperty("damages").GetArrayElementAtIndex(i);

            // Axes: forward runs along the wall, up is up
            Matrix4x4 matrix = wall.transform.localToWorldMatrix * Matrix4x4.TRS(point, Quaternion.LookRotation(direction), Vector3.one);
            float middle = hole ? (damage.bottom + damage.top) * 0.5f : damage.top;
            float half = damage.width * 0.5f;

            // What it removes, outlined on the wall's line
            Handles.color = Level_Handles.Outline;
            float low = hole ? damage.bottom : damage.top;
            float high = hole ? damage.top : damage.top + 1.5f;
            Handles.DrawAAPolyLine(Level_Handles.LineWidth, matrix.MultiplyPoint3x4(new Vector3(0f, low, -half)),
                matrix.MultiplyPoint3x4(new Vector3(0f, low, half)), matrix.MultiplyPoint3x4(new Vector3(0f, high, half)),
                matrix.MultiplyPoint3x4(new Vector3(0f, high, -half)), matrix.MultiplyPoint3x4(new Vector3(0f, low, -half)));

            // Slide along the wall: it follows the cursor but is always put back on the wall's line
            Vector3 handle = matrix.MultiplyPoint3x4(new Vector3(0f, middle, 0f));

            if (deleting)
            {
                if (Level_Handles.DeleteDot(handle, true))
                {
                    Undo.RecordObject(wall, "Delete Wall Damage");
                    damages.RemoveAt(i);
                    EditorUtility.SetDirty(wall);
                    wall.RequestRefresh();
                    return;
                }

                continue;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Level_Handles.Move(handle, wall.transform);

            if (EditorGUI.EndChangeCheck())
            {
                Vector3 local = Level_Handles.SnapPlanar(wall.transform.InverseTransformPoint(moved), wall.Snap);

                if (wall.SnapToWall(local, out Vector3 onWall, out _))
                {
                    item.FindPropertyRelative("position").vector3Value = onWall;
                    changed = true;
                }
            }

            for (int sign = -1; sign <= 1; sign += 2)
            {
                if (Level_BoxHandles.Reach(matrix, new Vector3(0f, middle, 0f), Vector3.forward * sign, half, Level_Handles.Width,
                    false, out float reach))
                {
                    item.FindPropertyRelative("width").floatValue = Mathf.Max(0.3f, Level_Handles.Snap(reach * 2f, wall.Snap));
                    changed = true;
                }
            }

            // Top of a hole, or how tall a broken wall still stands
            if (Level_BoxHandles.Reach(matrix, Vector3.zero, Vector3.up, damage.top, Level_Handles.Height, true, out float top))
            {
                float minimum = hole ? damage.bottom + 0.3f : 0f;
                item.FindPropertyRelative("top").floatValue = Mathf.Max(minimum, Level_Handles.Snap(top, wall.Snap));
                changed = true;
            }

            // Bottom of a hole: a cone hanging down from the top
            if (hole && Level_BoxHandles.Reach(matrix, Vector3.up * damage.top, Vector3.down, damage.top - damage.bottom,
                Level_Handles.Height, true, out float drop))
            {
                item.FindPropertyRelative("bottom").floatValue = Mathf.Clamp(Level_Handles.Snap(damage.top - drop, wall.Snap), 0f, damage.top - 0.3f);
                changed = true;
            }
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            wall.RequestRefresh();
        }
    }

    [MenuItem("GameObject/Level/Egyptian/Wall", false, 47)]
    private static void Create(MenuCommand command) => Level_EgyptLook.Create<Level_EgyptWall>("Egyptian Wall", command);
}
