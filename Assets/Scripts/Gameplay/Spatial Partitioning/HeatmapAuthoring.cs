using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class HeatmapAuthoring : MonoBehaviour
{
    // Settings must allow the heatmap to cover the biggest map in the game

    public Vector2Int WorldCentre;
    /// <summary>
    /// In unity units
    /// </summary>
    public int MapWidth;
    /// <summary>
    /// In unity units
    /// </summary>
    public int MapHeight;
    public float CoarseCellSize;
    public int FineCellSizeMultiplier;

    class HeatmapAuthoringBaker : Baker<HeatmapAuthoring>
    {
        public override void Bake(HeatmapAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            int2 worldOrigin = (int2)math.round(new float2(
                authoring.WorldCentre.x - authoring.MapWidth / 2, 
                authoring.WorldCentre.y - authoring.MapHeight / 2));
            float coarseSize = authoring.CoarseCellSize;
            int coarseWidth = Mathf.FloorToInt(authoring.MapWidth / coarseSize);
            int coarseHeight = Mathf.FloorToInt(authoring.MapHeight / coarseSize);
            float fineSize = coarseSize / authoring.FineCellSizeMultiplier;
            int fineWidth = coarseWidth * authoring.FineCellSizeMultiplier;
            int fineHeight = coarseHeight * authoring.FineCellSizeMultiplier;
            AddComponent(entity, new EnemyHeatmapSettings
            {
                WorldOrigin = worldOrigin,
                CoarseCellSize = coarseSize,
                CoarseWidth = coarseWidth,
                CoarseHeight = coarseHeight,
                FineCellSize = fineSize,
                FineWidth = fineWidth,
                FineHeight = fineHeight,
            });
        }
    }
}

public partial struct EnemyHeatmapSettings : IComponentData
{
    public int2 WorldOrigin;
    public float CoarseCellSize;
    public int CoarseWidth;
    public int CoarseHeight;
    public float FineCellSize;
    public int FineWidth;
    public int FineHeight;
}