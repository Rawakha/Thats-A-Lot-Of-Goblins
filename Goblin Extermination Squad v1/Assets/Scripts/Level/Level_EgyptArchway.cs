using System;
using System.Collections.Generic;
using UnityEngine;

// A standalone Egyptian archway: two pillar-style piers either side of a clear opening, with a lintel slab and a teal trim
// across the top. Built on the level tool foundation (Level_Tool), so editable stones, the one-mesh bake and Undo come
// from there. The opening is empty: nothing solid bridges it, and the lintel starts at Clear Height, so goblins walk
// through. Keep Clear Height above the flow bake's ground clearance plus obstacle height (about 3.2 with the defaults).
//
// Setup: GameObject > Level > Egyptian > Archway. White square moves it, yellow cone sets the clear height, orange cubes set
// the opening's width, pink cubes set its depth, the blue ring turns it. It stands on its origin, in the middle of the
// opening, facing along its own forward axis.
// GC: nothing at runtime, no ticks or subscriptions. Generating allocates working lists and objects in the editor only.
public class Level_EgyptArchway : Level_Tool
{
    [Header("Opening")]
    [Tooltip("Width of the clear opening between the piers, metres.")]
    [SerializeField, Min(1f)] private float clearWidth = 4f;
    [Tooltip("Height of the clear space under the lintel, metres. Keep it above the flow bake's ground clearance + obstacle height.")]
    [SerializeField, Min(0.5f)] private float clearHeight = 4.5f;
    [Tooltip("How deep the archway is front to back, metres.")]
    [SerializeField, Min(0.3f)] private float depth = 1.6f;

    [Header("Piers")]
    [Tooltip("Width of each pier at its base and capital, metres.")]
    [SerializeField, Min(0.4f)] private float pierWidth = 1.6f;
    [Tooltip("How far each pier rises above the top of the lintel, metres.")]
    [SerializeField, Min(0f)] private float pierRise = 0.35f;
    [Tooltip("Height of each pier's base block, metres.")]
    [SerializeField, Min(0.1f)] private float baseHeight = 0.35f;
    [Tooltip("Height of each pier's capital, metres.")]
    [SerializeField, Min(0.1f)] private float capitalHeight = 0.35f;
    [Tooltip("Height of the coloured band under each capital, metres.")]
    [SerializeField, Min(0.05f)] private float bandHeight = 0.12f;

    [Header("Lintel")]
    [Tooltip("Thickness of the lintel slab, metres.")]
    [SerializeField, Min(0.1f)] private float lintelHeight = 0.5f;
    [Tooltip("Thickness of the coloured trim on top of the lintel, metres.")]
    [SerializeField, Min(0.05f)] private float trimHeight = 0.15f;

    [Header("Rotation")]
    [Tooltip("Spin around the vertical axis, degrees.")]
    [SerializeField, Range(-180f, 180f)] private float yaw;

    public float ClearWidth => clearWidth;
    public float ClearHeight => clearHeight;
    public float Depth => depth;
    public float Yaw => yaw;
    public Quaternion Rotation => Quaternion.Euler(0f, yaw, 0f);

    protected override void BuildPieces(List<Level_ToolPiece> pieces)
    {
        float pierHeight = clearHeight + lintelHeight + trimHeight + pierRise;
        float shaft = pierHeight - baseHeight - capitalHeight - bandHeight;

        if (shaft < 0.2f)
            throw new InvalidOperationException("The piers are too short for their base, band and capital. Raise Clear Height or shrink the trim.");

        Quaternion rotation = Rotation;
        float pierX = clearWidth * 0.5f + pierWidth * 0.5f;

        for (int side = -1; side <= 1; side += 2)
        {
            string name = side < 0 ? "Left" : "Right";
            float x = pierX * side;
            pieces.Add(new Level_ToolPiece($"{name}/Base", Place(rotation, x, 0f), rotation,
                new Vector3(pierWidth, baseHeight, depth), "Trim", "Pier Base"));
            pieces.Add(new Level_ToolPiece($"{name}/Shaft", Place(rotation, x, baseHeight), rotation,
                new Vector3(pierWidth * 0.7f, shaft, depth * 0.7f), "Main", "Pier Shaft"));
            pieces.Add(new Level_ToolPiece($"{name}/Band", Place(rotation, x, baseHeight + shaft), rotation,
                new Vector3(pierWidth * 0.9f, bandHeight, depth * 0.9f), "Accent", "Pier Band"));
            pieces.Add(new Level_ToolPiece($"{name}/Capital", Place(rotation, x, pierHeight - capitalHeight), rotation,
                new Vector3(pierWidth, capitalHeight, depth), "Trim", "Pier Capital"));
        }

        // The lintel runs across both piers, so its ends are hidden inside their shafts
        float lintelWidth = clearWidth + pierWidth * 2f;
        pieces.Add(new Level_ToolPiece("Lintel", Place(rotation, 0f, clearHeight), rotation,
            new Vector3(lintelWidth, lintelHeight, depth), "Trim", "Lintel"));
        pieces.Add(new Level_ToolPiece("Trim", Place(rotation, 0f, clearHeight + lintelHeight), rotation,
            new Vector3(lintelWidth, trimHeight, depth), "Accent", "Lintel Trim"));
    }

    private static Vector3 Place(Quaternion rotation, float x, float y) => rotation * new Vector3(x, y, 0f);
}
