using UnityEditor;
using UnityEngine;

// What every level tool shows in the Scene view while selected: the same footprint outline and the same status label
// (Editing, Baked or Needs Bake for bake tools; Built for tools that make their own output).
//
// Setup: call from OnSceneGUI. GC: editor only; a string per label, on repaint only.
public static class Level_ToolFeedback
{
    private static GUIStyle labelStyle;

    public static void Footprint(Matrix4x4 space, Vector3[] points, bool closed)
    {
        Handles.color = Level_Handles.Outline;
        int edges = closed ? points.Length : points.Length - 1;

        for (int i = 0; i < edges; i++)
            Handles.DrawLine(space.MultiplyPoint3x4(points[i]), space.MultiplyPoint3x4(points[(i + 1) % points.Length]), Level_Handles.LineWidth);
    }

    public static void Label(Vector3 world, string text, Color colour)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        labelStyle ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.LowerCenter };
        labelStyle.normal.textColor = colour;
        Handles.Label(world, text, labelStyle);
    }

    // For tools that build their own output
    public static void Built(Transform tool, string detail = null)
    {
        Label(tool.position, detail == null ? $"{tool.name}: Built" : $"{tool.name}: Built, {detail}", Level_Handles.Outline);
    }

    public static string StatusText(Level_BakeGroup group)
    {
        switch (group.Status)
        {
            case Level_BakeStatus.NeedsBake: return "Needs Bake";
            case Level_BakeStatus.Editing: return "Editing";
            default: return $"Baked, {group.BakedPieces} pieces";
        }
    }

    public static Color StatusColour(Level_BakeGroup group)
    {
        switch (group.Status)
        {
            case Level_BakeStatus.NeedsBake: return Level_Handles.Delete;
            case Level_BakeStatus.Editing: return Level_Handles.Height;
            default: return Level_Handles.Add;
        }
    }

    public static void Status(Level_BakeGroup group)
    {
        Label(group.transform.position, $"{group.name}: {StatusText(group)}", StatusColour(group));
    }
}
