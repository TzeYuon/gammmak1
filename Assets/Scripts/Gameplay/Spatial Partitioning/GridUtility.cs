using System.Runtime.CompilerServices;
using Unity.Mathematics;

public static class GridUtility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 WorldPosToCell(float2 worldPos, float cellSize)
    {
        return (int2)math.floor(worldPos / cellSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 WorldPosToCell(float3 worldPos, float cellSize)
    {
        return (int2)math.floor(worldPos.xy / cellSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WorldPosToIndex(float3 worldPos, float cellSize, int gridWidth)
    {
        int2 cell = (int2)math.floor(worldPos.xy / cellSize);
        return cell.x + (cell.y * gridWidth);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WorldPosToIndex(float2 worldPos, float cellSize, int gridWidth)
    {
        int2 cell = (int2)math.floor(worldPos / cellSize);
        return cell.x + (cell.y * gridWidth);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 IndexToCell(int flatIndex, int gridWidth)
    {
        return new int2(flatIndex % gridWidth, flatIndex / gridWidth);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CellToIndex(int2 cell, int gridWidth)
    {
        return cell.x + (cell.y * gridWidth);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 CellToWorldPos(int2 cell, float cellSize)
    {
        return (new float2(cell.x, cell.y) * cellSize) + (cellSize * 0.5f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWithinBounds(int index, int gridWidth, int gridHeight)
    {
        int2 checkCell = IndexToCell(index, gridWidth);
        if (checkCell.x < 0 || checkCell.x >= gridWidth ||
            checkCell.y < 0 || checkCell.y >= gridHeight)
            return false;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWithinBounds(int2 checkCell, int gridWidth, int gridHeight)
    {
        if (checkCell.x < 0 || checkCell.x >= gridWidth ||
            checkCell.y < 0 || checkCell.y >= gridHeight)
            return false;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWithinBounds(float2 checkWorldPos, float cellSize, int gridWidth, int gridHeight)
    {
        return IsWithinBounds(WorldPosToCell(checkWorldPos, cellSize), gridWidth, gridHeight);
    }
}