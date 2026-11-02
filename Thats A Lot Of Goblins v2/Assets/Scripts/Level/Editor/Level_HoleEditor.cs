using UnityEditor;
using UnityEngine;

// Select a Level_Hole: white square moves its mouth, orange cubes set radius, yellow cone sets depth.
// Handles use the containing ground's space, just like the cut mesh; the hole object's height is ignored.
// GC: ground lookup on Layout plus editor serialization/Undo allocations only. No runtime cost/subscriptions.
[CustomEditor(typeof(Level_Hole))]
public class Level_HoleEditor : Level_GroundShapeEditor
{
    private Level_Ground ground;

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Scene view: drag the white square to move the hole, orange cubes for radius, " +
            "the yellow cone for depth. Snap controls movement and sizes (0 is free). The outline previews " +
            "changes; the ground and collider rebuild when you release.", MessageType.Info);
        base.OnInspectorGUI();
    }

    private void OnSceneGUI()
    {
        if (Application.isPlaying)
            return;

        Level_Hole hole = (Level_Hole)target;
        if (Event.current.type == EventType.Layout && GUIUtility.hotControl == 0)
        {
            ground = null;
            foreach (Level_Ground candidate in FindObjectsByType<Level_Ground>(FindObjectsInactive.Exclude))
            {
                if (!candidate.isActiveAndEnabled || candidate.gameObject.scene != hole.gameObject.scene)
                    continue;
                Vector3 local = candidate.transform.InverseTransformPoint(hole.transform.position);
                Vector2 half = candidate.Size * 0.5f;
                if (Mathf.Abs(local.x) < half.x && Mathf.Abs(local.z) < half.y)
                {
                    ground = candidate;
                    break;
                }
            }
        }

        serializedObject.Update();
        SerializedProperty radius = serializedObject.FindProperty("radius");
        SerializedProperty depth = serializedObject.FindProperty("depth");
        float snap = hole.Snap;
        Transform space = ground != null ? ground.transform : null;
        Vector3 centre = space != null ? space.InverseTransformPoint(hole.transform.position) : hole.transform.position;
        centre.y = 0f;
        Matrix4x4 matrix = space != null ? space.localToWorldMatrix : Matrix4x4.identity;
        Vector3 mouth = matrix.MultiplyPoint3x4(centre);
        Vector3 up = space != null ? space.up : Vector3.up;
        Vector3 right = space != null ? space.right : Vector3.right;
        Vector3 forward = space != null ? space.forward : Vector3.forward;

        using (new Handles.DrawingScope(Level_Handles.Outline, matrix))
        {
            Handles.DrawWireDisc(centre, Vector3.up, radius.floatValue);
            Handles.DrawWireDisc(centre + Vector3.down * depth.floatValue, Vector3.up, radius.floatValue);
            for (int i = 0; i < 4; i++)
            {
                Vector3 edge = centre + Quaternion.Euler(0f, i * 90f, 0f) * Vector3.right * radius.floatValue;
                Handles.DrawDottedLine(edge, edge + Vector3.down * depth.floatValue, 3f);
            }
        }

        Handles.color = Color.white;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider2D(mouth, up, right, forward,
            HandleUtility.GetHandleSize(mouth) * 0.08f, Handles.RectangleHandleCap, Vector2.zero);
        if (EditorGUI.EndChangeCheck())
        {
            BeginShapeChange();
            Vector3 local = matrix.inverse.MultiplyPoint3x4(moved);
            local = Level_Handles.SnapPlanar(local, snap);
            local.y = space != null ? space.InverseTransformPoint(hole.transform.position).y : hole.transform.position.y;
            Undo.RecordObject(hole.transform, "Move Level Hole");
            hole.transform.position = matrix.MultiplyPoint3x4(local);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hole.transform);
            Level_Ground.RequestRebuild();
        }

        bool changed = false;
        for (int i = 0; i < 4; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.right;
            Vector3 edge = matrix.MultiplyPoint3x4(centre + direction * radius.floatValue);
            Handles.color = Level_Handles.Width;
            EditorGUI.BeginChangeCheck();
            Vector3 pulled = Handles.Slider(edge, matrix.MultiplyVector(direction).normalized,
                HandleUtility.GetHandleSize(edge) * 0.08f, Handles.CubeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                BeginShapeChange();
                radius.floatValue = Mathf.Max(0.1f, Level_Handles.Snap(
                    Vector3.Dot(matrix.inverse.MultiplyPoint3x4(pulled) - centre, direction), snap));
                changed = true;
            }
        }

        Vector3 bottom = matrix.MultiplyPoint3x4(centre + Vector3.down * depth.floatValue);
        Handles.color = Level_Handles.Height;
        Handles.DrawDottedLine(mouth, bottom, 3f);
        EditorGUI.BeginChangeCheck();
        Vector3 lowered = Handles.Slider(bottom, -up, HandleUtility.GetHandleSize(bottom) * 0.15f, Handles.ConeHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            BeginShapeChange();
            depth.floatValue = Mathf.Max(0.05f, Level_Handles.Snap(centre.y - matrix.inverse.MultiplyPoint3x4(lowered).y, snap));
            changed = true;
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            Level_Ground.RequestRebuild();
        }

        FinishShapeGUI();
    }
}
