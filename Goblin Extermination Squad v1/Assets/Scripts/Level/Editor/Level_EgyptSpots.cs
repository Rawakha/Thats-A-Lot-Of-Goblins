using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The purple "place something here" spots on an Egyptian wall, shared by the pillar and (later) the archway: one at every
// node (corners and ends) and one halfway along every stretch that has a wall. Click one and the caller places its object.
// Spots that already hold something are hidden, so you can see what's left. Spots sit at half the wall's height, away from
// the green add-node dots on the ground.
//
// Setup: call Pick from a wall editor's DrawHandles. GC: editor only, one list per call. No subscriptions.
public static class Level_EgyptSpots
{
    private const float TakenRadius = 0.6f;

    // Where a spot is, in the wall's local space, which way the wall runs there, and its height and thickness (metres)
    public struct Spot
    {
        public Vector3 position;
        public Vector3 direction;
        public float height;
        public float thickness;
    }

    // taken: wall-local positions already holding an object. Returns true on the frame a spot is clicked.
    public static bool Pick(Level_EgyptWall wall, List<Vector3> taken, out Spot picked)
    {
        picked = default;
        List<Level_EgyptWallNode> nodes = wall.Nodes;

        if (nodes == null || nodes.Count < 2 || nodes.Contains(null))
            return false;

        Matrix4x4 space = wall.transform.localToWorldMatrix;

        for (int i = 0; i < nodes.Count; i++)
        {
            Level_EgyptWallNode node = nodes[i];
            Vector3 position = Level_Handles.Flat(node.position);
            int other = i + 1 < nodes.Count ? i + 1 : i - 1;
            Vector3 direction = (Level_Handles.Flat(nodes[other].position) - position) * (other > i ? 1f : -1f);
            Spot spot = Make(wall, position, direction, node.height, node.height, node.thickness, node.thickness);

            if (Try(space, spot, taken, ref picked))
                return true;

            if (i + 1 < nodes.Count && !node.gapAfter)
            {
                Level_EgyptWallNode next = nodes[i + 1];
                Vector3 end = Level_Handles.Flat(next.position);
                spot = Make(wall, (position + end) * 0.5f, end - position, node.height, next.height, node.thickness, next.thickness);

                if (Try(space, spot, taken, ref picked))
                    return true;
            }
        }

        return false;
    }

    private static Spot Make(Level_EgyptWall wall, Vector3 position, Vector3 direction, float heightA, float heightB,
        float thicknessA, float thicknessB)
    {
        direction.y = 0f;
        return new Spot
        {
            position = position,
            direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.right,
            height = wall.Height * (heightA + heightB) * 0.5f,
            thickness = wall.Thickness * (thicknessA + thicknessB) * 0.5f
        };
    }

    private static bool Try(Matrix4x4 space, Spot spot, List<Vector3> taken, ref Spot picked)
    {
        for (int i = 0; i < taken.Count; i++)
        {
            if ((taken[i] - spot.position).sqrMagnitude < TakenRadius * TakenRadius)
                return false;
        }

        Vector3 world = space.MultiplyPoint3x4(spot.position + Vector3.up * spot.height * 0.5f);

        if (!Level_Handles.PlaceButton(world))
            return false;

        picked = spot;
        return true;
    }
}
