using UnityEngine;

[CreateAssetMenu(menuName = "TD/Flow Field Costs")]
public class FlowFieldCosts : ScriptableObject
{
    public byte Default = 1;
    public byte NearWall = 5;
    public byte Impassable = 255;

    public const ushort Unreachable = ushort.MaxValue;
}