using UnityEditor;
using UnityEngine;

// The Piece kind of tool: one object with a width (orange), depth (pink) and height (yellow), like a pillar, a door or
// the ground. The pieces are public too, so a round hole or a cliff's node can use the same cubes and cones.
//
// Setup: call from OnSceneGUI; the caller records Undo (SerializedObject does it for you) and decides when to rebuild.
// GC: editor only, none per call.
public static class Level_BoxHandles
{
    // Whole-box sizes on the object's own axes, standing on its origin. Cubes on both sides, a cone on top if useHeight.
    // size is (width, height, depth). Returns true if it changed.
    public static bool DrawSize(Transform space, ref Vector3 size, float snap, float minimum = 0.25f, bool useHeight = true)
    {
        Matrix4x4 matrix = space.localToWorldMatrix;
        bool changed = false;

        DrawFootprint(matrix, size);

        for (int sign = -1; sign <= 1; sign += 2)
        {
            if (Reach(matrix, Vector3.zero, Vector3.right * sign, size.x * 0.5f, Level_Handles.Width, false, out float width))
            {
                size.x = Mathf.Max(minimum, Level_Handles.Snap(width * 2f, snap));
                changed = true;
            }

            if (Reach(matrix, Vector3.zero, Vector3.forward * sign, size.z * 0.5f, Level_Handles.Length, false, out float depth))
            {
                size.z = Mathf.Max(minimum, Level_Handles.Snap(depth * 2f, snap));
                changed = true;
            }
        }

        if (useHeight && Reach(matrix, Vector3.zero, Vector3.up, size.y, Level_Handles.Height, true, out float height))
        {
            size.y = Mathf.Max(minimum, Level_Handles.Snap(height, snap));
            changed = true;
        }

        return changed;
    }

    // The white square that moves the whole object along its ground plane, snapped. Records Undo itself.
    public static bool MoveObject(Transform space, float snap)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Level_Handles.Move(space.position, space);

        if (!EditorGUI.EndChangeCheck())
            return false;

        Undo.RecordObject(space, "Move Level Object");
        Vector3 offset = space.InverseTransformVector(moved - space.position);
        Vector3 localPosition = space.InverseTransformVector(space.position);
        space.position = space.TransformVector(Level_Handles.SnapPlanar(localPosition + offset, snap));
        PrefabUtility.RecordPrefabInstancePropertyModifications(space);
        return true;
    }

    // One handle a distance along a direction from a point, in the matrix's space: a cone with a dotted line (cone true)
    // or a cube. Returns true when dragged, with the new distance. All the sizing handles are this.
    public static bool Reach(Matrix4x4 space, Vector3 from, Vector3 direction, float distance, Color colour, bool cone, out float reached)
    {
        Vector3 start = space.MultiplyPoint3x4(from);
        Vector3 end = space.MultiplyPoint3x4(from + direction * distance);
        Vector3 worldDirection = space.MultiplyVector(direction).normalized;
        Handles.color = colour;
        EditorGUI.BeginChangeCheck();
        Vector3 pulled;

        if (cone)
        {
            Handles.DrawDottedLine(start, end, 3f);
            pulled = Level_Handles.Cone(end, worldDirection);
        }
        else
        {
            pulled = Level_Handles.Cube(end, worldDirection);
        }

        reached = distance;

        if (!EditorGUI.EndChangeCheck())
            return false;

        reached = Vector3.Dot(space.inverse.MultiplyPoint3x4(pulled) - from, direction);
        return true;
    }

    private static void DrawFootprint(Matrix4x4 space, Vector3 size)
    {
        float x = size.x * 0.5f;
        float z = size.z * 0.5f;
        Level_ToolFeedback.Footprint(space, new[]
        {
            new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z), new Vector3(x, 0f, z), new Vector3(-x, 0f, z)
        }, true);
    }
}
