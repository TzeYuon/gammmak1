using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(CollisionAndDamageGroup))]
partial struct ProcessAOESystem : ISystem
{
    EntityQuery fineHeatmapQuery;
    EntityQuery queueQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        fineHeatmapQuery = SystemAPI.QueryBuilder().WithAll<FineEnemyHeatmapComponent>().Build();
        queueQuery = SystemAPI.QueryBuilder().WithAll<GlobalVFXQueue>().Build();

        state.RequireForUpdate<GlobalVFXQueue>();
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        fineHeatmapQuery.CompleteDependency();
        var fineHeatmap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();

        queueQuery.CompleteDependency();
        var vfxQueue = SystemAPI.GetSingletonRW<GlobalVFXQueue>().ValueRW.Queue.AsParallelWriter();

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var job = new ProcessExplosionsJob
        {
            SpatialMap = fineHeatmap.Map,
            TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            ECB = ecb,
            VfxQueue = vfxQueue,
            WorldOrigin = fineHeatmap.WorldOrigin,
            CellSize = fineHeatmap.CellSize,
            GridWidth = fineHeatmap.GridWidth,
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

public struct ExplosionEvent : IComponentData
{
    public float Damage;
    public float RadiusSq;
    public float4 Colour;
}

[BurstCompile]
public partial struct ProcessExplosionsJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> SpatialMap;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

    public EntityCommandBuffer.ParallelWriter ECB;
    public NativeQueue<VFXPayload>.ParallelWriter VfxQueue;
    public int2 WorldOrigin;
    public float CellSize;
    public int GridWidth;

    private void Execute(
        Entity entity, [ChunkIndexInQuery] int chunkIndex,
        in LocalTransform transform,
        in ExplosionEvent explosion)
    {
        float3 centerPos = transform.Position;
        int radiusCells = (int)math.ceil(math.sqrt(explosion.RadiusSq) / CellSize);
        int2 centerCell = GridUtility.WorldPosToCell(centerPos, WorldOrigin, CellSize);

        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                int index = GridUtility.CellToIndex(centerCell + new int2(x, y), GridWidth);
                if (SpatialMap.TryGetFirstValue(index, out Entity enemy, out var it))
                {
                    do
                    {
                        if (!TransformLookup.HasComponent(enemy)) continue;

                        float distSq = math.distancesq(centerPos, TransformLookup[enemy].Position);
                        if (distSq <= explosion.RadiusSq)
                        {
                            ECB.AppendToBuffer(chunkIndex, enemy, new DamageEvent { Value = explosion.Damage });
                        }
                    } while (SpatialMap.TryGetNextValue(out enemy, ref it));
                }
            }
        }

        // Queue explosion vfx
        VfxQueue.Enqueue(new VFXPayload
        {
            Position = centerPos,
            Type = (uint)VFXType.Explosion,
            Colour = explosion.Colour,
            CustomData = new float4(math.sqrt(explosion.RadiusSq) * 2, 0, 0, 0)
        });

        // Destroy explosion entity
        ECB.DestroyEntity(chunkIndex, entity);
    }
}