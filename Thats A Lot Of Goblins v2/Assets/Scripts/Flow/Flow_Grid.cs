using System;
using Unity.Mathematics;
using UnityEngine;

// The flat world grid every flow field shares: square cells on the XZ plane from Origin, Width across x, Height
// across z. Cell index = y * Width + x, where y runs along world z.
// Plain data, so Burst jobs take it by value; the world helpers are main-thread conveniences.
[Serializable]
public struct Flow_Grid
{
    [Tooltip("World position of the grid's minimum corner. Only x and z matter.")]
    public Vector3 origin;
    [Min(0.1f)] public float cellSize;
    [Min(1)] public int width;
    [Min(1)] public int height;

    public int CellCount => width * height;
    public Vector3 Size => new Vector3(width * cellSize, 0f, height * cellSize);
    public Vector3 Centre => origin + Size * 0.5f;

    public bool Matches(in Flow_Grid other)
    {
        return width == other.width && height == other.height
            && Mathf.Approximately(cellSize, other.cellSize)
            && Mathf.Approximately(origin.x, other.origin.x) && Mathf.Approximately(origin.z, other.origin.z);
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

    // The cell containing a world point, or -1 outside the grid. Burst-safe.
    public int CellIndex(float3 world)
    {
        int x = (int)math.floor((world.x - origin.x) / cellSize);
        int y = (int)math.floor((world.z - origin.z) / cellSize);
        return InBounds(x, y) ? y * width + x : -1;
    }

    public int CellIndex(Vector3 world) => CellIndex((float3)world);

    public Vector2Int CellCoords(Vector3 world)
    {
        return new Vector2Int(
            Mathf.FloorToInt((world.x - origin.x) / cellSize),
            Mathf.FloorToInt((world.z - origin.z) / cellSize));
    }

    public Vector3 CellCentre(int x, int y)
    {
        return new Vector3(origin.x + (x + 0.5f) * cellSize, origin.y, origin.z + (y + 0.5f) * cellSize);
    }

    public Vector3 CellCentre(int index) => CellCentre(index % width, index / width);
}

// How a field's per-cell direction is stored: one byte, 0-7 for the eight neighbours going anticlockwise from +x
// (east), or one of the specials. Burst-safe.
public static class Flow_Direction
{
    public const byte None = 255;    // no way to the goal from here (walled in, or unreachable)
    public const byte Goal = 254;    // this cell is a goal

    private const float Diagonal = 0.70710678f;

    // Grid step for a direction: x along world x, y along world z
    public static void Offset(int direction, out int dx, out int dy)
    {
        switch (direction)
        {
            case 0: dx = 1; dy = 0; break;
            case 1: dx = 1; dy = 1; break;
            case 2: dx = 0; dy = 1; break;
            case 3: dx = -1; dy = 1; break;
            case 4: dx = -1; dy = 0; break;
            case 5: dx = -1; dy = -1; break;
            case 6: dx = 0; dy = -1; break;
            default: dx = 1; dy = -1; break;
        }
    }

    // Unit world direction on the XZ plane (x, z), or zero for None and Goal
    public static float2 Decode(byte direction)
    {
        switch (direction)
        {
            case 0: return new float2(1f, 0f);
            case 1: return new float2(Diagonal, Diagonal);
            case 2: return new float2(0f, 1f);
            case 3: return new float2(-Diagonal, Diagonal);
            case 4: return new float2(-1f, 0f);
            case 5: return new float2(-Diagonal, -Diagonal);
            case 6: return new float2(0f, -1f);
            case 7: return new float2(Diagonal, -Diagonal);
            default: return float2.zero;
        }
    }

    public static Vector3 ToWorld(byte direction)
    {
        float2 flat = Decode(direction);
        return new Vector3(flat.x, 0f, flat.y);
    }
}

// A field held by some system. Stays valid until released; a released slot's handle goes stale (version changes).
public readonly struct Flow_FieldHandle : IEquatable<Flow_FieldHandle>
{
    public readonly int Index;
    public readonly int Version;    // 0 is never valid

    public Flow_FieldHandle(int index, int version)
    {
        Index = index;
        Version = version;
    }

    public static Flow_FieldHandle Invalid => default;

    public bool Equals(Flow_FieldHandle other) => Index == other.Index && Version == other.Version;
    public override bool Equals(object obj) => obj is Flow_FieldHandle other && Equals(other);
    public override int GetHashCode() => (Index * 397) ^ Version;
}
