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

        ComponentLookup<TurretComponent> turretLookup = SystemAPI.GetComponentLookup<TurretComponent>(true);

        uint randomSeed = (uint)(SystemAPI.Time.ElapsedTime * 1000.0);

        JobHandle hitscan = new HitscanFiringJob
        {
            DeltaTime = deltaTime,
            ECB = ecb,
            VFXWriter = vfxWriter,
            FineHeatmap = map,
            CellSize = fineEntityMap.CellSize,
            GridWidth = fineEntityMap.GridWidth,
            TurretLookup = turretLookup,
        }.ScheduleParallel(state.Dependency);

        JobHandle projectile = new ProjectileFiringJob
        {
            DeltaTime = deltaTime,
            ECB = ecb,
            TurretLookup = turretLookup,
            RandomSeed = randomSeed,
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

    [ReadOnly] public ComponentLookup<TurretComponent> TurretLookup;

    private void Execute(
        Entity entity,
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

        if (!TurretLookup.HasComponent(entity) || math.dot(barrelForward, dirToTarget) < 0.98f) return; // mostly facing target

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

    [ReadOnly] public ComponentLookup<TurretComponent> TurretLookup;

    public uint RandomSeed;

    private void Execute(
        Entity entity,
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

        if (!TurretLookup.HasComponent(entity) || math.dot(weaponForward, dirToTarget) < 0.95f) return; // mostly facing target

        Entity projectile = ECB.Instantiate(chunkIndex, projectileWeapon.ProjectilePrefab);

        // Add spread
        var rng = Random.CreateFromIndex((uint)entity.Index * 197u + RandomSeed);
        float randomAngleOffset = math.radians(rng.NextFloat(-projectileWeapon.Spread, projectileWeapon.Spread));
        float baseAngle = math.atan2(dirToTarget.y, dirToTarget.x);
        float finalAngle = baseAngle + randomAngleOffset;
        float2 finalDirection = new(math.cos(finalAngle), math.sin(finalAngle));

        ECB.SetComponent(chunkIndex, projectile, LocalTransform.FromPositionRotation(
            localToWorld.Position,
            quaternion.RotateZ(finalAngle - (math.PI / 2f)))
        );

        ECB.SetComponent(chunkIndex, projectile, new ProjectileMovement
        {
            Velocity = finalDirection * projectileWeapon.ProjectileSpeed,
            HitRadiusSq = 1,
        });

        ECB.SetComponent(chunkIndex, projectile, new StandardProjectile
        {
            Damage = projectileWeapon.Damage,
        });
    }
}