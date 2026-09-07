using UnityEngine;

[System.Serializable]
public struct Grid
{
    public Vector3 origin;
    public float cellSize;
    public int width;
    public int height;

    public int CellCount => width * height;

    public Vector3 WorldToSnapped(Vector3 world)
    {
        Vector2Int cell = WorldToCell(world);
        return CellCentre(cell);
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        int x = Mathf.FloorToInt((world.x - origin.x) / cellSize);
        int y = Mathf.FloorToInt((world.z - origin.z) / cellSize);
        return new Vector2Int(x, y);
    }

    public int ToIndex(Vector2Int cell)
    {
        return cell.y * width + cell.x;
    }

    public int ToIndex(Vector3 world)
    {
        return ToIndex(WorldToCell(world));
    }

    public Vector3 CellCentre(Vector2Int cell)
    {
        float x = origin.x + (cell.x + 0.5f) * cellSize;
        float z = origin.z + (cell.y + 0.5f) * cellSize;
        return new Vector3(x, origin.y, z);
    }

    public Vector3 CellCentre(int index)
    {
        return CellCentre(new Vector2Int(index % width, index / width));
    }

    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    public bool InBounds(Vector3 world)
    {
        return InBounds(WorldToCell(world));
    }

    public bool InBounds(int index)
    {
        return index >= 0 && index < CellCount;
    }
}