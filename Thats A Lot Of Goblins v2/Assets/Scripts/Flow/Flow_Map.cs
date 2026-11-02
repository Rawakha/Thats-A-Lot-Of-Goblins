using UnityEngine;

// The baked obstacles for one level: a cost per cell of the grid, made in the editor by Flow_System's Bake Obstacles
// button and loaded once at runtime. 255 is blocked, 1 is open ground, in between is slower ground (next to walls,
// later mud and crops). Runtime changes (rubble, placed buildings) go in Flow_System's overlay, never in here.
[CreateAssetMenu(menuName = "Goblins/Flow/Flow Map", fileName = "Flow_Map")]
public class Flow_Map : ScriptableObject
{
    public const byte Blocked = 255;

    [ReadOnly] public Flow_Grid grid;
    [HideInInspector] public byte[] costs;

    [Header("Last Bake")]
    [ReadOnly] public int blockedCells;
    [ReadOnly] public float bakeMs;
    [ReadOnly] public string bakedAt;

    public bool IsBaked => costs != null && grid.CellCount > 0 && costs.Length == grid.CellCount;
}
