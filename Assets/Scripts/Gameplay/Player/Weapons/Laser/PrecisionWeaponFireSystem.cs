using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(CombatSystemGroup))]
public partial class PrecisionWeaponFireSystem : SystemBase
{
    private NativeQueue<LaserEvent> laserQueue;

    [BurstCompile]
    protected override void OnCreate()
    {
        RequireForUpdate<FineEnemyHeatmapComponent>();
        RequireForUpdate<PrecisionTargetingComponent>();
        RequireForUpdate<HealthComponent>();

        laserQueue = new NativeQueue<LaserEvent>(Allocator.Persistent);
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        CompleteDependency();
        FineEnemyHeatmapComponent fineEntityMap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        NativeParallelMultiHashMap<int, Entity> map = fineEntityMap.Map;

        ComponentLookup<HealthComponent> healthLookup = SystemAPI.GetComponentLookup<HealthComponent>(false);
        ComponentLookup<LocalTransform> transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        foreach (var (weaponState, transform, parent) in SystemAPI.Query<RefRW<WeaponState>, RefRO<LocalToWorld>, RefRO<Parent>>().WithAll<PrecisionTargetingComponent>())
        {
            weaponState.ValueRW.FireTimer -= SystemAPI.Time.DeltaTime;
            if (weaponState.ValueRO.FireTimer > 0) continue; // on cooldown
            if (!weaponState.ValueRO.HasTarget) continue; // no target
            if (weaponState.ValueRO.FireRate == 0) continue; // invalid fire rate
            weaponState.ValueRW.FireTimer = 1 / weaponState.ValueRO.FireRate;

            // Calculate positions for laser vfx
            float3 startPos = transform.ValueRO.Position;
            float3 endPos = new(weaponState.ValueRO.TargetPos.x, weaponState.ValueRO.TargetPos.y, startPos.z);

            // Use spatial map to find target hit
            int index = GridUtility.WorldPosToIndex(weaponState.ValueRO.TargetPos, fineEntityMap.CellSize, fineEntityMap.GridWidth);
            if (map.TryGetFirstValue(index, out Entity e, out _))
            {
                if (transformLookup.HasComponent(e)) endPos = transformLookup[e].Position;

                // Get health component and subtract health
                if (healthLookup.HasComponent(e))
                {
                    RefRW<HealthComponent> health = healthLookup.GetRefRW(e);
                    if (health.ValueRO.Value > 0) health.ValueRW.Value = 0;
                }    
            }

            float3 direction = endPos - startPos;
            float distance = math.length(direction);
            float3 midpoint = startPos + (direction * 0.5f);

            float angleRadians = math.atan2(direction.y, direction.x);
            float angleDegrees = math.degrees(angleRadians);

            laserQueue.Enqueue(new LaserEvent
            {
                Midpoint = midpoint,
                Length = distance,
                AngleZ = angleDegrees,
                Color = new float4(0.0f, 1.0f, 1.0f, 3.0f)
            });
        }
    }

    [BurstCompile]
    protected override void OnDestroy()
    {
        if (laserQueue.IsCreated) laserQueue.Dispose();
    }

    public NativeQueue<LaserEvent> GetLaserQueue()
    {
        return laserQueue;
    }
}
