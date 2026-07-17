using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(ActionAndMovementGroup))]
public partial struct WeaponFiringSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
        state.RequireForUpdate<WeaponState>();
        state.RequireForUpdate<GlobalVFXQueue>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        double elapsedTime = SystemAPI.Time.ElapsedTime;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        FineEnemyHeatmapComponent fineEntityMap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        NativeParallelMultiHashMap<int, Entity> map = fineEntityMap.Map;

        var vfxWriter = SystemAPI.GetSingletonRW<GlobalVFXQueue>().ValueRW.Queue.AsParallelWriter();

        ComponentLookup<TurretComponent> turretLookup = SystemAPI.GetComponentLookup<TurretComponent>(true);

        uint randomSeed = (uint)(SystemAPI.Time.ElapsedTime * 1000.0);

        JobHandle hitscan = new HitscanFiringJob
        {
            DeltaTime = deltaTime,
            ECB = ecb,
            VFXWriter = vfxWriter,
            FineHeatmap = map,
            WorldOrigin = fineEntityMap.WorldOrigin,
            CellSize = fineEntityMap.CellSize,
            GridWidth = fineEntityMap.GridWidth,
            TurretLookup = turretLookup,
        }.ScheduleParallel(state.Dependency);

        JobHandle projectile = new ProjectileFiringJob
        {
            DeltaTime = deltaTime,
            ElapsedTime = elapsedTime,
            ECB = ecb,
            TurretLookup = turretLookup,
            RandomSeed = randomSeed,
            VFXWriter = vfxWriter,
        }.ScheduleParallel(hitscan);

        state.Dependency = projectile;
    }
}

/// <summary>
/// Hitscan weapons must have a specific entity target
/// </summary>
[BurstCompile]
public partial struct HitscanFiringJob : IJobEntity
{
    public float DeltaTime;
    public EntityCommandBuffer.ParallelWriter ECB;

    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> FineHeatmap;
    public int2 WorldOrigin;
    public float CellSize;
    public int GridWidth;

    public NativeQueue<VFXPayload>.ParallelWriter VFXWriter;

    [ReadOnly] public ComponentLookup<TurretComponent> TurretLookup;

    private void Execute(
        Entity entity,
        [ChunkIndexInQuery] int chunkIndex,
        ref WeaponState state,
        in HitscanWeapon weapon,
        in WeaponCombatStats stats,
        in LocalToWorld localToWorld
        )
    {
        state.FireTimer -= DeltaTime;
        if (state.FireTimer > 0 || !state.HasTarget || stats.FireRate == 0) return;
        state.FireTimer = 1 / stats.FireRate;

        float3 barrelForward = localToWorld.Up;
        float3 dirToTarget = math.normalize(new float3(state.TargetPos, localToWorld.Position.z) - localToWorld.Position);

        if (TurretLookup.HasComponent(entity) && math.dot(barrelForward, dirToTarget) < 0.98f) return; // mostly facing target

        // deal damage to specific target
        var enemy = state.TargetEntity;
        ECB.AppendToBuffer(chunkIndex, enemy, new DamageEvent { Value = stats.Damage });

        float3 startPos = localToWorld.Position;
        float3 dir = new float3(state.TargetPos, localToWorld.Position.z) - localToWorld.Position;
        float distance = math.length(dir);
        float3 midpoint = startPos + (dir * 0.5f);

        float angleRadians = math.atan2(dir.y, dir.x);
        float angleDegrees = math.degrees(angleRadians);
        quaternion rotation = quaternion.RotateZ(angleRadians - (math.PI / 2f));
        float3 spawnPoint = math.transform(localToWorld.Value, new float3(weapon.WeaponStartOffset, 0));

        // TEMPORARY: All weapons have the same vfx
        VFXWriter.Enqueue(new VFXPayload
        {
            Position = midpoint,
            Type = (uint)VFXType.Laser,
            Colour = weapon.VFXColour,
            CustomData = new float4(angleDegrees, distance, 0, 0),
        });

        VFXWriter.Enqueue(new VFXPayload
        {
            Position = spawnPoint,
            Type = (uint)VFXType.MuzzleFlash,
            Colour = weapon.VFXColour,
            CustomData = new float4(angleRadians - (math.PI / 2f), 0, 0, 0)
        });
    }
}

[BurstCompile]
public partial struct ProjectileFiringJob : IJobEntity
{
    public float DeltaTime;
    public double ElapsedTime;
    public EntityCommandBuffer.ParallelWriter ECB;

    [ReadOnly] public ComponentLookup<TurretComponent> TurretLookup;

    public uint RandomSeed;

    public NativeQueue<VFXPayload>.ParallelWriter VFXWriter;

    private void Execute(
        Entity entity,
        [ChunkIndexInQuery] int chunkIndex,
        ref WeaponState state,
        in ProjectileWeapon projectileWeapon,
        in WeaponCombatStats stats,
        in LocalToWorld localToWorld
        )
    {
        state.FireTimer -= DeltaTime;
        if (state.FireTimer > 0 || !state.HasTarget || stats.FireRate == 0) return;
        var rng = Random.CreateFromIndex((uint)entity.Index * 197u + RandomSeed);
        float baseCooldown = 1.0f / stats.FireRate;
        state.FireTimer = baseCooldown * rng.NextFloat(0.9f, 1.1f);

        float3 weaponForward = localToWorld.Up;
        float2 targetPos = state.TargetPos;
        float3 dirToTarget = math.normalize(new float3(targetPos, localToWorld.Position.z) - localToWorld.Position);

        if (TurretLookup.HasComponent(entity) && math.dot(weaponForward, dirToTarget) < 0.95f) return; // mostly facing target

        float baseAngle = math.atan2(weaponForward.y, weaponForward.x);
        float3 spawnPoint = math.transform(localToWorld.Value, new float3(projectileWeapon.WeaponStartOffset, 0));

        for (int i = 0; i < stats.ProjectileCount; i++) {
            Entity projectile = ECB.Instantiate(chunkIndex, projectileWeapon.ProjectilePrefab);

            // Add spread
            float randomAngleOffset = math.radians(rng.NextFloat(-stats.Spread, stats.Spread));
            float finalAngle = baseAngle + randomAngleOffset;
            quaternion rotation = quaternion.RotateZ(finalAngle - (math.PI / 2f));
            
            ECB.SetComponent(chunkIndex, projectile, LocalTransform.FromPositionRotation(
                spawnPoint,
                rotation)
            );

            ECB.SetComponent(chunkIndex, projectile, new ProjectileMovement
            {
                MovedThisFrame = 0
            });

            ECB.SetComponent(chunkIndex, projectile, new ProjectileMovementStats
            {
                Speed = stats.ProjectileSpeed,
                TurnSpeed = stats.ProjectileTurnSpeed,
                HomingRange = 20
            });

            ECB.SetComponent(chunkIndex, projectile, new ProjectileTriggerState
            {
                IsTriggered = false,
                RemainingHits = stats.PierceCount
            });

            ECB.SetComponent(chunkIndex, projectile, new ProjectileTriggerStats
            {
                TargetPos = targetPos,
                HitRadius = stats.HitRadius,
                TriggerTime = stats.TriggerTime,
            });

            ECB.SetComponent(chunkIndex, projectile, new ProjectilePayloadStats
            {
                Damage = stats.Damage,
                ExplosionRadiusSq = stats.ExplosionRadiusSq
            });

            ECB.SetComponent(chunkIndex, projectile, new ProjectileLifespan
            {
                ExpirationTime = ElapsedTime + stats.ProjectileLifetime
            });

            if (!stats.IgnoreObstacles)
            {
                ECB.AddComponent(chunkIndex, projectile, new CollideWithObstacles());
            } 
        }

        // TEMPORARY: All weapons have the same vfx
        VFXWriter.Enqueue(new VFXPayload
        {
            Position = spawnPoint,
            Type = (uint)VFXType.MuzzleFlash,
            Colour = projectileWeapon.VFXColour,
            CustomData = new float4(baseAngle - (math.PI / 2f), 0, 0, 0)
        });
    }
}