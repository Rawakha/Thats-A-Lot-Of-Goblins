using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The Path kind of tool: an open or closed line of Level_PathNodes, with the shared node gestures plus a yellow height
// cone and an orange thickness cube per node. Limits and the extend distance are parameters with the usual defaults.
// Rebuilds on release through the owner's RequestRefresh.
//
// Setup: call Draw from OnSceneGUI. GC: editor only, one small adapter per call.
public static class Level_PathHandles
{
    public struct Limits
    {
        public Vector2 height;
        public Vector2 thickness;
        public float extendDistance;
        public int maximumNodes;

        public static Limits Default => new Limits
        {
            height = new Vector2(0.2f, 3f),
            thickness = new Vector2(0.3f, 3f),
            extendDistance = 5f,
            maximumNodes = 128
        };
    }

    private sealed class PathNodes<T> : Level_NodeList where T : Level_PathNode, new()
    {
        public List<T> Nodes;
        public float Height, Thickness;
        public Limits Range;
        public System.Func<T, T, Vector3, T> Blend;
        public System.Predicate<T> Gap;

        public override int Count => Nodes.Count;
        public override Vector3 Get(int index) => Level_Handles.Flat(Nodes[index].position);
        public override void Set(int index, Vector3 position) => Nodes[index].position = position;
        public override void RemoveAt(int index) => Nodes.RemoveAt(index);
        public override bool IsGap(int index) => Gap != null && Gap(Nodes[index]);

        // Extending a line's end starts a fresh node; inserting between two blends them
        public override void Insert(int index, Vector3 position, int from, int to)
        {
            Nodes.Insert(index, from == to || Blend == null ? new T { position = position } : Blend(Nodes[from], Nodes[to], position));
        }

        public override bool DrawNodeHandles(int index, Vector3 local)
        {
            T node = Nodes[index];
            Matrix4x4 matrix = Space.localToWorldMatrix;

            if (Level_BoxHandles.Reach(matrix, local, Vector3.up, Height * node.height, Level_Handles.Height, true, out float reached))
            {
                Undo.RecordObject(Owner, "Set Path Height");
                node.height = Mathf.Clamp(Level_Handles.Snap(reached, Snap) / Mathf.Max(0.5f, Height), Range.height.x, Range.height.y);
                return true;
            }

            Vector3 direction = index + 1 < Nodes.Count ? Get(index + 1) - local : index > 0 ? local - Get(index - 1) : Vector3.right;
            Vector3 side = new Vector3(-direction.z, 0f, direction.x).normalized;

            if (side.sqrMagnitude < 0.01f)
                side = Vector3.forward;

            if (Level_BoxHandles.Reach(matrix, local, side, Thickness * node.thickness * 0.5f, Level_Handles.Width, false, out float half))
            {
                Undo.RecordObject(Owner, "Set Path Thickness");
                node.thickness = Mathf.Clamp(Level_Handles.Snap(Mathf.Abs(half) * 2f, Snap) / Mathf.Max(0.3f, Thickness), Range.thickness.x, Range.thickness.y);
                return true;
            }

            return false;
        }
    }

    public static void Draw<T>(Level_BakeGroup owner, List<T> nodes, bool closed, float height, float thickness, float snap,
        System.Func<T, T, Vector3, T> insert, System.Predicate<T> isGap = null, Limits? limits = null, bool extendStart = false)
        where T : Level_PathNode, new()
    {
        if (nodes == null || nodes.Contains(null))
            return;

        Limits range = limits ?? Limits.Default;
        bool changed = Level_NodeHandles.Draw(new PathNodes<T>
        {
            Owner = owner,
            Space = owner.transform,
            Closed = closed,
            Minimum = closed ? 3 : 2,
            Maximum = range.maximumNodes,
            Snap = snap,
            ExtendDistance = range.extendDistance,
            ExtendStart = extendStart,
            RejectCrossing = closed,
            Nodes = nodes,
            Height = height,
            Thickness = thickness,
            Range = range,
            Blend = insert,
            Gap = isGap
        });

        if (changed)
            owner.RequestRefresh();
    }
}
