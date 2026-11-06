using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// An Egyptian pillar: a pedestal, a square shaft, a coloured band and a capital, standing on its origin. Built on the
// level tool foundation (Level_Tool), so it gets editable stones, the one-mesh bake and Undo from there.
//
// Turn spins it around the vertical axis (which way its square faces). Roll tips it over around its own forward axis
// from its base, for leaning and fallen ruins. The Scene handles: white square moves, yellow cone sets height, orange
// cubes set width, blue rings set Turn (flat, at the base) and Roll (upright, at mid-height).
//
// Setup: GameObject > Level > Egyptian > Pillar for a standalone one. Select a Wall and use its purple spots to place pillars on
// its corners, ends and between nodes; they land in the wall's Editable Sources so they bake with it.
// GC: nothing at runtime, no ticks or subscriptions. Generating allocates working lists and objects in the editor only.
public class Level_EgyptPillar : Level_Tool
{
    [Header("Size")]
    [Tooltip("Total height, metres, measured along the pillar (so it stays this long when rolled).")]
    [SerializeField, Min(1f)] private float height = 5f;
    [Tooltip("Width of the base and capital, metres. The shaft is narrower.")]
    [SerializeField, Min(0.4f)] private float width = 1.6f;

    [Header("Trim")]
    [Tooltip("Height of the base block, metres.")]
    [SerializeField, Min(0.1f)] private float baseHeight = 0.35f;
    [Tooltip("Height of the capital on top, metres.")]
    [SerializeField, Min(0.1f)] private float capitalHeight = 0.35f;
    [Tooltip("Height of the coloured band under the capital, metres.")]
    [SerializeField, Min(0.05f)] private float bandHeight = 0.12f;

    [Header("Rotation")]
    [Tooltip("Spin around the vertical axis, degrees. Turns which way the square faces.")]
    [SerializeField, Range(-180f, 180f)] private float yaw;
    [Tooltip("Tip around the pillar's own forward axis, from its base, degrees. 0 is upright; 90 lies it on its side.")]
    [SerializeField, Range(-90f, 90f)] private float roll;

    public float Height => height;
    public float Width => width;
    public float Yaw => yaw;
    public float Roll => roll;

    // The pillar's full orientation in the tool's space: spin first, then roll about the spun forward axis
    public Quaternion Rotation => Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, roll);

    protected override void BuildPieces(List<Level_ToolPiece> pieces)
    {
        float shaft = height - baseHeight - capitalHeight - bandHeight;

        if (shaft < 0.2f)
            throw new InvalidOperationException("The pillar is too short for its base, band and capital. Make it taller or the trim smaller.");

        Quaternion rotation = Rotation;
        float shaftWidth = width * 0.7f;
        float bandWidth = width * 0.9f;

        // Offsets are along the pillar's own up, so the whole thing turns and tips about its base centre
        pieces.Add(new Level_ToolPiece("Base", Vector3.zero, rotation, new Vector3(width, baseHeight, width), "Trim", "Pillar Base"));
        pieces.Add(new Level_ToolPiece("Shaft", rotation * Vector3.up * baseHeight, rotation,
            new Vector3(shaftWidth, shaft, shaftWidth), "Main", "Pillar Shaft"));
        pieces.Add(new Level_ToolPiece("Band", rotation * Vector3.up * (baseHeight + shaft), rotation,
            new Vector3(bandWidth, bandHeight, bandWidth), "Accent", "Pillar Band"));
        pieces.Add(new Level_ToolPiece("Capital", rotation * Vector3.up * (height - capitalHeight), rotation,
            new Vector3(width, capitalHeight, width), "Trim", "Pillar Capital"));
    }

#if UNITY_EDITOR
    // A wall this pillar stands in cuts itself around it, so it has to regenerate when the pillar changes
    public override bool Generate()
    {
        bool made = base.Generate();
        NotifyWall();
        return made;
    }

    public void NotifyWall()
    {
        Level_EgyptWall wall = transform.parent != null ? transform.parent.GetComponentInParent<Level_EgyptWall>(true) : null;

        if (wall != null)
            wall.RequestRefresh();
    }

    // For tools that place a pillar: size and facing in one Undo step
    public void Setup(float newHeight, float newWidth, float newYaw)
    {
        Undo.RecordObject(this, "Set Pillar");
        height = Mathf.Max(1f, newHeight);
        width = Mathf.Max(0.4f, newWidth);
        yaw = Mathf.Repeat(newYaw + 180f, 360f) - 180f;
    }
#endif
}
