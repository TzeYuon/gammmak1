using JetBrains.Annotations;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(CollisionAndDamageGroup))]
partial struct ProcessAOESystem : ISystem
{
    private EntityQuery explosionTargetPlayerQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GlobalVFXQueue>();
        state.RequireForUpdate<FineEnemyHeatmapComponent>();

        explosionTargetPlayerQuery = SystemAPI.QueryBuilder().WithAll<ExplosionEventTargetPlayer>().Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var fineHeatmap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        var vfxQueue = SystemAPI.GetSingletonRW<GlobalVFXQueue>().ValueRW.Queue.AsParallelWriter();

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        // process explosions that hit enemies
        var targetSwarmJob = new ProcessExplosionsTargetSwarmJob
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
        state.Dependency = targetSwarmJob.ScheduleParallel(state.Dependency);

        // process explosions that hit players
        if (explosionTargetPlayerQuery.CalculateEntityCount() == 0) return;
        var explosionsTargetPlayer = explosionTargetPlayerQuery.ToComponentDataArray<ExplosionEventTargetPlayer>(Allocator.TempJob);
        var targetPlayerJob = new ProcessExplosionsTargetPlayerJob
        {
            Explosions = explosionsTargetPlayer,
            ECB = ecb,
        };
        state.Dependency = targetPlayerJob.ScheduleParallel(state.Dependency);

        // destroy and queue vfx for explosions that hit players
        var cleanUpTargetPlayerJob = new CleanupExplosionsTargerPlayerJob
        {
            VfxQueue = vfxQueue,
            ECB = ecb,
        };
        state.Dependency = cleanUpTargetPlayerJob.ScheduleParallel(state.Dependency);
    }
}

public struct ExplosionEventTargetSwarm : IComponentData
{
    public float3 Position;
    public float Damage;
    public float RadiusSq;
    public float4 Colour;
}

public struct ExplosionEventTargetPlayer : IComponentData
{
    public float3 Position;
    public float Damage;
    public float RadiusSq;
    public float4 Colour;
}

[BurstCompile]
public partial struct ProcessExplosionsTargetSwarmJob : IJobEntity
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
        Entity entity, 
        [ChunkIndexInQuery] int chunkIndex,
        in ExplosionEventTargetSwarm explosion)
    {
        NativeHashSet<Entity> uniqueHits = new NativeHashSet<Entity>(128, Allocator.Temp);

        float3 centerPos = explosion.Position;
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

[BurstCompile]
public partial struct ProcessExplosionsTargetPlayerJob : IJobEntity
{
    [ReadOnly] public NativeArray<ExplosionEventTargetPlayer> Explosions;

    public EntityCommandBuffer.ParallelWriter ECB;

    private void Execute(
        Entity entity,
        [ChunkIndexInQuery] int chunkIndex,
        in LocalTransform transform,
        in HitboxCombined combinedRadius,
        in DynamicBuffer<Hitbox> hitboxes)
    {
        float2 pos = transform.Position.xy;
        float maxRadius = math.sqrt(combinedRadius.MaxBoundingRadiusSq);

        quaternion inverseShipRot = math.inverse(transform.Rotation);
        for (int j = 0; j < Explosions.Length; j++)
        {
            var explosion = Explosions[j];

            // Check distance of explosion
            float2 explosionPos = explosion.Position.xy;
            float distSq = math.distancesq(pos, explosionPos);
            float explosionRadius = math.sqrt(explosion.RadiusSq);
            float combinedOuterRadius = maxRadius + explosionRadius;
            if (distSq > combinedOuterRadius * combinedOuterRadius) continue;

            float3 offset3D = new(explosionPos - pos, 0);
            float3 localOffset3D = math.rotate(inverseShipRot, offset3D);
            float2 shipLocalEnemyPos = localOffset3D.xy;

            bool isHit = false;

            // Loop through all hitboxes to determine if hit
            for (int i = 0; i < hitboxes.Length; i++)
            {
                var hb = hitboxes[i];
                float2 hbLocalPos = shipLocalEnemyPos - hb.LocalOffset;
                math.sincos(-hb.LocalRotation, out float sinH, out float cosH);

                float2 finalPos = new(
                    hbLocalPos.x * cosH - hbLocalPos.y * sinH,
                    hbLocalPos.x * sinH + hbLocalPos.y * cosH
                );

                if (hb.Shape == HitboxShape.Circle)
                {
                    float combinedRadiusLimit = hb.Size.x + explosionRadius;
                    if (math.lengthsq(finalPos) <= combinedRadiusLimit * combinedRadiusLimit)
                    {
                        isHit = true;
                        break;
                    }
                }
                else if (hb.Shape == HitboxShape.Rectangle)
                {
                    float closestX = math.clamp(finalPos.x, -hb.Size.x, hb.Size.x);
                    float closestY = math.clamp(finalPos.y, -hb.Size.y, hb.Size.y);

                    float distX = finalPos.x - closestX;
                    float distY = finalPos.y - closestY;

                    if ((distX * distX + distY * distY) <= explosionRadius * explosionRadius)
                    {
                        isHit = true;
                        break;
                    }
                }
                else if (hb.Shape == HitboxShape.Triangle)
                {
                    float expandedHalfBase = hb.Size.x + explosionRadius;
                    float expandedHeight = hb.Size.y + explosionRadius;

                    if (finalPos.y >= -explosionRadius && finalPos.y <= expandedHeight)
                    {
                        float tRatio = (finalPos.y + explosionRadius) / (expandedHeight + explosionRadius);
                        float maxWidthAtY = expandedHalfBase * (1.0f - tRatio);

                        if (math.abs(finalPos.x) <= maxWidthAtY)
                        {
                            isHit = true;
                            break;
                        }
                    }
                }
            }

            if (isHit)
            {
                ECB.AppendToBuffer(chunkIndex, entity, new DamageEvent { Value = explosion.Damage });
            }
        }
    }
}

[BurstCompile]
public partial struct CleanupExplosionsTargerPlayerJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;
    public NativeQueue<VFXPayload>.ParallelWriter VfxQueue;

    private void Execute(
        Entity entity, 
        [ChunkIndexInQuery] int chunkIndex,
        in ExplosionEventTargetPlayer explosion)
    {
        // Queue explosion vfx
        VfxQueue.Enqueue(new VFXPayload
        {
            Position = explosion.Position,
            Type = (uint)VFXType.Explosion,
            Colour = explosion.Colour,
            CustomData = new float4(math.sqrt(explosion.RadiusSq) * 2, 0, 0, 0)
        });

        ECB.DestroyEntity(chunkIndex, entity);
    }
}