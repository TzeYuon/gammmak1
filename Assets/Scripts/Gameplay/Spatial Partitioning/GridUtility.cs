using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasLineOfSight(
        float2 start, 
        float2 end,
        NativeArray<int> obstacleGrid,
        int2 worldOrigin,
        int width,
        int height)
    {
        float dist = math.distance(start, end);

        int steps = (int)math.ceil(dist / 0.5f);

        if (steps <= 0) return true;

        float2 stepVector = (end - start) / steps;
        float2 currentPos = start;

        for (int i = 0; i < steps; i++)
        {
            if (GetTerrainAtWorldPos(currentPos, obstacleGrid, worldOrigin, width, height) == 1)
            {
                return false;
            }

            currentPos += stepVector;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidGridManagerWorldPos(float2 pos, int2 worldOrigin, int width, int height) {
        var gridPos = WorldPosToGridManagerPos(pos, worldOrigin);

        if (gridPos.x < 0 || gridPos.x >= width ||
            gridPos.y < 0 || gridPos.y >= height)
            return false;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 WorldPosToGridManagerPos(float2 pos, int2 worldOrigin)
    {
        int gridX = (int)math.round(pos.x) - worldOrigin.x;
        int gridY = (int)math.round(pos.y) - worldOrigin.y;
        return new int2(gridX, gridY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WorldPosToGridManagerIndex(float2 pos, int2 worldOrigin, int width)
    {
        var gridPos = WorldPosToGridManagerPos(pos, worldOrigin);
        return gridPos.x + gridPos.y * width;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetTerrainAtWorldPos(float2 pos, NativeArray<int> obstacleGrid, int2 worldOrigin, int width, int height)
    {
        if (!IsValidGridManagerWorldPos(pos, worldOrigin, width, height)) return -1;
        return obstacleGrid[WorldPosToGridManagerIndex(pos, worldOrigin, width)];
    }
}