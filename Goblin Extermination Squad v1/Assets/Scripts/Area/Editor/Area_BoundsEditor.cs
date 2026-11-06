using UnityEditor;
using UnityEngine;

// Scene view handles for an area's edges, matching the level tools: the white square moves it, orange cubes set the
// width, pink cubes the length, and the yellow cube on the east side of the red line sets the margin.
[CustomEditor(typeof(Area_Bounds))]
public class Area_BoundsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (DrawDefaultInspector()) ((Area_Bounds)target).RefreshWarningAppearance();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Rebuild Red Warning Line")) ((Area_Bounds)target).RebuildWarningLine();
    }
    private static readonly Color Orange = new Color(1f, 0.55f, 0.1f);
    private static readonly Color Pink = new Color(1f, 0.4f, 0.8f);

    private void OnSceneGUI()
    {
        Area_Bounds bounds = (Area_Bounds)target;
        Transform transform = bounds.transform;
        Vector3 centre = transform.position;

        serializedObject.Update();
        SerializedProperty size = serializedObject.FindProperty("warningSize");
        SerializedProperty margin = serializedObject.FindProperty("margin");
        Vector2 value = size.vector2Value;

        // Move
        Handles.color = Color.white;
        float squareSize = HandleUtility.GetHandleSize(centre) * 0.3f;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider2D(centre, Vector3.up, Vector3.forward, Vector3.right, squareSize, Handles.RectangleHandleCap, 0f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(transform, "Move Area Bounds");
            transform.position = new Vector3(moved.x, centre.y, moved.z);
            bounds.RequestWarningLine();
        }

        // Width and length, both sides, staying centred
        Handles.color = Orange;
        value.x = SideHandle(centre, Vector3.right, value.x);
        value.x = SideHandle(centre, Vector3.left, value.x);
        Handles.color = Pink;
        value.y = SideHandle(centre, Vector3.forward, value.y);
        value.y = SideHandle(centre, Vector3.back, value.y);
        size.vector2Value = Vector2.Max(value, Vector2.one);

        // Margin
        Handles.color = Color.yellow;
        Vector3 marginPoint = centre + Vector3.right * (value.x * 0.5f + margin.floatValue);
        EditorGUI.BeginChangeCheck();
        Vector3 newMargin = Handles.Slider(marginPoint, Vector3.right, HandleUtility.GetHandleSize(marginPoint) * 0.15f, Handles.CubeHandleCap, 0f);

        if (EditorGUI.EndChangeCheck())
            margin.floatValue = Mathf.Max(0.1f, newMargin.x - centre.x - value.x * 0.5f);

        serializedObject.ApplyModifiedProperties();
    }

    private static float SideHandle(Vector3 centre, Vector3 side, float length)
    {
        Vector3 point = centre + side * (length * 0.5f);
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(point, side, HandleUtility.GetHandleSize(point) * 0.15f, Handles.CubeHandleCap, 0f);

        return EditorGUI.EndChangeCheck() ? Vector3.Dot(moved - centre, side) * 2f : length;
    }
}

// The spawn point's handles: the white square moves it, the pink cube sets how far south the plane starts.
[CustomEditor(typeof(Area_Spawn))]
public class Area_SpawnEditor : Editor
{
    private void OnSceneGUI()
    {
        Area_Spawn spawn = (Area_Spawn)target;
        Transform transform = spawn.transform;
        Vector3 point = transform.position;

        Handles.color = Color.white;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider2D(point, Vector3.up, Vector3.forward, Vector3.right, HandleUtility.GetHandleSize(point) * 0.3f, Handles.RectangleHandleCap, 0f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(transform, "Move Area Spawn");
            transform.position = new Vector3(moved.x, point.y, moved.z);
        }

        serializedObject.Update();
        SerializedProperty distance = serializedObject.FindProperty("flyInDistance");
        Vector3 start = spawn.Start;

        Handles.color = new Color(1f, 0.4f, 0.8f);
        EditorGUI.BeginChangeCheck();
        Vector3 newStart = Handles.Slider(start, Vector3.back, HandleUtility.GetHandleSize(start) * 0.15f, Handles.CubeHandleCap, 0f);

        if (EditorGUI.EndChangeCheck())
            distance.floatValue = Mathf.Max(0f, point.z - newStart.z);

        serializedObject.ApplyModifiedProperties();
    }
}

