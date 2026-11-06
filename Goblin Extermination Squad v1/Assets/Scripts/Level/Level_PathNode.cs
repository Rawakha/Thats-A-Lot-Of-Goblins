using UnityEngine;

// Shared flat path controls. Serialized by value on each authoring component.
[System.Serializable]
public class Level_PathNode
{
    [Tooltip("Position in the tool's local XZ plane. Y is ignored; use the root Transform for floor height.")]
    public Vector3 position;
    [Tooltip("Multiplier of the overall wall height at this node.")]
    [Range(0.2f, 3f)] public float height = 1f;
    [Tooltip("Multiplier of the overall wall thickness at this node.")]
    [Range(0.3f, 3f)] public float thickness = 1f;
}
