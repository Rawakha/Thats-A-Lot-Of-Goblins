using UnityEditor;
using UnityEngine;

// The shared look of every level tool's Scene view handles, so learning one teaches the rest: white squares move, green
// dots add, red dots delete, yellow cones set height, orange cubes set width, pink cubes set length, blue rings turn.
// Also the snapping. Built on by Level_NodeHandles and Level_BoxHandles; call from a custom editor, no setup.
// GC: static value types only. Editor-only, no runtime allocations or subscriptions.
public static class Level_Handles
{
    public static readonly Color Outline = new Color(0.3f, 0.9f, 1f, 0.8f);
    public static readonly Color Height = new Color(1f, 0.85f, 0.2f);
    public static readonly Color Width = new Color(1f, 0.55f, 0.15f);
    public static readonly Color Length = new Color(1f, 0.45f, 0.8f);
    public static readonly Color Turn = new Color(0.35f, 0.6f, 1f);
    public static readonly Color Add = new Color(0.35f, 1f, 0.4f);
    public static readonly Color Delete = new Color(1f, 0.25f, 0.2f);
    public static readonly Color Place = new Color(0.75f, 0.45f, 1f);

    public const float LineWidth = 3f;
    private const float MoveSize = 0.08f;
    private const float CubeSize = 0.08f;
    private const float ConeSize = 0.13f;
    private const float AddSize = 0.06f;
    private const float DeleteSize = 0.08f;

    public static float Snap(float value, float step) => step > 0f ? Mathf.Round(value / step) * step : value;

    public static Vector3 SnapPlanar(Vector3 value, float step)
        => new Vector3(Snap(value.x, step), value.y, Snap(value.z, step));

    public static Vector3 Flat(Vector3 position) => new Vector3(position.x, 0f, position.z);

    public static Vector3 Move(Vector3 world, Transform space) => Move(world, space.up, space.right, space.forward);

    public static Vector3 Move(Vector3 world, Vector3 up, Vector3 right, Vector3 forward)
    {
        Handles.color = Color.white;
        return Handles.Slider2D(world, up, right, forward,
            HandleUtility.GetHandleSize(world) * MoveSize, Handles.RectangleHandleCap, Vector2.zero);
    }

    public static Vector3 Cube(Vector3 world, Vector3 direction)
        => Handles.Slider(world, direction, HandleUtility.GetHandleSize(world) * CubeSize, Handles.CubeHandleCap, 0f);

    public static Vector3 Cone(Vector3 world, Vector3 direction)
        => Handles.Slider(world, direction, HandleUtility.GetHandleSize(world) * ConeSize, Handles.ConeHandleCap, 0f);

    public static bool AddDot(Vector3 world)
    {
        Handles.color = Add;
        float size = HandleUtility.GetHandleSize(world) * AddSize;
        return Handles.Button(world, Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap);
    }

    // A blue ring that turns an angle (degrees) about a world axis through a point. Returns the new angle.
    public static float TurnRing(Vector3 centre, Vector3 axis, float angle, float radius)
    {
        Handles.color = Turn;
        Quaternion current = Quaternion.AngleAxis(angle, axis);
        Quaternion turned = Handles.Disc(current, centre, axis, radius, false, 0f);

        if (turned == current)
            return angle;

        (turned * Quaternion.Inverse(current)).ToAngleAxis(out float delta, out Vector3 around);
        return angle + (Vector3.Dot(around, axis) < 0f ? -delta : delta);
    }

    // A purple cube: put another tool's object (a pillar, an archway) at this spot
    public static bool PlaceButton(Vector3 world)
    {
        Handles.color = Place;
        float size = HandleUtility.GetHandleSize(world) * 0.1f;
        return Handles.Button(world, Quaternion.identity, size, size * 1.3f, Handles.CubeHandleCap);
    }

    // Grey when the tool can't lose another node
    public static bool DeleteDot(Vector3 world, bool allowed)
    {
        Handles.color = allowed ? Delete : Color.grey;
        float size = HandleUtility.GetHandleSize(world) * DeleteSize;
        return Handles.Button(world, Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap);
    }

    // Ctrl doesn't repaint on its own, so the delete dots would only show once the mouse moved over a handle
    public static void RepaintOnModifierChange()
    {
        EventType type = Event.current.type;

        if (type == EventType.MouseMove || type == EventType.KeyDown || type == EventType.KeyUp)
            SceneView.RepaintAll();
    }
}

// Ground and hole editors share a drag lifetime: outline now, mesh/collider once on release or deselection.
// GC: standard Unity editor/SerializedObject allocations only. No event subscriptions or runtime cost.
public abstract class Level_GroundShapeEditor : InspectorButtonEditor
{
    private bool dragging;

    protected void BeginShapeChange()
    {
        if (!dragging && GUIUtility.hotControl != 0)
        {
            dragging = true;
            Level_Ground.BeginHandleEdit();
        }
    }

    protected void FinishShapeGUI()
    {
        if (dragging && (Event.current.rawType == EventType.MouseUp || GUIUtility.hotControl == 0))
            EndDrag();
    }

    protected virtual void OnDisable() => EndDrag();

    private void EndDrag()
    {
        if (!dragging)
            return;

        dragging = false;
        Level_Ground.EndHandleEdit();
    }
}
