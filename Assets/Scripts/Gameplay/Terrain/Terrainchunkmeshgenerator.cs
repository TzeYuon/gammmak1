using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds one merged mesh per (chunk, terrain type) pair using a direct,
/// per-square marching-squares triangulation - no smoothing pass, so the
/// result is angular and straight-edged rather than rounded/organic. That's
/// deliberate: it matches a sharp vector art style (diamonds, triangles)
/// better than smoothed cave contours would.
///
/// Convention: typeGrid[x, y] holds a terrain type id per cell. A "square"
/// sits between four adjacent cells: (x,y), (x+1,y), (x,y+1), (x+1,y+1).
/// Each cell is a marching-squares "control node" - active if its type
/// matches targetType. Because adjacent chunks read the same shared corner
/// cells, chunk meshes tile seamlessly with no gaps or double geometry.
///
/// Note: the two "saddle" configurations (only the two diagonal corners
/// active, e.g. bottom-left + top-right) are inherently ambiguous in
/// marching squares. This implementation always resolves them as one
/// connected hexagonal piece rather than splitting into two separate
/// triangles. That's a standard, harmless simplification for terrain.
/// </summary>
public static class TerrainChunkMeshGenerator
{
    // Boundary walk order for one square, counter-clockwise: BL, BR, TR, TL
    private static readonly float2[] CornerOffsets =
    {
        new float2(0f, 0f), // BL
        new float2(1f, 0f), // BR
        new float2(1f, 1f), // TR
        new float2(0f, 1f), // TL
    };

    private static readonly float2[] EdgeMidOffsets =
    {
        new float2(0.5f, 0f), // midpoint of BL -> BR
        new float2(1f, 0.5f), // midpoint of BR -> TR
        new float2(0.5f, 1f), // midpoint of TR -> TL
        new float2(0f, 0.5f), // midpoint of TL -> BL
    };

    /// <summary>
    /// Generates a mesh for one chunk's cells of the given type, or null if
    /// the chunk has no geometry for that type. Vertices are in LOCAL chunk
    /// space (0..chunkSize) - place the resulting entity's LocalTransform at
    /// (chunkOriginX, chunkOriginY) in world/grid space to position it.
    /// </summary>
    public static Mesh GenerateChunkMesh(
        int[,] typeGrid,
        int gridWidth,
        int gridHeight,
        int chunkOriginX,
        int chunkOriginY,
        int chunkSize,
        int targetType,
        int outOfBoundsType)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var scratchPoints = new List<float2>(8);

        for (int sx = chunkOriginX; sx < chunkOriginX + chunkSize; sx++)
        {
            for (int sy = chunkOriginY; sy < chunkOriginY + chunkSize; sy++)
            {
                bool bl = GetCellType(typeGrid, gridWidth, gridHeight, sx, sy, outOfBoundsType) == targetType;
                bool br = GetCellType(typeGrid, gridWidth, gridHeight, sx + 1, sy, outOfBoundsType) == targetType;
                bool tr = GetCellType(typeGrid, gridWidth, gridHeight, sx + 1, sy + 1, outOfBoundsType) == targetType;
                bool tl = GetCellType(typeGrid, gridWidth, gridHeight, sx, sy + 1, outOfBoundsType) == targetType;

                if (!bl && !br && !tr && !tl) continue; // fully inactive square - nothing to build here

                BuildSquarePolygon(bl, br, tr, tl, scratchPoints);
                if (scratchPoints.Count < 3) continue;

                float2 localOrigin = new float2(sx - chunkOriginX, sy - chunkOriginY);
                int baseIndex = vertices.Count;

                for (int i = 0; i < scratchPoints.Count; i++)
                {
                    float2 p = localOrigin + scratchPoints[i];
                    vertices.Add(new Vector3(p.x, p.y, 0f));
                }

                // Fan triangulation from point 0. Valid here because the
                // boundary walk always produces a simple (convex, in every
                // case except the resolved saddle case above) polygon.
                for (int i = 1; i < scratchPoints.Count - 1; i++)
                {
                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + i);
                    triangles.Add(baseIndex + i + 1);
                }
            }
        }

        if (vertices.Count == 0) return null;

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;

        // If the chunk renders invisible, your camera is looking at the
        // mesh from the other side - either flip the two outer indices in
        // each triangle below, or set "Cull Off" on the material while you
        // sanity-check orientation.
    }

    /// <summary>
    /// Cheap pre-check so callers can skip full generation (and the entity
    /// spawn that would follow it) for chunks with nothing of this type.
    /// </summary>
    public static bool ChunkContainsType(
        int[,] typeGrid, int gridWidth, int gridHeight,
        int chunkOriginX, int chunkOriginY, int chunkSize, int targetType)
    {
        int maxX = math.min(chunkOriginX + chunkSize, gridWidth);
        int maxY = math.min(chunkOriginY + chunkSize, gridHeight);
        for (int x = chunkOriginX; x < maxX; x++)
            for (int y = chunkOriginY; y < maxY; y++)
                if (typeGrid[x, y] == targetType) return true;
        return false;
    }

    private static int GetCellType(int[,] typeGrid, int gridWidth, int gridHeight, int x, int y, int outOfBoundsType)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return outOfBoundsType;
        return typeGrid[x, y];
    }

    // Walks the square's boundary in a fixed cyclic order. Keeps a corner
    // point if that corner is active, and the edge midpoint between two
    // corners if exactly one of them is active (i.e. the boundary crosses
    // that edge). The resulting point list, taken in order, is the polygon
    // of the "active" region inside this square.
    private static void BuildSquarePolygon(bool bl, bool br, bool tr, bool tl, List<float2> result)
    {
        result.Clear();
        bool[] active = { bl, br, tr, tl };

        for (int k = 0; k < 4; k++)
        {
            int next = (k + 1) % 4;
            if (active[k]) result.Add(CornerOffsets[k]);
            if (active[k] != active[next]) result.Add(EdgeMidOffsets[k]);
        }
    }
}