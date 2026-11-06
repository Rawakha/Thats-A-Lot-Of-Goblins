using UnityEditor;
using UnityEngine;

// Handles and menu item for the Egyptian pyramid: white square moves it, orange cubes set the base size, the yellow cone
// sets the height, the blue ring turns it. If it has an entrance, that gets its own handles on the face: a white square
// slides the door round the base (it stays on the edge, and jumps to whichever face it is nearest), orange cubes set its
// width, a yellow cone its height, with a cyan outline and an arrow showing which way goblins come out.
// GC: editor only (handles and inspector UI). No subscriptions.
[CustomEditor(typeof(Level_EgyptPyramid))]
public class Level_EgyptPyramidEditor : Level_ToolEditor
{
    protected override string HelpText => "White square moves it, orange cubes set the base size, the yellow cone sets the height, the " +
        "blue ring turns it. Add Entrance cuts a door; its own handles slide it round the base and set its size. " +
        "Courses, the top width, missing top courses and weathering are below.";

    public override void OnInspectorGUI()
    {
        var pyramid = (Level_EgyptPyramid)target;

        // Lands on the base's edge nearest where the Scene view is looking
        if (!pyramid.HasEntrance && GUILayout.Button("Add Entrance"))
            pyramid.AddEntrance(pyramid.transform.InverseTransformPoint(Level_ToolCreation.ScenePivot(pyramid.transform.position.y)));

        base.OnInspectorGUI();
    }

    protected override void DrawHandles()
    {
        var pyramid = (Level_EgyptPyramid)target;
        Transform space = pyramid.transform;
        Level_BoxHandles.MoveObject(space, pyramid.Snap);

        Matrix4x4 matrix = space.localToWorldMatrix * Matrix4x4.Rotate(pyramid.Rotation);
        serializedObject.Update();
        bool changed = false;

        if (Level_BoxHandles.Reach(matrix, Vector3.zero, Vector3.up, pyramid.Height, Level_Handles.Height, true, out float height))
        {
            serializedObject.FindProperty("height").floatValue = Mathf.Max(1f, Level_Handles.Snap(height, pyramid.Snap));
            changed = true;
        }

        for (int sign = -1; sign <= 1; sign += 2)
        {
            if (Level_BoxHandles.Reach(matrix, Vector3.zero, Vector3.right * sign, pyramid.BaseSize * 0.5f, Level_Handles.Width, false,
                out float half))
            {
                serializedObject.FindProperty("baseSize").floatValue = Mathf.Max(2f, Level_Handles.Snap(half * 2f, pyramid.Snap));
                changed = true;
            }
        }

        float newYaw = Level_Handles.TurnRing(space.position, space.up, pyramid.Yaw, pyramid.BaseSize * 0.75f);

        if (!Mathf.Approximately(newYaw, pyramid.Yaw))
        {
            serializedObject.FindProperty("yaw").floatValue = Mathf.Clamp(newYaw, -180f, 180f);
            changed = true;
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            Changed();
        }

        if (pyramid.HasEntrance)
            DrawEntrance(pyramid);
    }

    private void DrawEntrance(Level_EgyptPyramid pyramid)
    {
        Transform space = pyramid.transform;
        pyramid.EntranceFrame(out Vector3 point, out Quaternion look);

        // Axes: right runs along the face, forward points out of the pyramid
        Matrix4x4 door = space.localToWorldMatrix * Matrix4x4.TRS(point, look, Vector3.one);
        float width = pyramid.EntranceWidth;
        float height = pyramid.EntranceHeight;

        Handles.color = Level_Handles.Outline;
        Handles.DrawAAPolyLine(Level_Handles.LineWidth, door.MultiplyPoint3x4(new Vector3(-width * 0.5f, 0f, 0f)),
            door.MultiplyPoint3x4(new Vector3(-width * 0.5f, height, 0f)), door.MultiplyPoint3x4(new Vector3(width * 0.5f, height, 0f)),
            door.MultiplyPoint3x4(new Vector3(width * 0.5f, 0f, 0f)));

        // Which way goblins come out
        Handles.DrawAAPolyLine(Level_Handles.LineWidth, door.MultiplyPoint3x4(new Vector3(0f, 0.05f, 0f)), door.MultiplyPoint3x4(new Vector3(0f, 0.05f, 2.5f)));

        // Slide round the base: the handle follows the cursor, then the door is put on the nearest face
        Vector3 handle = door.MultiplyPoint3x4(new Vector3(0f, 0.05f, 0f));
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Level_Handles.Move(handle, space);

        if (EditorGUI.EndChangeCheck())
            pyramid.MoveEntrance(Level_Handles.SnapPlanar(space.InverseTransformPoint(moved), pyramid.Snap));

        serializedObject.Update();
        bool changed = false;

        for (int sign = -1; sign <= 1; sign += 2)
        {
            if (Level_BoxHandles.Reach(door, new Vector3(0f, height * 0.5f, 0f), Vector3.right * sign, width * 0.5f, Level_Handles.Width,
                false, out float half))
            {
                serializedObject.FindProperty("entranceWidth").floatValue = Mathf.Max(0.5f, Level_Handles.Snap(half * 2f, pyramid.Snap));
                changed = true;
            }
        }

        if (Level_BoxHandles.Reach(door, Vector3.zero, Vector3.up, height, Level_Handles.Height, true, out float reached))
        {
            serializedObject.FindProperty("entranceHeight").floatValue = Mathf.Max(0.5f, Level_Handles.Snap(reached, pyramid.Snap));
            changed = true;
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            Changed();
        }
    }

    [MenuItem("GameObject/Level/Egyptian/Pyramid", false, 51)]
    private static void Create(MenuCommand command) => Level_EgyptLook.Create<Level_EgyptPyramid>("Egyptian Pyramid", command);
}
