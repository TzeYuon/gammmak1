#if UNITY_EDITOR 
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class HeatmapVisualiser : MonoBehaviour
{
    // How many bugs it takes for a cell to be considered "Max Density" (Pure Red)
    public float MaxDensityColorThreshold = 10f;

    // We only want to draw this when the game is actually running and the ECS world exists
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 1. Get the default ECS World
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var entityManager = world.EntityManager;

        // 2. Find the Singleton Entity that holds our Heatmap data
        var query = entityManager.CreateEntityQuery(typeof(CoarseEnemyHeatmapComponent));
        if (query.IsEmptyIgnoreFilter) return;
        query.CompleteDependency();
        var entity = query.GetSingletonEntity();

        // 3. Read the data
        var coarseHeatmap = entityManager.GetComponentData<CoarseEnemyHeatmapComponent>(entity);
        var arr = coarseHeatmap.Map;

        // 4. Draw the Grid
        // A 100x100 grid is 10,000 cells. Drawing 10,000 empty cubes will lag the Unity Editor.
        // So, we ONLY draw cells that actually contain bugs!

        float halfCell = coarseHeatmap.CellSize * 0.5f;
        Vector3 size = new Vector3(coarseHeatmap.CellSize, coarseHeatmap.CellSize, 0.1f); // Adjust Z for 2D/3D

        for (int x = 0; x < coarseHeatmap.GridWidth; x++)
        {
            for (int y = 0; y < coarseHeatmap.GridHeight; y++)
            {
                int flatIndex = x + (y * coarseHeatmap.GridWidth);
                int density = arr[flatIndex];

                if (density > 0)
                {
                    // Calculate World Position (Center of the cell)
                    float worldX = (x * coarseHeatmap.CellSize) + halfCell;
                    float worldY = (y * coarseHeatmap.CellSize) + halfCell;
                    Vector3 cellCenter = new Vector3(worldX, worldY, 0);

                    // Interpolate color: Blue (1 bug) to Red (Max bugs)
                    float colorLerp = math.clamp(density / MaxDensityColorThreshold, 0f, 1f);
                    Gizmos.color = Color.Lerp(Color.blue, Color.red, colorLerp);

                    // Draw a semi-transparent solid cube
                    Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.5f);
                    Gizmos.DrawCube(cellCenter, size);

                    // Draw a solid wireframe border so it looks like a grid
                    Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 1.0f);
                    Gizmos.DrawWireCube(cellCenter, size);
                }
            }
        }
    }
}
#endif