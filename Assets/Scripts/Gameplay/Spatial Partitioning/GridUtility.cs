using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class GridUtility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 RoundWorldPosToCenter(float2 worldPos, int2 worldOrigin, float cellSize)
    {
        return CellToWorldPosCenter(WorldPosToCell(worldPos, worldOrigin, cellSize), worldOrigin, cellSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 RoundWorldPosToCorner(float2 worldPos, int2 worldOrigin, float cellSize)
    {
        return CellToWorldPosCorner(WorldPosToCell(worldPos, worldOrigin, cellSize), worldOrigin, cellSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 WorldPosToCell(float2 worldPos, int2 worldOrigin, float cellSize)
    {
        return (int2)math.floor((worldPos - worldOrigin) / cellSize);
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
        return index >= 0 && index < (gridWidth * gridHeight);
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
    public static int GetTerrainAtWorldPos(float2 pos, NativeArray<int> obstacleGrid, int2 worldOrigin, int width, int height, float cellSize)
    {
        if (!IsWithinBounds(pos, worldOrigin, cellSize, width, height)) return -1;
        return obstacleGrid[WorldPosToIndex(pos, worldOrigin, cellSize, width)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetTerrainAtWorldPos(int2 cell, NativeArray<int> obstacleGrid, int width, int height)
    {
        if (!IsWithinBounds(cell, width, height)) return -1;
        return obstacleGrid[CellToIndex(cell, width)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetTerrainAtWorldPos(int index, NativeArray<int> obstacleGrid, int width, int height)
    {
        if (!IsWithinBounds(index, width, height)) return -1;
        return obstacleGrid[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTerrainPassable(float2 pos, NativeArray<int> obstacleGrid, int2 worldOrigin, int width, int height, float cellSize)
    {
        int terrain = GetTerrainAtWorldPos(pos, obstacleGrid, worldOrigin, width, height, cellSize);
        return terrain != 1 && terrain != -1;
    }

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTerrainPassable(int2 cell, NativeArray<int> obstacleGrid, int width, int height)
    {
        int terrain = GetTerrainAtWorldPos(cell, obstacleGrid, width, height);
        return terrain != 1 && terrain != -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTerrainPassable(int index, NativeArray<int> obstacleGrid, int width, int height)
    {
        int terrain = GetTerrainAtWorldPos(index, obstacleGrid, width, height);
        return terrain != 1 && terrain != -1;
    }
}