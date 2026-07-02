using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class GridUtility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 RoundWorldPosToCorner(float2 worldPos, int2 worldOrigin, float cellSize)
    {
        return CellToWorldPosCorner(WorldPosToCell(worldPos, worldOrigin, cellSize), worldOrigin, cellSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 WorldPosToCell(float2 worldPos, int2 worldOrigin, float cellSize)
    {
        int gridX = (int)math.floor((worldPos.x - worldOrigin.x) / cellSize);
        int gridY = (int)math.floor((worldPos.y - worldOrigin.y) / cellSize);
        return new int2(gridX, gridY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 WorldPosToCell(float3 worldPos, int2 worldOrigin, float cellSize)
    {
        return WorldPosToCell(worldPos.xy, worldOrigin, cellSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WorldPosToIndex(float3 worldPos, int2 worldOrigin, float cellSize, int gridWidth)
    {
        return WorldPosToIndex(worldPos.xy, worldOrigin, cellSize, gridWidth);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WorldPosToIndex(float2 worldPos, int2 worldOrigin, float cellSize, int gridWidth)
    {
        int2 cell = WorldPosToCell(worldPos, worldOrigin, cellSize);
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
    public static float2 CellToWorldPosCenter(int2 cell, int2 worldOrigin, float cellSize)
    {
        return (new float2(cell.x, cell.y) * cellSize) + (cellSize * 0.5f) + worldOrigin;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 CellToWorldPosCorner(int2 cell, int2 worldOrigin, float cellSize)
    {
        return (new float2(cell.x, cell.y) * cellSize) + worldOrigin;
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
    public static bool IsWithinBounds(float2 checkWorldPos, int2 worldOrigin, float cellSize, int gridWidth, int gridHeight)
    {
        return IsWithinBounds(
            WorldPosToCell(checkWorldPos, worldOrigin, cellSize), 
            gridWidth, 
            gridHeight
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasLineOfSight(
        float2 start, 
        float2 end,
        NativeArray<int> obstacleGrid,
        int2 worldOrigin,
        int width,
        int height,
        float cellSize)
    {
        float dist = math.distance(start, end);
        int steps = (int)math.ceil(dist / (cellSize * 0.5f));

        if (steps <= 0) return true;

        float2 stepVector = (end - start) / steps;
        float2 currentPos = start;

        for (int i = 0; i < steps; i++)
        {
            if (!IsTerrainPassable(currentPos, obstacleGrid, worldOrigin, width, height, cellSize))
            {
                return false;
            }

            currentPos += stepVector;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 GetLastSafePointOnLine(
        float2 start,
        float2 end,
        NativeArray<int> obstacleGrid,
        int2 worldOrigin,
        int width,
        int height,
        float cellSize)
    {
        float dist = math.distance(start, end);
        int steps = (int)math.ceil(dist / (cellSize * 0.5f));

        if (steps <= 0) return start;

        float2 stepVector = (end - start) / steps;
        float2 currentPos = start;
        float2 lastSafePos = start;

        for (int i = 0; i < steps; i++)
        {
            if (!IsTerrainPassable(currentPos, obstacleGrid, worldOrigin, width, height, cellSize))
            {
                return RoundWorldPosToCorner(lastSafePos, worldOrigin, cellSize);
            }

            lastSafePos = currentPos;
            currentPos += stepVector;
        }

        return end;
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

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetTerrainAtWorldPos(float2 pos, NativeArray<int> obstacleGrid, int2 worldOrigin, int width, int height, float cellSize)
    {
        if (!IsWithinBounds(pos, worldOrigin, cellSize, width, height)) return -1;
        return obstacleGrid[WorldPosToIndex(pos, worldOrigin, cellSize, width)];
    }

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTerrainPassable(float2 pos, NativeArray<int> obstacleGrid, int2 worldOrigin, int width, int height, float cellSize)
    {
        int terrain = GetTerrainAtWorldPos(pos, obstacleGrid, worldOrigin, width, height, cellSize);
        return terrain != 1 && terrain != -1;
    }
}