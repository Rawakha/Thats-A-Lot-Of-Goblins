using System.Collections.Generic;
using UnityEngine;

// Copy-paste template for the Outline kind of tool: a closed polygon. Here it's stones scattered inside the outline,
// with one trim-coloured stone on each corner. To make a real tool, copy this and its editor in
// Editor/Level_ExampleEditors.cs. Use the seed for anything random, so a rebuild gives the same result.
// Setup: GameObject > Level > Common > Examples > Outline. GC: nothing at runtime; see Level_Tool.
public class Level_ExampleOutline : Level_Tool
{
    [Tooltip("Stones scattered inside the outline.")]
    [SerializeField, Range(0, 200)] private int stones = 20;
    [Tooltip("Width and depth of each stone, metres.")]
    [SerializeField, Min(0.25f)] private float stoneSize = 1.5f;
    [SerializeField] private List<Vector3> nodes = new List<Vector3>
    {
        new Vector3(-6f, 0f, -6f), new Vector3(6f, 0f, -6f), new Vector3(6f, 0f, 6f), new Vector3(-6f, 0f, 6f)
    };

    public List<Vector3> Nodes => nodes;

    protected override void BuildPieces(List<Level_ToolPiece> pieces)
    {
        Vector2[] outline = new Vector2[nodes.Count];
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = -min;

        for (int i = 0; i < nodes.Count; i++)
        {
            outline[i] = new Vector2(nodes[i].x, nodes[i].z);
            min = Vector2.Min(min, outline[i]);
            max = Vector2.Max(max, outline[i]);
            pieces.Add(new Level_ToolPiece($"Corner {i}", new Vector3(outline[i].x, 0f, outline[i].y), Quaternion.identity, Vector3.one * stoneSize, "Trim"));
        }

        Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex((uint)seed);

        for (int s = 0; s < stones; s++)
        {
            Vector2 point = new Vector2(random.NextFloat(min.x, max.x), random.NextFloat(min.y, max.y));

            if (Level_SurfaceShape.SignedDistance(point, outline) > 0f)
                pieces.Add(new Level_ToolPiece($"Stone {s}", new Vector3(point.x, 0f, point.y), Quaternion.identity, Vector3.one * stoneSize));
        }
    }
}
