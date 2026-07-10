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
    public float CellSize;

    class HeatmapAuthoringBaker : Baker<HeatmapAuthoring>
    {
        public override void Bake(HeatmapAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            int2 worldOrigin = (int2)math.round(new float2(
                authoring.WorldCentre.x - authoring.MapWidth / 2, 
                authoring.WorldCentre.y - authoring.MapHeight / 2));
            float fineSize = authoring.CellSize;
            int fineWidth = Mathf.FloorToInt(authoring.MapWidth / fineSize);
            int fineHeight = Mathf.FloorToInt(authoring.MapHeight / fineSize);
            AddComponent(entity, new EnemyHeatmapSettings
            {
                WorldOrigin = worldOrigin,
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
    public float FineCellSize;
    public int FineWidth;
    public int FineHeight;
}