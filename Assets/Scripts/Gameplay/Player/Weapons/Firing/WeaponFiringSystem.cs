using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(CombatSystemGroup))]
public partial struct WeaponFiringSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
        state.RequireForUpdate<WeaponState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();
        float deltaTime = SystemAPI.Time.DeltaTime;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        FineEnemyHeatmapComponent fineEntityMap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        NativeParallelMultiHashMap<int, Entity> map = fineEntityMap.Map;

        var vfxWriter = SystemAPI.GetSingleton<GlobalVFXQueue>().Queue.AsParallelWriter();

        //foreach (var (weaponState, transform, parent) in SystemAPI.Query<RefRW<WeaponState>, RefRO<LocalToWorld>, RefRO<Parent>>().WithAll<PrecisionTargetingComponent>())
        //{
        //    weaponState.ValueRW.FireTimer -= SystemAPI.Time.DeltaTime;
        //    if (weaponState.ValueRO.FireTimer > 0) continue; // on cooldown
        //    if (!weaponState.ValueRO.HasTarget) continue; // no target
        //    if (weaponState.ValueRO.FireRate == 0) continue; // invalid fire rate
        //    weaponState.ValueRW.FireTimer = 1 / weaponState.ValueRO.FireRate;

        //    // Calculate positions for laser vfx
        //    float3 startPos = transform.ValueRO.Position;
        //    float3 endPos = new(weaponState.ValueRO.TargetPos.x, weaponState.ValueRO.TargetPos.y, startPos.z);

        //    // Use spatial map to find target hit
        //    int index = GridUtility.WorldPosToIndex(weaponState.ValueRO.TargetPos, fineEntityMap.CellSize, fineEntityMap.GridWidth);
        //    if (map.TryGetFirstValue(index, out Entity e, out _))
        //    {
        //        if (transformLookup.HasComponent(e)) endPos = transformLookup[e].Position;

        //        // Get health component and subtract health
        //        if (healthLookup.HasComponent(e))
        //        {
        //            RefRW<HealthComponent> health = healthLookup.GetRefRW(e);
        //            if (health.ValueRO.Value > 0) health.ValueRW.Value = 0;
        //        }    
        //    }

        //    float3 direction = endPos - startPos;
        //    float distance = math.length(direction);
        //    float3 midpoint = startPos + (direction * 0.5f);

        //    float angleDegrees = math.degrees(math.atan2(direction.y, direction.x));

        //    vfxWriter.Enqueue(new VFXPayload
        //    {
        //        Position = midpoint,
        //        Type = (uint)VFXType.Laser,
        //        Colour = new float4(0.0f, 1.0f, 1.0f, 3.0f),
        //        CustomData = new float4(angleDegrees, distance, 0, 0),
        //    });
        //}

        JobHandle hitscan = new HitscanFiringJob
        {
            DeltaTime = deltaTime,
            ECB = ecb,
            VFXWriter = vfxWriter,
            FineHeatmap = map,
            CellSize = fineEntityMap.CellSize,
            GridWidth = fineEntityMap.GridWidth,
        }.ScheduleParallel(state.Dependency);

        JobHandle projectile = new ProjectileFiringJob
        {
            DeltaTime = deltaTime,
            ECB = ecb,
        }.ScheduleParallel(hitscan);

        state.Dependency = projectile;
        state.CompleteDependency();
    }
}

[BurstCompile]
public partial struct HitscanFiringJob : IJobEntity
{
    public float DeltaTime;
    public EntityCommandBuffer.ParallelWriter ECB;

    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> FineHeatmap;
    public float CellSize;
    public int GridWidth;

    public NativeQueue<VFXPayload>.ParallelWriter VFXWriter;

    private void Execute(
        [ChunkIndexInQuery] int chunkIndex,
        ref WeaponState state,
        in HitscanWeapon hitscanWeapon,
        in LocalToWorld localToWorld
        )
    {
        state.FireTimer -= DeltaTime;
        if (state.FireTimer > 0 || !state.HasTarget || state.FireRate == 0) return;
        state.FireTimer = 1 / state.FireRate;

        float3 barrelForward = localToWorld.Up;
        float3 dirToTarget = math.normalize(new float3(state.TargetPos, localToWorld.Position.z) - localToWorld.Position);

        if (math.dot(barrelForward, dirToTarget) < 0.98f) return; // mostly facing target

        int index = GridUtility.WorldPosToIndex(state.TargetPos, CellSize, GridWidth);
        if (FineHeatmap.TryGetFirstValue(index, out Entity enemy, out var it))
        {
            do
            {
                ECB.AppendToBuffer(chunkIndex, enemy, new DamageEvent { Value = hitscanWeapon.Damage });
            } while (FineHeatmap.TryGetNextValue(out enemy, ref it));
        }

        float3 startPos = localToWorld.Position;
        float3 dir = new float3(state.TargetPos, localToWorld.Position.z) - localToWorld.Position;
        float distance = math.length(dir);
        float3 midpoint = startPos + (dir * 0.5f);

        float angleDegrees = math.degrees(math.atan2(dir.y, dir.x));

        VFXWriter.Enqueue(new VFXPayload
        {
            Position = midpoint,
            Type = (uint)VFXType.Laser,
            Colour = new float4(0.0f, 1.0f, 1.0f, 3.0f),
            CustomData = new float4(angleDegrees, distance, 0, 0),
        });
    }
}

[BurstCompile]
public partial struct ProjectileFiringJob : IJobEntity
{
    public float DeltaTime;
    public EntityCommandBuffer.ParallelWriter ECB;

    private void Execute(
        [ChunkIndexInQuery] int chunkIndex,
        ref WeaponState state,
        in ProjectileWeapon projectileWeapon,
        in LocalToWorld localToWorld
        )
    {
        state.FireTimer -= DeltaTime;
        if (state.FireTimer > 0 || !state.HasTarget || state.FireRate == 0) return;
        state.FireTimer = 1 / state.FireRate;

        float3 weaponForward = localToWorld.Up;
        float3 dirToTarget = math.normalize(new float3(state.TargetPos, localToWorld.Position.z) - localToWorld.Position);

        if (math.dot(weaponForward, dirToTarget) < 0.95f) return; // mostly facing target

        Entity projectile = ECB.Instantiate(chunkIndex, projectileWeapon.ProjectilePrefab);

        ECB.SetComponent(chunkIndex, projectile, LocalTransform.FromPositionRotation(
            localToWorld.Position,
            localToWorld.Rotation)
        );

        ECB.SetComponent(chunkIndex, projectile, new ProjectileMovement
        {
            Velocity = weaponForward.xy * projectileWeapon.ProjectileSpeed,
            HitRadiusSq = 1,
        });

        ECB.SetComponent(chunkIndex, projectile, new StandardProjectile
        {
            Damage = projectileWeapon.Damage,
        });
    }
}