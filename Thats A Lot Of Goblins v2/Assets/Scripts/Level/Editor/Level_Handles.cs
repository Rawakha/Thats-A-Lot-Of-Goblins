using UnityEditor;
using UnityEngine;

// Shared Scene view handle colours and snapping for level tools. Called by their custom editors; no setup.
// GC: static value types only. Editor-only, no runtime allocations or subscriptions.
public static class Level_Handles
{
    public static readonly Color Outline = new Color(0.3f, 0.9f, 1f, 0.8f);
    public static readonly Color Height = new Color(1f, 0.85f, 0.2f);
    public static readonly Color Width = new Color(1f, 0.55f, 0.15f);
    public static readonly Color Length = new Color(1f, 0.45f, 0.8f);
    public static readonly Color Add = new Color(0.35f, 1f, 0.4f);
    public static readonly Color Delete = new Color(1f, 0.25f, 0.2f);

    public static float Snap(float value, float step) => step > 0f ? Mathf.Round(value / step) * step : value;

    public static Vector3 SnapPlanar(Vector3 value, float step)
        => new Vector3(Snap(value.x, step), value.y, Snap(value.z, step));

    public static Vector3 Move(Vector3 world, Transform space)
    {
        Handles.color = Color.white;
        return Handles.Slider2D(world, space.up, space.right, space.forward,
            HandleUtility.GetHandleSize(world) * 0.08f, Handles.RectangleHandleCap, Vector2.zero);
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
