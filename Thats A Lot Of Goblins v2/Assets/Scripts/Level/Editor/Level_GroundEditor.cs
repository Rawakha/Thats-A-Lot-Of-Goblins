using UnityEditor;
using UnityEngine;

// Select a Level_Ground: white square moves it, orange cubes set width, pink cubes set length.
// Outline previews edits; the mesh and collider rebuild on release. Inspector settings also rebuild automatically.
// GC: Unity editor serialization/Undo only, no runtime allocations or new subscriptions.
[CustomEditor(typeof(Level_Ground))]
public class Level_GroundEditor : Level_GroundShapeEditor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Scene view: drag the white square to move the ground, orange cubes for width, " +
            "pink cubes for length. Snap controls movement and sizes (0 is free). The outline previews changes; " +
            "the ground and collider rebuild when you release.", MessageType.Info);
        base.OnInspectorGUI();
    }

    private void OnSceneGUI()
    {
        if (Application.isPlaying)
            return;

        Level_Ground ground = (Level_Ground)target;
        Transform space = ground.transform;
        serializedObject.Update();
        SerializedProperty property = serializedObject.FindProperty("size");
        Vector2 size = property.vector2Value;
        float snap = serializedObject.FindProperty("snap").floatValue;

        Handles.color = Level_Handles.Outline;
        Vector3 a = space.TransformPoint(new Vector3(-size.x, 0f, -size.y) * 0.5f);
        Vector3 b = space.TransformPoint(new Vector3(size.x, 0f, -size.y) * 0.5f);
        Vector3 c = space.TransformPoint(new Vector3(size.x, 0f, size.y) * 0.5f);
        Vector3 d = space.TransformPoint(new Vector3(-size.x, 0f, size.y) * 0.5f);
        Handles.DrawLine(a, b, 3f);
        Handles.DrawLine(b, c, 3f);
        Handles.DrawLine(c, d, 3f);
        Handles.DrawLine(d, a, 3f);

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Level_Handles.Move(space.position, space);
        if (EditorGUI.EndChangeCheck())
        {
            BeginShapeChange();
            Undo.RecordObject(space, "Move Level Ground");
            Vector3 offset = space.InverseTransformVector(moved - space.position);
            Vector3 localPosition = space.InverseTransformVector(space.position);
            Vector3 snapped = Level_Handles.SnapPlanar(localPosition + offset, snap);
            space.position = space.TransformVector(snapped);
            PrefabUtility.RecordPrefabInstancePropertyModifications(space);
            Level_Ground.RequestRebuild();
        }

        bool changed = false;
        for (int axis = 0; axis < 2; axis++)
        for (int sign = -1; sign <= 1; sign += 2)
        {
            Vector3 direction = axis == 0 ? Vector3.right : Vector3.forward;
            float extent = axis == 0 ? size.x : size.y;
            Vector3 edge = space.TransformPoint(direction * (extent * 0.5f * sign));
            Handles.color = axis == 0 ? Level_Handles.Width : Level_Handles.Length;
            EditorGUI.BeginChangeCheck();
            Vector3 pulled = Handles.Slider(edge, space.TransformDirection(direction * sign),
                HandleUtility.GetHandleSize(edge) * 0.08f, Handles.CubeHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck())
                continue;

            BeginShapeChange();
            float value = Mathf.Max(0.5f, Level_Handles.Snap(
                Vector3.Dot(space.InverseTransformPoint(pulled), direction) * sign * 2f, snap));
            if (axis == 0)
                size.x = value;
            else
                size.y = value;
            changed = true;
        }

        if (changed)
        {
            property.vector2Value = size;
            serializedObject.ApplyModifiedProperties();
            Level_Ground.RequestRebuild();
        }

        FinishShapeGUI();
    }
}
