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
        Level_ToolInspector.Help("Scene view: drag the white square to move the ground, orange cubes for width, " +
            "pink cubes for length. Snap controls movement and sizes (0 is free). The outline previews changes; " +
            "the ground and collider rebuild when you release.");
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
        Vector3 box = new Vector3(size.x, 0f, size.y);

        if (Level_BoxHandles.MoveObject(space, snap))
        {
            BeginShapeChange();
            Level_Ground.RequestRebuild();
        }

        if (Level_BoxHandles.DrawSize(space, ref box, snap, 0.5f, false))
        {
            BeginShapeChange();
            property.vector2Value = new Vector2(box.x, box.z);
            serializedObject.ApplyModifiedProperties();
            Level_Ground.RequestRebuild();
        }

        Level_ToolFeedback.Built(space);
        FinishShapeGUI();
    }
}
