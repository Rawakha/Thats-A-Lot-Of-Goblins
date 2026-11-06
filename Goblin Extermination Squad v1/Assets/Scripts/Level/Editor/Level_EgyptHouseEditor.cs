using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Handles and menu item for the Egyptian house. White square moves the house (and shifts an upper floor on the roof below).
// Per storey: orange cubes set its width, pink cubes its depth, a yellow cone its height. Each door and window shows a cyan
// outline: a white square slides it along the wall (it always sits on the nearest wall of its storey), orange cubes set its
// width, yellow cones its top and bottom edges, and Ctrl/Cmd shows a red dot that deletes it. The stair has a white square
// that slides it along the ground-floor walls. Add Door / Window / Floor / Stairs are inspector buttons.
// GC: editor only (handles and inspector UI). No subscriptions.
[CustomEditor(typeof(Level_EgyptHouse))]
public class Level_EgyptHouseEditor : Level_ToolEditor
{
    protected override string HelpText => "White square moves it. Per floor: orange cubes width, pink cubes depth, yellow cone height, " +
        "and a white square shifts an upper floor. Add Door / Add Window place one on the wall nearest where the Scene view looks; " +
        "slide it with its white square, Ctrl/Cmd then click the red dot to delete. Rotate the object to turn the house.";

    public override void OnInspectorGUI()
    {
        var house = (Level_EgyptHouse)target;
        Vector3 pivot = house.transform.InverseTransformPoint(Level_ToolCreation.ScenePivot(house.transform.position.y));

        // A whole new house: floors, door, windows and maybe stairs. Press again for another.
        if (GUILayout.Button("Randomise House", GUILayout.Height(26f)))
            house.Randomise();


        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Door"))
            house.AddOpening(Level_EgyptOpeningKind.Door, pivot);

        if (GUILayout.Button("Add Window"))
            house.AddOpening(Level_EgyptOpeningKind.Window, pivot);

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Floor"))
            house.AddFloor();

        if (!house.HasStairs && GUILayout.Button("Add Stairs"))
            house.AddStairs(pivot);

        EditorGUILayout.EndHorizontal();
        base.OnInspectorGUI();
    }

    protected override void DrawHandles()
    {
        var house = (Level_EgyptHouse)target;
        Transform space = house.transform;
        Level_BoxHandles.MoveObject(space, house.Snap);
        Matrix4x4 matrix = space.localToWorldMatrix;
        serializedObject.Update();
        bool changed = false;
        float bottom = 0f;
        List<Level_EgyptHouseFloor> floors = house.Floors;
        bool deleting = Event.current.control || Event.current.command;
        Level_Handles.RepaintOnModifierChange();

        for (int f = 0; f < floors.Count; f++)
        {
            Level_EgyptHouseFloor floor = floors[f];
            SerializedProperty item = serializedObject.FindProperty("floors").GetArrayElementAtIndex(f);
            Vector3 centre = f == 0 ? Vector3.zero : new Vector3(floor.offset.x, 0f, floor.offset.y);
            Vector3 mid = centre + Vector3.up * (bottom + floor.height * 0.5f);

            // Ctrl/Cmd: a red dot on the roof of each floor deletes it (not the last one)
            if (deleting)
            {
                if (Level_Handles.DeleteDot(matrix.MultiplyPoint3x4(centre + Vector3.up * (bottom + floor.height + 0.4f)), floors.Count > 1))
                {
                    house.RemoveFloor(f);
                    return;
                }

                bottom += floor.height + 0.35f;
                continue;
            }

            for (int sign = -1; sign <= 1; sign += 2)
            {
                if (Level_BoxHandles.Reach(matrix, mid, Vector3.right * sign, floor.width * 0.5f, Level_Handles.Width, false, out float half))
                {
                    item.FindPropertyRelative("width").floatValue = Mathf.Max(2f, Level_Handles.Snap(half * 2f, house.Snap));
                    changed = true;
                }

                if (Level_BoxHandles.Reach(matrix, mid, Vector3.forward * sign, floor.depth * 0.5f, Level_Handles.Length, false, out float reach))
                {
                    item.FindPropertyRelative("depth").floatValue = Mathf.Max(2f, Level_Handles.Snap(reach * 2f, house.Snap));
                    changed = true;
                }
            }

            if (Level_BoxHandles.Reach(matrix, centre + Vector3.up * bottom, Vector3.up, floor.height, Level_Handles.Height, true, out float up))
            {
                item.FindPropertyRelative("height").floatValue = Mathf.Max(2f, Level_Handles.Snap(up, house.Snap));
                changed = true;
            }

            // An upper floor slides about on the roof below
            if (f > 0)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Level_Handles.Move(matrix.MultiplyPoint3x4(centre + Vector3.up * (bottom + floor.height)), space);

                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 local = space.InverseTransformPoint(moved);
                    item.FindPropertyRelative("offset").vector2Value = new Vector2(Level_Handles.Snap(local.x, house.Snap), Level_Handles.Snap(local.z, house.Snap));
                    changed = true;
                }
            }

            bottom += floor.height + 0.35f; // roughly where the next floor starts; handles only
        }

        DrawOpenings(house, matrix);
        DrawStairs(house, matrix);

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            Changed();
        }
    }

    private void DrawOpenings(Level_EgyptHouse house, Matrix4x4 matrix)
    {
        List<Level_EgyptHouseOpening> openings = house.Openings;
        bool deleting = Event.current.control || Event.current.command;
        Level_Handles.RepaintOnModifierChange();
        bool changed = false;

        for (int i = 0; i < openings.Count; i++)
        {
            Level_EgyptHouseOpening opening = openings[i];

            if (opening == null || !house.OpeningFrame(i, out Vector3 point, out Quaternion look))
                continue;

            // Axes: right runs along the wall, up is up, forward points out of the wall
            Matrix4x4 frame = matrix * Matrix4x4.TRS(point, look, Vector3.one);
            float half = opening.width * 0.5f;
            float middle = (opening.bottom + opening.top) * 0.5f;
            SerializedProperty item = serializedObject.FindProperty("openings").GetArrayElementAtIndex(i);

            Handles.color = Level_Handles.Outline;
            Handles.DrawAAPolyLine(Level_Handles.LineWidth, frame.MultiplyPoint3x4(new Vector3(-half, opening.bottom, 0f)),
                frame.MultiplyPoint3x4(new Vector3(-half, opening.top, 0f)), frame.MultiplyPoint3x4(new Vector3(half, opening.top, 0f)),
                frame.MultiplyPoint3x4(new Vector3(half, opening.bottom, 0f)), frame.MultiplyPoint3x4(new Vector3(-half, opening.bottom, 0f)));

            Vector3 handle = frame.MultiplyPoint3x4(new Vector3(0f, middle, 0f));

            if (deleting)
            {
                if (Level_Handles.DeleteDot(handle, true))
                {
                    Undo.RecordObject(house, "Delete Opening");
                    openings.RemoveAt(i);
                    EditorUtility.SetDirty(house);
                    house.RequestRefresh();
                    return;
                }

                continue;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Level_Handles.Move(handle, house.transform);

            if (EditorGUI.EndChangeCheck())
            {
                house.MoveOpening(i, Level_Handles.SnapPlanar(house.transform.InverseTransformPoint(moved), house.Snap));
                return; // the list may have rebuilt; redraw next frame
            }

            for (int sign = -1; sign <= 1; sign += 2)
            {
                if (Level_BoxHandles.Reach(frame, new Vector3(0f, middle, 0f), Vector3.right * sign, half, Level_Handles.Width, false, out float reach))
                {
                    item.FindPropertyRelative("width").floatValue = Mathf.Max(0.4f, Level_Handles.Snap(reach * 2f, house.Snap));
                    changed = true;
                }
            }

            if (Level_BoxHandles.Reach(frame, Vector3.zero, Vector3.up, opening.top, Level_Handles.Height, true, out float top))
            {
                item.FindPropertyRelative("top").floatValue = Mathf.Max(opening.bottom + 0.4f, Level_Handles.Snap(top, house.Snap));
                changed = true;
            }

            // Windows have a bottom edge too: a cone hanging down from the top
            if (opening.kind == Level_EgyptOpeningKind.Window && Level_BoxHandles.Reach(frame, Vector3.up * opening.top, Vector3.down,
                opening.top - opening.bottom, Level_Handles.Height, true, out float drop))
            {
                item.FindPropertyRelative("bottom").floatValue = Mathf.Clamp(Level_Handles.Snap(opening.top - drop, house.Snap), 0f, opening.top - 0.4f);
                changed = true;
            }
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            house.RequestRefresh();
        }
    }

    private void DrawStairs(Level_EgyptHouse house, Matrix4x4 matrix)
    {
        if (!house.StairsFrame(out Vector3 point, out Quaternion look))
            return;

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Level_Handles.Move(matrix.MultiplyPoint3x4(point + look * Vector3.forward * 0.6f + Vector3.up * 0.05f), house.transform);

        if (EditorGUI.EndChangeCheck())
            house.MoveStairs(Level_Handles.SnapPlanar(house.transform.InverseTransformPoint(moved), house.Snap));
    }

    [MenuItem("GameObject/Level/Egyptian/House", false, 52)]
    private static void Create(MenuCommand command)
    {
        var house = Level_EgyptLook.Create<Level_EgyptHouse>("Egyptian House", command);
        house.AddOpening(Level_EgyptOpeningKind.Door, new Vector3(0f, 0f, 3f));
        house.AddOpening(Level_EgyptOpeningKind.Window, new Vector3(-2f, 0f, 3f));
    }
}
