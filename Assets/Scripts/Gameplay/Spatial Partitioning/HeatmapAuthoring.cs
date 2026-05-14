using Unity.Entities;
using UnityEngine;

class HeatmapAuthoring : MonoBehaviour
{
    public int MapWidth;
    public int MapHeight;
    public float CoarseCellSize;
    public int FineCellSizeMultiplier;

    class HeatmapAuthoringBaker : Baker<HeatmapAuthoring>
    {
        public override void Bake(HeatmapAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            float coarseSize = authoring.CoarseCellSize;
            int coarseWidth = Mathf.FloorToInt(authoring.MapWidth / coarseSize);
            int coarseHeight = Mathf.FloorToInt(authoring.MapHeight / coarseSize);
            float fineSize = coarseSize / authoring.FineCellSizeMultiplier;
            int fineWidth = coarseWidth * authoring.FineCellSizeMultiplier;
            int fineHeight = coarseHeight * authoring.FineCellSizeMultiplier;
            AddComponent(entity, new EnemyHeatmapSettings
            {
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
    public float CoarseCellSize;
    public int CoarseWidth;
    public int CoarseHeight;
    public float FineCellSize;
    public int FineWidth;
    public int FineHeight;
}