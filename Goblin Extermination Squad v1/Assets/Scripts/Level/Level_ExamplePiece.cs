using System.Collections.Generic;
using UnityEngine;

// Copy-paste template for the Piece kind of tool: one object with a width, height and depth. Here it's one plain stone.
// To make a real tool, copy this and its editor in Editor/Level_ExampleEditors.cs, rename them, swap BuildPieces.
// Setup: GameObject > Level > Common > Examples > Piece. GC: nothing at runtime; see Level_Tool.
public class Level_ExamplePiece : Level_Tool
{
    [Tooltip("Width, height and depth, metres.")]
    [SerializeField] private Vector3 size = new Vector3(2f, 4f, 2f);

    protected override void BuildPieces(List<Level_ToolPiece> pieces)
    {
        pieces.Add(new Level_ToolPiece("Body", Vector3.zero, Quaternion.identity, size));
    }
}
