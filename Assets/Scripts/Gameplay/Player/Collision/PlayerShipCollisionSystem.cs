using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(CollisionAndDamageGroup))]
partial struct PlayerShipCollisionSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        FineEnemyHeatmapComponent fineMap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<HealthComponent> healthLookup = SystemAPI.GetComponentLookup<HealthComponent>(true);
        ComponentLookup<SwarmDamageComponent> swarmDamageLookup = SystemAPI.GetComponentLookup<SwarmDamageComponent>(true);
        ComponentLookup<CollisionRadius> collisionRadiusLookup = SystemAPI.GetComponentLookup<CollisionRadius>(true);

        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var job = new ContactDamageJob
        {
            Map = fineMap.Map,
            TransformLookup = transformLookup,
            HealthLookup = healthLookup,
            SwarmDamageLookup = swarmDamageLookup,
            CollisionRadiusLookup = collisionRadiusLookup,
            ECB = ecb,
            WorldOrigin = fineMap.WorldOrigin,
            CellSize = fineMap.CellSize,
            GridWidth = fineMap.GridWidth,
            GridHeight = fineMap.GridHeight,
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}

[BurstCompile]
[WithAll(typeof(PlayerTag))]
[WithAll(typeof(HealthComponent))]
[WithDisabled(typeof(DeadTag))]
public partial struct ContactDamageJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> Map;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<HealthComponent> HealthLookup;
    [ReadOnly] public ComponentLookup<SwarmDamageComponent> SwarmDamageLookup;
    [ReadOnly] public ComponentLookup<CollisionRadius> CollisionRadiusLookup;

    public EntityCommandBuffer.ParallelWriter ECB;

    public int2 WorldOrigin;
    public float CellSize;
    public int GridWidth;
    public int GridHeight;

    void Execute(
        Entity entity,
        [ChunkIndexInQuery] int chunkIndex,
        in LocalTransform transform,
        in HitboxCombined combinedRadius,
        in DynamicBuffer<Hitbox> hitboxes
        )
    {
        float2 pos = transform.Position.xy;
        int2 cell = GridUtility.WorldPosToCell(pos, WorldOrigin, CellSize);
        float maxRadius = math.sqrt(combinedRadius.MaxBoundingRadiusSq);
        float maxExpectedEnemyRadius = 2.0f; // Hardcoded radius
        int radiusCells = (int)math.ceil((maxRadius + maxExpectedEnemyRadius) / CellSize);

        quaternion inverseShipRot = math.inverse(transform.Rotation);

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
                        if (!SwarmDamageLookup.HasComponent(enemyEntity)) continue;

                        RefRO<LocalTransform> t = TransformLookup.GetRefRO(enemyEntity);
                        float2 ePos = t.ValueRO.Position.xy;
                        float distSq = math.distancesq(pos, ePos);

                        if (distSq > combinedRadius.MaxBoundingRadiusSq) continue;
                        float enemyRadius = CollisionRadiusLookup.HasComponent(enemyEntity)
                            ? CollisionRadiusLookup[enemyEntity].Value
                            : 0f;

                        float combinedOuterRadius = maxRadius + enemyRadius;
                        if (distSq > combinedOuterRadius * combinedOuterRadius) continue;

                        if (!HealthLookup.HasComponent(enemyEntity)) continue;

                        float3 offset3D = new(ePos - pos, 0);
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
                                float combinedRadiusLimit = hb.Size.x + enemyRadius;
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

                                if ((distX * distX + distY * distY) <= enemyRadius * enemyRadius)
                                {
                                    isHit = true;
                                    break;
                                }
                            }
                            else if (hb.Shape == HitboxShape.Triangle)
                            {
                                float expandedHalfBase = hb.Size.x + enemyRadius;
                                float expandedHeight = hb.Size.y + enemyRadius;

                                if (finalPos.y >= -enemyRadius && finalPos.y <= expandedHeight)
                                {
                                    float tRatio = (finalPos.y + enemyRadius) / (expandedHeight + enemyRadius);
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
                            var enemyHealth = HealthLookup[enemyEntity];
                            if (enemyHealth.Value <= 0) continue;

                            var damage = SwarmDamageLookup[enemyEntity].Damage;

                            ECB.AppendToBuffer(chunkIndex, entity, new DamageEvent { Value = damage });
                            ECB.AppendToBuffer(chunkIndex, enemyEntity, new DamageEvent { Value = damage });
                        }
                    } while (Map.TryGetNextValue(out enemyEntity, ref iterator));
                }
            }
        }
    }
}