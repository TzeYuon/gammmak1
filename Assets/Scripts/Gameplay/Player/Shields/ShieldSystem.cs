using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(CollisionAndDamageGroup))]
public partial struct ShieldSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
        state.RequireForUpdate<ShieldComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        FineEnemyHeatmapComponent fineMap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        ComponentLookup<CollisionRadius> collisionRadiusLookup = SystemAPI.GetComponentLookup<CollisionRadius>(true);

        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        // deal damage to enemies and reduce capacity
        state.Dependency = new ShieldJob
        {
            Map = fineMap.Map,
            TransformLookup = transformLookup,
            CollisionRadiusLookup = collisionRadiusLookup,
            ECB = ecb,
            DeltaTime = SystemAPI.Time.DeltaTime,
            WorldOrigin = fineMap.WorldOrigin,
            CellSize = fineMap.CellSize,
            GridWidth = fineMap.GridWidth,
            GridHeight = fineMap.GridHeight,
        }.ScheduleParallel(state.Dependency);

        // update opacity
        state.Dependency = new ShieldVisualJob().ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct ShieldJob : IJobEntity
{
    public float DeltaTime;

    public int2 WorldOrigin;
    public float CellSize;
    public int GridWidth;
    public int GridHeight;

    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> Map;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    [ReadOnly] public ComponentLookup<CollisionRadius> CollisionRadiusLookup;

    public EntityCommandBuffer.ParallelWriter ECB;

    private void Execute(
        Entity entity, [ChunkIndexInQuery] int chunkIndex,
        ref ShieldComponent shield,
        in LocalToWorld transform)
    {
        // Regen capacity
        if (shield.CurrentCapacity < shield.MaxCapacity)
        {
            shield.CurrentCapacity = math.min(shield.MaxCapacity, shield.CurrentCapacity + (shield.RegenPerSecond * DeltaTime));
        }

        if (shield.CurrentCapacity <= 0) return; // Shield inactive

        float3 centerPos = transform.Position;
        float maxExpectedEnemyRadius = 2.0f; // Hardcoded radius
        int2 centerCell = GridUtility.WorldPosToCell(centerPos, WorldOrigin, CellSize);
        float radius = shield.Radius;
        int radiusCells = (int)math.ceil((radius + maxExpectedEnemyRadius) / CellSize);

        float damageDealt = shield.DamagePerSecond * DeltaTime;
        float capacityCostPerEnemy = (damageDealt) * (1 - shield.BonusShieldEfficiency);
        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                int2 curCell = centerCell + new int2(x, y);
                if (!GridUtility.IsWithinBounds(curCell, GridWidth, GridHeight)) continue;

                int index = GridUtility.CellToIndex(curCell, GridWidth);
                if (Map.TryGetFirstValue(index, out Entity enemy, out var it))
                {
                    do
                    {
                        if (shield.CurrentCapacity <= 0) return; // Shield inactive

                        if (!TransformLookup.EntityExists(enemy)) continue;
                        if (!TransformLookup.HasComponent(enemy)) continue;

                        if (!CollisionRadiusLookup.HasComponent(enemy)) continue;

                        float distSq = math.distancesq(centerPos.xy, TransformLookup[enemy].Position.xy);
                        float rawEnemyRadius = CollisionRadiusLookup[enemy].Value;
                        float maxOverlapDistance = radius + rawEnemyRadius;
                        float maxOverlapDistanceSq = maxOverlapDistance * maxOverlapDistance;

                        if (distSq <= radius * radius)
                        {
                            // calculate how much capacity left
                            float fraction = capacityCostPerEnemy > 0 
                                ? math.min(1, shield.CurrentCapacity / capacityCostPerEnemy)
                                : 1;
                            float finalCapacityCost = fraction * capacityCostPerEnemy;
                            float finalDamage = fraction * damageDealt;

                            // apply damage
                            ECB.AppendToBuffer(chunkIndex, enemy, new DamageEvent { Value = finalDamage });

                            // drain capacity
                            shield.CurrentCapacity -= finalCapacityCost;
                        }

                    } while (Map.TryGetNextValue(out enemy, ref it));
                }
            }
        }
    }
}

[BurstCompile]
public partial struct ShieldVisualJob : IJobEntity
{
    private void Execute(
        in ShieldComponent shield,
        ref ShieldOpacityOverride opacity,
        ref LocalTransform transform)
    {
        float healthPercentage = shield.CurrentCapacity / shield.MaxCapacity;
        opacity.Value = shield.CurrentCapacity > 0 ? math.max(0.1f, healthPercentage) : 0f;
        transform.Scale = shield.Radius * 2;
    }
}