using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(MovementSystemGroup))]
partial struct ProjectileSystem : ISystem
{
    private ComponentLookup<LocalTransform> transformLookup;
    private ComponentLookup<HealthComponent> healthLookup;

    public void OnCreate(ref SystemState state)
    {
        transformLookup = state.GetComponentLookup<LocalTransform>(true);
        healthLookup = state.GetComponentLookup<HealthComponent>(false);
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        transformLookup.Update(ref state);
        healthLookup.Update(ref state);

        var fineHeatmap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        float deltaTime = SystemAPI.Time.DeltaTime;
        double elapsedTime = SystemAPI.Time.ElapsedTime;

        // Movement
        var linearHandle = new LinearFlightJob { DeltaTime = deltaTime }.ScheduleParallel(state.Dependency);

        var homingHandle = new HomingFlightJob 
        { 
            DeltaTime = deltaTime,
            Map = fineHeatmap.DensityMap,
            GridWidth = fineHeatmap.GridWidth,
            GridHeight = fineHeatmap.GridHeight,
            CellSize = fineHeatmap.CellSize,
        }.ScheduleParallel(linearHandle);

        // Trigger
        var timerHandle = new TimerTriggerJob { ElapsedTime = elapsedTime }.ScheduleParallel(homingHandle);

        var coordHandle = new CoordinateTriggerJob().ScheduleParallel(homingHandle);

        var contactHandle = new ContactTriggerJob
        {
            Map = fineHeatmap.Map,
            TransformLookup = transformLookup,
            CellSize = fineHeatmap.CellSize,
            GridWidth = fineHeatmap.GridWidth,
        }.ScheduleParallel(homingHandle);

        var triggerhandle = JobHandle.CombineDependencies(timerHandle, JobHandle.CombineDependencies(coordHandle, contactHandle));

        // Payload
        var kineticHandle = new KineticPayloadJob
        {
            ECB = ecb,
        }.ScheduleParallel(triggerhandle);

        var explosiveHandle = new ExplosivePayloadJob
        {
            ECB = ecb
        }.ScheduleParallel(triggerhandle);
        var combinedPayloadHandle = JobHandle.CombineDependencies(kineticHandle, explosiveHandle);

        combinedPayloadHandle = new PiercingPayloadJob
        {
            ECB = ecb,
        }.ScheduleParallel(combinedPayloadHandle);

        // Lifespan
        var lifespanHandle = new ProjectileLifespanJob
        {
            ElapsedTime = elapsedTime,
            ECB = ecb,
        }.ScheduleParallel(combinedPayloadHandle);

        state.Dependency = lifespanHandle;
    }
}

[BurstCompile]
[WithAll(typeof(LinearFlight))]
public partial struct LinearFlightJob : IJobEntity
{
    public float DeltaTime;

    private void Execute(
        ref LocalTransform transform, 
        in ProjectileMovementStats stats)
    {
        transform.Position += new float3(DeltaTime * stats.Speed * transform.Up().xy, 0);
    }
}

[BurstCompile]
public partial struct HomingFlightJob : IJobEntity
{
    public float DeltaTime;
    [ReadOnly] public NativeArray<int> Map;
    public int GridWidth;
    public int GridHeight;
    public float CellSize;

    private void Execute(
        ref LocalTransform transform,
        in ProjectileMovementStats stats,
        in HomingFlight homing
        )
    {
        float2 currentPos = transform.Position.xy;
        float2 currentHeading = transform.Up().xy;

        int radiusCells = (int)math.ceil(stats.HomingRange / CellSize);
        int2 centerCell = GridUtility.WorldPosToCell(currentPos, CellSize);

        float bestScore = -1f;
        float2 bestTargetPos = currentPos + currentHeading * stats.Speed;

        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                int2 checkCell = centerCell + new int2(x, y);

                // Bounds check
                if (!GridUtility.IsWithinBounds(checkCell, GridWidth, GridHeight)) continue;

                int density = Map[GridUtility.CellToIndex(checkCell, GridWidth)];
                if (density <= 0) continue;

                float2 cellWorldPos = GridUtility.CellToWorldPos(checkCell, CellSize);
                float2 vectorToCell = cellWorldPos - currentPos;
                float dist = math.length(vectorToCell);

                if (dist > stats.HomingRange || dist < 0.1f) continue;
                float2 dirToCell = vectorToCell / dist;

                // Distance bias
                float distWeight = 1.0f - (dist / stats.HomingRange);
                distWeight = math.pow(distWeight, homing.DistanceBias);

                // Alignment bias
                float dot = math.dot(currentHeading, dirToCell);
                float angleWeight = (dot + 1.0f) * 0.5f;
                angleWeight = math.pow(angleWeight, homing.AngleBias);

                float score = density * distWeight * angleWeight;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTargetPos = cellWorldPos;
                }
            }
        }

        // Rotate projectile towards target
        float2 desiredDir = math.normalize(bestTargetPos - currentPos);
        float currentAngle = math.atan2(currentHeading.y, currentHeading.x);
        float targetAngle = math.atan2(desiredDir.y, desiredDir.x);
        float angleDiff = targetAngle - currentAngle;
        while (angleDiff > math.PI) angleDiff -= math.PI * 2f;
        while (angleDiff < -math.PI) angleDiff += math.PI * 2f;
        float maxTurnThisFrame = homing.TurnSpeed * DeltaTime;
        float actualTurn = math.clamp(angleDiff, -maxTurnThisFrame, maxTurnThisFrame);
        float finalAngle = currentAngle + actualTurn;
        float2 newDir = new float2(math.cos(finalAngle), math.sin(finalAngle));

        // Apply move and rotate
        transform.Position.xy += DeltaTime * stats.Speed * newDir;
        transform.Rotation = quaternion.RotateZ(finalAngle - (math.PI / 2f));
    }
}

[BurstCompile]
[WithAll(typeof(ContactTrigger))]
public partial struct ContactTriggerJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, Entity> Map;
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    public float CellSize;
    public int GridWidth;

    private void Execute(
        in LocalTransform transform, 
        ref ProjectileTriggerState state,
        in ProjectileTriggerStats stats
        )
    {
        if (state.IsTriggered) return;

        int index = GridUtility.WorldPosToIndex(transform.Position.xy, CellSize, GridWidth);
        if (Map.TryGetFirstValue(index, out Entity bug, out var it))
        {
            do
            {
                if (!TransformLookup.HasComponent(bug)) continue;

                if (math.distancesq(transform.Position.xy, TransformLookup[bug].Position.xy) <= stats.HitRadiusSq)
                {
                    state.IsTriggered = true;
                    state.EntityHit = bug;
                    break;
                }
            } while (Map.TryGetNextValue(out bug, ref it));
        }
    }
}

[BurstCompile]
[WithAll(typeof(CoordinateTrigger))]
public partial struct CoordinateTriggerJob : IJobEntity
{
    private void Execute(
        in LocalTransform transform, 
        ref ProjectileTriggerState state,
        in ProjectileTriggerStats stats
        )
    {
        if (state.IsTriggered) return;

        if (math.distancesq(transform.Position.xy, stats.TargetPos) <= 0.1f)
        {
            state.IsTriggered = true;
        }
    }
}

[BurstCompile]
[WithAll(typeof(TimerTrigger))]
public partial struct TimerTriggerJob : IJobEntity
{
    public double ElapsedTime;

    private void Execute(
        ref ProjectileTriggerState state,
        in ProjectileTriggerStats stats
        )
    {
        if (state.IsTriggered) return;

        if (stats.TriggerTime <= ElapsedTime)
        {
            state.IsTriggered = true;
        }
    }
}

[BurstCompile]
[WithAll(typeof(KineticPayload))]
public partial struct KineticPayloadJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    private void Execute(
        Entity entity, 
        [ChunkIndexInQuery] int chunkIndex, 
        in ProjectileTriggerState trigger,
        in ProjectilePayloadStats stats
        )
    {
        if (!trigger.IsTriggered) return;

        // Apply damage
        ECB.AppendToBuffer(chunkIndex, trigger.EntityHit, new DamageEvent { Value = stats.Damage });

        // Destroy the projectile
        ECB.DestroyEntity(chunkIndex, entity);
    }
}

[BurstCompile]
[WithAll(typeof(PiercingPayload))]
public partial struct PiercingPayloadJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    private void Execute(
        Entity entity, 
        [ChunkIndexInQuery] int chunkIndex, 
        ref ProjectileTriggerState trigger, 
        ref ProjectilePayloadStats stats
        )
    {
        if (!trigger.IsTriggered) return;

        // Apply damage
        ECB.AppendToBuffer(chunkIndex, trigger.EntityHit, new DamageEvent { Value = stats.Damage });

        // Decrement pierce count
        trigger.RemainingHits--;

        // Destroy projectile if no remaining hits
        if (trigger.RemainingHits <= 0)
        {
            ECB.DestroyEntity(chunkIndex, entity);
            return;
        }

        // Reset trigger
        trigger.IsTriggered = false;
    }
}

[BurstCompile]
[WithAll(typeof(ExplosivePayload))]
public partial struct ExplosivePayloadJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    private void Execute(
        Entity entity, 
        [ChunkIndexInQuery] int chunkIndex, 
        in LocalTransform transform,
        in ProjectilePayloadStats stats,
        in ProjectileTriggerState trigger)
    {
        if (!trigger.IsTriggered) return;

        // Create explosion event
        Entity explosion = ECB.CreateEntity(chunkIndex);
        ECB.AddComponent(chunkIndex, explosion, new LocalTransform { Position = transform.Position });
        ECB.AddComponent(chunkIndex, explosion, new ExplosionEvent
        {
            Damage = stats.Damage,
            RadiusSq = stats.ExplosionRadiusSq,
            Colour = new float4(0, 1, 1, 0)
        });

        // Destroy the projectile
        ECB.DestroyEntity(chunkIndex, entity);
    }
}

[BurstCompile]
public partial struct ProjectileLifespanJob : IJobEntity
{
    public double ElapsedTime;
    public EntityCommandBuffer.ParallelWriter ECB;

    private void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in ProjectileLifespan lifespan)
    {
        if (ElapsedTime >= lifespan.ExpirationTime)
        {
            ECB.DestroyEntity(chunkIndex, entity);
        }
    }
}