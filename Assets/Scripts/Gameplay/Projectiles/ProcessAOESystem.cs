using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(CollisionAndDamageGroup))]
partial struct ProcessAOESystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GlobalVFXQueue>();
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var fineHeatmap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        var vfxQueue = SystemAPI.GetSingletonRW<GlobalVFXQueue>().ValueRW.Queue.AsParallelWriter();

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var job = new ProcessExplosionsJob
        {
            SpatialMap = fineHeatmap.Map,
            TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            RadiusLookup = SystemAPI.GetComponentLookup<CollisionRadius>(true),
            ECB = ecb,
            VfxQueue = vfxQueue,
            WorldOrigin = fineHeatmap.WorldOrigin,
            CellSize = fineHeatmap.CellSize,
            GridWidth = fineHeatmap.GridWidth,
            GridHeight = fineHeatmap.GridHeight,
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
    [ReadOnly] public ComponentLookup<CollisionRadius> RadiusLookup;

    public NativeQueue<VFXPayload>.ParallelWriter VfxQueue;
    public EntityCommandBuffer.ParallelWriter ECB;
    public float CellSize;
    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;

    private void Execute(
        Entity entity, [ChunkIndexInQuery] int chunkIndex,
        in LocalTransform transform,
        in ExplosionEvent explosion)
    {
        NativeHashSet<Entity> uniqueHits = new NativeHashSet<Entity>(128, Allocator.Temp);

        float3 centerPos = transform.Position;
        int2 centerCell = GridUtility.WorldPosToCell(centerPos, WorldOrigin, CellSize);
        float radius = math.sqrt(explosion.RadiusSq);
        int radiusCells = (int)math.ceil(radius / CellSize);

        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                int2 checkCell = centerCell + new int2(x, y);
                if (!GridUtility.IsWithinBounds(checkCell, GridWidth, GridHeight)) continue;

                int index = GridUtility.CellToIndex(checkCell, GridWidth);
                if (SpatialMap.TryGetFirstValue(index, out Entity enemy, out var it))
                {
                    do
                    {
                        if (!TransformLookup.HasComponent(enemy)) continue;
                        if (!RadiusLookup.HasComponent(enemy)) continue;

                        // check distance from explosion center
                        float3 enemyPos = TransformLookup[enemy].Position;
                        float distSq = math.distancesq(centerPos, enemyPos);
                        float radiusEnemy = RadiusLookup[enemy].Value;
                        float maxReach = radius + radiusEnemy;

                        if (distSq <= maxReach * maxReach)
                        {
                            // only deal damage to unique enemies
                            if (uniqueHits.Add(enemy))
                            {
                                ECB.AppendToBuffer(chunkIndex, enemy, new DamageEvent { Value = explosion.Damage });
                            }
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

        uniqueHits.Dispose();
        ECB.DestroyEntity(chunkIndex, entity);
    }
}