using System.Collections.Generic;
using UnityEngine;

// Copy-paste template for the Path kind of tool: a line of nodes with a height and thickness at each. Here it's a row of
// stones along the line. To make a real tool, copy this and its editor in Editor/Level_ExampleEditors.cs.
// Setup: GameObject > Level > Common > Examples > Path. GC: nothing at runtime; see Level_Tool.
public class Level_ExamplePath : Level_Tool
{
    [Tooltip("Height at a node with Height 1, metres.")]
    [SerializeField, Min(0.25f)] private float height = 3f;
    [Tooltip("Thickness at a node with Thickness 1, metres.")]
    [SerializeField, Min(0.25f)] private float thickness = 1f;
    [Tooltip("Roughly how long each stone is, metres.")]
    [SerializeField, Min(0.25f)] private float stoneLength = 2f;
    [SerializeField] private List<Level_PathNode> nodes = new List<Level_PathNode>
    {
        new Level_PathNode { position = new Vector3(-5f, 0f, 0f) },
        new Level_PathNode { position = new Vector3(5f, 0f, 0f) }
    };

    public List<Level_PathNode> Nodes => nodes;
    public float Height => height;
    public float Thickness => thickness;

    protected override void BuildPieces(List<Level_ToolPiece> pieces)
    {
        for (int edge = 0; edge + 1 < nodes.Count; edge++)
        {
            Level_PathNode a = nodes[edge];
            Level_PathNode b = nodes[edge + 1];
            Vector3 line = b.position - a.position;
            line.y = 0f;
            int stones = Mathf.Max(1, Mathf.RoundToInt(line.magnitude / stoneLength));

            if (line.sqrMagnitude < 1e-4f)
                continue;

            for (int s = 0; s < stones; s++)
            {
                float t = (s + 0.5f) / stones;
                Vector3 position = new Vector3(a.position.x, 0f, a.position.z) + line * t;
                Vector3 size = new Vector3(Mathf.Lerp(a.thickness, b.thickness, t) * thickness,
                    Mathf.Lerp(a.height, b.height, t) * height, line.magnitude / stones);

                // Ids from the edge and stone: inserting a node renumbers what's after it, so give a real tool's
                // nodes their own saved ids if hand-moved stones must follow them
                pieces.Add(new Level_ToolPiece($"Edge {edge} Stone {s}", position, Quaternion.LookRotation(line), size));
            }
        }
    }
}
