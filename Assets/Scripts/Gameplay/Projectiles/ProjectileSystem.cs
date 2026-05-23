using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(MovementSystemGroup))]
partial struct ProjectileSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
        state.RequireForUpdate<ProjectileMovement>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var fineHeatmap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        float deltaTime = SystemAPI.Time.DeltaTime;

        state.Dependency = new StandardProjectileJob
        {
            DeltaTime = deltaTime,
            Map = fineHeatmap.Map,
            CellSize = fineHeatmap.CellSize,
            GridWidth = fineHeatmap.GridWidth,
            GridHeight = fineHeatmap.GridHeight,
            TransformLookup = transformLookup,
            ECB = ecb
        }.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithDisabled(typeof(DeadTag))]
public partial struct StandardProjectileJob : IJobEntity
{
    public float DeltaTime;

    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> Map;
    public float CellSize;
    public int GridWidth;
    public int GridHeight;

    [NativeDisableContainerSafetyRestriction] [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

    public EntityCommandBuffer.ParallelWriter ECB;

    void Execute(
        Entity entity,
        [ChunkIndexInQuery] int chunkIndex,
        ref LocalTransform transform,
        in ProjectileMovement projectileMovement,
        in StandardProjectile standardProjectile
    )
    {
        transform.Position = new float3(transform.Position.xy + projectileMovement.Velocity * DeltaTime, 0);

        float2 pos = transform.Position.xy;
        int2 cell = GridUtility.WorldPosToCell(transform.Position, CellSize);
        float radiusSq = projectileMovement.HitRadiusSq;
        int radiusCells = (int)math.ceil(math.sqrt(radiusSq) / CellSize);

        // Check cells within radius if projectile is hitting an enemy
        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                int2 curCell = cell + new int2(x, y);
                if (!GridUtility.IsWithinBounds(curCell, GridWidth, GridHeight)) continue;

                int index = GridUtility.CellToIndex(curCell, GridWidth);
                if (Map.TryGetFirstValue(index, out Entity enemyEntity, out var iterator))
                {
                    do
                    {
                        if (!TransformLookup.EntityExists(enemyEntity)) continue;
                        if (!TransformLookup.HasComponent(enemyEntity)) continue;

                        RefRO<LocalTransform> t = TransformLookup.GetRefRO(enemyEntity);
                        float2 ePos = t.ValueRO.Position.xy;
                        float distSq = math.distancesq(pos, ePos);

                        if (distSq > radiusSq) continue;

                        ECB.AppendToBuffer(chunkIndex, enemyEntity, new DamageEvent { Value = standardProjectile.Damage });
                        ECB.SetComponentEnabled<DeadTag>(chunkIndex, entity, true);
                        return;
                    } while (Map.TryGetNextValue(out enemyEntity, ref iterator));
                }
            }
        }
    }
}