using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// What Level_NodeHandles needs to know about a tool's list of nodes. Each tool stores nodes its own way (structs,
// classes, plain positions), so a small subclass of this adapts them. Positions are flat, in Space's local XZ.
public abstract class Level_NodeList
{
    public Object Owner;
    public Transform Space;
    public bool Closed;
    public int Minimum = 2;
    public int Maximum = 512;
    public float Snap;

    [Tooltip("How far past the end of an open line the green extend dot sits, metres.")]
    public float ExtendDistance = 5f;

    // Also offer the extend dot past the first node, not just the last
    public bool ExtendStart;

    // Refuse a move or delete that makes the outline cross itself, keeping the last valid shape
    public bool RejectCrossing;

    public abstract int Count { get; }
    public abstract Vector3 Get(int index);
    public abstract void Set(int index, Vector3 position);
    public abstract void RemoveAt(int index);

    // A new node at index, blended from the nodes either side (from and to are the same at the end of a line)
    public abstract void Insert(int index, Vector3 position, int from, int to);

    // True for a section drawn as an opening (green) rather than a solid edge
    public virtual bool IsGap(int index) => false;

    // Per-node handles beyond moving, like height and width. Return true if something changed (record Undo yourself).
    public virtual bool DrawNodeHandles(int index, Vector3 local) => false;
}

// A list of plain positions: the usual adapter for outlines
public sealed class Level_PositionNodes : Level_NodeList
{
    private readonly List<Vector3> nodes;

    public Level_PositionNodes(List<Vector3> nodes) => this.nodes = nodes;

    public override int Count => nodes.Count;
    public override Vector3 Get(int index) => Level_Handles.Flat(nodes[index]);
    public override void Set(int index, Vector3 position) => nodes[index] = position;
    public override void RemoveAt(int index) => nodes.RemoveAt(index);
    public override void Insert(int index, Vector3 position, int from, int to) => nodes.Insert(index, position);
}

// The one set of node gestures every line and outline tool shares: white squares move, green dots insert or extend,
// Ctrl/Cmd turns nodes into red dots that delete. Used through Level_PathHandles and Level_OutlineHandles, or directly.
// It only edits the list and records Undo and prefab changes; the caller decides when to rebuild.
//
// Setup: call from OnSceneGUI. GC: editor only; one polygon copy per move or delete when RejectCrossing is on.
public static class Level_NodeHandles
{
    public static bool Draw(Level_NodeList list)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || list.Count == 0)
            return false;

        Level_Handles.RepaintOnModifierChange();
        bool deleting = EditorGUI.actionKey;
        bool changed = false;
        Transform space = list.Space;
        int edges = list.Closed ? list.Count : list.Count - 1;

        for (int i = 0; i < edges; i++)
        {
            Handles.color = list.IsGap(i) ? Level_Handles.Add : Level_Handles.Outline;
            Handles.DrawLine(space.TransformPoint(list.Get(i)), space.TransformPoint(list.Get((i + 1) % list.Count)), Level_Handles.LineWidth);
        }

        for (int i = 0; i < list.Count && !changed; i++)
        {
            Vector3 local = list.Get(i);
            Vector3 world = space.TransformPoint(local);

            if (deleting)
            {
                if (Level_Handles.DeleteDot(world, list.Count > list.Minimum) && list.Count > list.Minimum && CanRemove(list, i))
                {
                    Undo.RecordObject(list.Owner, "Delete Node");
                    list.RemoveAt(i);
                    changed = true;
                }

                continue;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Level_Handles.Move(world, space);

            if (EditorGUI.EndChangeCheck())
            {
                Vector3 position = Level_Handles.SnapPlanar(Level_Handles.Flat(space.InverseTransformPoint(moved)), list.Snap);

                if (CanMove(list, i, position))
                {
                    Undo.RecordObject(list.Owner, "Move Node");
                    list.Set(i, position);
                    changed = true;
                }

                break;
            }

            changed = list.DrawNodeHandles(i, local);
        }

        if (!deleting && !changed && list.Count < list.Maximum)
            changed = DrawAddDots(list, edges);

        if (changed)
        {
            EditorUtility.SetDirty(list.Owner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(list.Owner);
        }

        return changed;
    }

    private static bool DrawAddDots(Level_NodeList list, int edges)
    {
        Transform space = list.Space;

        for (int i = 0; i < edges; i++)
        {
            int next = (i + 1) % list.Count;
            Vector3 middle = (list.Get(i) + list.Get(next)) * 0.5f;

            if (!Level_Handles.AddDot(space.TransformPoint(middle)))
                continue;

            Undo.RecordObject(list.Owner, "Add Node");
            list.Insert(i + 1, middle, i, next);
            return true;
        }

        if (list.Closed || list.Count < 2)
            return false;

        if (DrawExtendDot(list, list.Count - 1, list.Count - 2, list.Count))
            return true;

        return list.ExtendStart && DrawExtendDot(list, 0, 1, 0);
    }

    private static bool DrawExtendDot(Level_NodeList list, int end, int before, int insertAt)
    {
        Vector3 direction = list.Get(end) - list.Get(before);
        direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.right;
        Vector3 beyond = list.Get(end) + direction * list.ExtendDistance;

        if (!Level_Handles.AddDot(list.Space.TransformPoint(beyond)))
            return false;

        Undo.RecordObject(list.Owner, "Extend Line");
        list.Insert(insertAt, Level_Handles.SnapPlanar(beyond, list.Snap), end, end);
        return true;
    }

    private static Vector2[] Polygon(Level_NodeList list, int skip = -1)
    {
        Vector2[] polygon = new Vector2[skip >= 0 ? list.Count - 1 : list.Count];
        int write = 0;

        for (int i = 0; i < list.Count; i++)
        {
            if (i == skip)
                continue;

            Vector3 point = list.Get(i);
            polygon[write++] = new Vector2(point.x, point.z);
        }

        return polygon;
    }

    // An outline that's already tangled can still be edited, or you'd never be able to untangle it
    private static bool CanMove(Level_NodeList list, int index, Vector3 position)
    {
        if (!list.RejectCrossing)
            return true;

        Vector2[] polygon = Polygon(list);

        if (!Level_SurfaceShape.Valid(polygon))
            return true;

        polygon[index] = new Vector2(position.x, position.z);
        return Level_SurfaceShape.Valid(polygon);
    }

    private static bool CanRemove(Level_NodeList list, int index)
    {
        return !list.RejectCrossing || !Level_SurfaceShape.Valid(Polygon(list)) || Level_SurfaceShape.Valid(Polygon(list, index));
    }
}

// The Outline kind of tool: a closed polygon you can move, insert into and delete from. A move that would make it cross
// itself is refused, keeping the last valid shape. Returns true if the nodes changed.
// Setup: call from OnSceneGUI with the tool's corners. GC: editor only, one small adapter per call.
public static class Level_OutlineHandles
{
    public static bool Draw(Object owner, Transform space, List<Vector3> nodes, float snap, int minimum = 3, int maximum = 512)
    {
        return Level_NodeHandles.Draw(new Level_PositionNodes(nodes)
        {
            Owner = owner,
            Space = space,
            Closed = true,
            Minimum = minimum,
            Maximum = maximum,
            Snap = snap,
            RejectCrossing = true
        });
    }
}
