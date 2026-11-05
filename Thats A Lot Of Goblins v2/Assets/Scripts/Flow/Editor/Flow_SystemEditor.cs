using UnityEditor;
using UnityEngine;

// Edit the world-aligned bake grid in the Scene view. Bake Obstacles applies it to the map.
// GC: editor serialization and handles only. No runtime allocations or subscriptions.
[CustomEditor(typeof(Flow_System))]
public class Flow_SystemEditor : InspectorButtonEditor
{
    public override void OnInspectorGUI()
    {
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.HelpBox("Scene view: white square moves the bake grid, orange cubes resize X, " +
                "and pink cubes resize Z in whole cells. Run Bake Obstacles to apply the new bounds.", MessageType.Info);

            if (GUILayout.Button("Center Grid on Transform"))
            {
                serializedObject.Update();
                SerializedProperty grid = serializedObject.FindProperty("bakeGrid");
                float cellSize = grid.FindPropertyRelative("cellSize").floatValue;
                Vector3 half = HalfSize(grid, cellSize);
                grid.FindPropertyRelative("origin").vector3Value = ((Flow_System)target).transform.position - half;
                serializedObject.ApplyModifiedProperties();
                SceneView.RepaintAll();
            }
        }

        base.OnInspectorGUI();
    }

    private void OnSceneGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        serializedObject.Update();
        SerializedProperty grid = serializedObject.FindProperty("bakeGrid");
        SerializedProperty origin = grid.FindPropertyRelative("origin");
        float cellSize = grid.FindPropertyRelative("cellSize").floatValue;
        if (cellSize <= 0f)
            return;

        Vector3 half = HalfSize(grid, cellSize);
        Vector3 center = origin.vector3Value + half;

        using (new Handles.DrawingScope(Level_Handles.Outline))
        {
            Handles.DrawWireCube(center, half * 2f);
            Handles.Label(center + half, "Flow bake grid");
        }

        using (new Handles.DrawingScope(Color.white))
        {
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider2D(center, Vector3.up, Vector3.right, Vector3.forward,
                HandleUtility.GetHandleSize(center) * 0.08f, Handles.RectangleHandleCap, Vector2.zero);
            if (EditorGUI.EndChangeCheck())
                center = new Vector3(moved.x, center.y, moved.z);
        }

        Resize(grid.FindPropertyRelative("width"), center, Vector3.right, cellSize, Level_Handles.Width);
        Resize(grid.FindPropertyRelative("height"), center, Vector3.forward, cellSize, Level_Handles.Length);

        Vector3 nextHalf = HalfSize(grid, cellSize);
        if (nextHalf != half || center != origin.vector3Value + half)
            origin.vector3Value = center - nextHalf;

        serializedObject.ApplyModifiedProperties();
    }

    private static Vector3 HalfSize(SerializedProperty grid, float cellSize)
    {
        return new Vector3(grid.FindPropertyRelative("width").intValue, 0f,
            grid.FindPropertyRelative("height").intValue) * (cellSize * 0.5f);
    }

    private static void Resize(SerializedProperty count, Vector3 center, Vector3 axis, float cellSize, Color color)
    {
        float half = count.intValue * cellSize * 0.5f;
        using (new Handles.DrawingScope(color))
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 direction = axis * sign;
                Vector3 edge = center + direction * half;
                EditorGUI.BeginChangeCheck();
                Vector3 pulled = Handles.Slider(edge, direction, HandleUtility.GetHandleSize(edge) * 0.08f,
                    Handles.CubeHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                    count.intValue = Mathf.Max(1, Mathf.RoundToInt(Vector3.Dot(pulled - center, direction) * 2f / cellSize));
            }
        }
    }
}
