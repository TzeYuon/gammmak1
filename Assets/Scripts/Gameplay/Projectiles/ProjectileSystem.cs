using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(ActionAndMovementGroup))]
partial struct ProjectileSystem : ISystem
{
    private ComponentLookup<LocalTransform> transformLookup;
    private ComponentLookup<CollisionRadius> collisionLookup;
    private ComponentLookup<HealthComponent> healthLookup;

    public void OnCreate(ref SystemState state)
    {
        transformLookup = state.GetComponentLookup<LocalTransform>(true);
        collisionLookup = state.GetComponentLookup<CollisionRadius>(true);
        healthLookup = state.GetComponentLookup<HealthComponent>(false);
        state.RequireForUpdate<FineEnemyHeatmapComponent>();
        state.RequireForUpdate<TerrainGridComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        transformLookup.Update(ref state);
        collisionLookup.Update(ref state);
        healthLookup.Update(ref state);

        var fineHeatmap = SystemAPI.GetSingleton<FineEnemyHeatmapComponent>();
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        float deltaTime = SystemAPI.Time.DeltaTime;
        double elapsedTime = SystemAPI.Time.ElapsedTime;

        var terrainGridComponent = SystemAPI.GetSingleton<TerrainGridComponent>();
        NativeArray<int> obstacleGrid = terrainGridComponent.Grid;
        int2 obstacleWorldOrigin = terrainGridComponent.WorldOrigin;
        int obstacleGridWidth = terrainGridComponent.GridWidth;
        int obstacleGridHeight = terrainGridComponent.GridHeight;

        // Movement
        var linearHandle = new LinearFlightJob { DeltaTime = deltaTime }.ScheduleParallel(state.Dependency);

        var homingHandle = new HomingFlightJob 
        { 
            DeltaTime = deltaTime,
            Map = fineHeatmap.DensityMap,
            WorldOrigin = fineHeatmap.WorldOrigin,
            GridWidth = fineHeatmap.GridWidth,
            GridHeight = fineHeatmap.GridHeight,
            CellSize = fineHeatmap.CellSize,
        }.ScheduleParallel(linearHandle);

        // Trigger
        var timerHandle = new TimerTriggerJob { ElapsedTime = elapsedTime }.ScheduleParallel(homingHandle);

        var coordHandle = new CoordinateTriggerJob().ScheduleParallel(timerHandle);

        var contactHandle = new ContactTriggerJob
        {
            Map = fineHeatmap.Map,
            TransformLookup = transformLookup,
            RadiusLookup = collisionLookup,
            WorldOrigin = fineHeatmap.WorldOrigin,
            CellSize = fineHeatmap.CellSize,
            GridWidth = fineHeatmap.GridWidth,
        }.ScheduleParallel(coordHandle);

        var obstacleHandle = new ObstacleTriggerJob
        {
            ObstacleGrid = obstacleGrid,
            ObstacleWorldOrigin = obstacleWorldOrigin,
            ObstacleGridWidth = obstacleGridWidth,
            ObstacleGridHeight = obstacleGridHeight,
        }.ScheduleParallel(contactHandle);

        var triggerhandle = obstacleHandle;

        // Payload
        var kineticHandle = new KineticPayloadJob
        {
            ECB = ecb,
        }.ScheduleParallel(triggerhandle);

        var explosiveHandle = new ExplosivePayloadJob
        {
            ECB = ecb
        }.ScheduleParallel(kineticHandle);

        var piercingHandle = new PiercingPayloadJob
        {
            ECB = ecb,
        }.ScheduleParallel(explosiveHandle);

        // Lifespan
        var lifespanHandle = new ProjectileLifespanJob
        {
            ElapsedTime = elapsedTime,
            ECB = ecb,
        }.ScheduleParallel(piercingHandle);

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
    public int2 WorldOrigin;
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
        int2 centerCell = GridUtility.WorldPosToCell(currentPos, WorldOrigin, CellSize);

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

                float2 cellWorldPos = GridUtility.CellToWorldPosCenter(checkCell, WorldOrigin, CellSize);
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
        float maxTurnThisFrame = stats.TurnSpeed * DeltaTime;
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
    [ReadOnly] public ComponentLookup<CollisionRadius> RadiusLookup;
    public int2 WorldOrigin;
    public float CellSize;
    public int GridWidth;

    private void Execute(
        in LocalTransform transform, 
        ref ProjectileTriggerState state,
        in ProjectileTriggerStats stats
        )
    {
        if (state.IsTriggered) return;

        int index = GridUtility.WorldPosToIndex(transform.Position.xy, WorldOrigin, CellSize, GridWidth);
        if (Map.TryGetFirstValue(index, out Entity bug, out var it))
        {
            do
            {
                if (!TransformLookup.HasComponent(bug)) continue;

                float targetRadius = 0.5f;
                if (RadiusLookup.HasComponent(bug))
                {
                    targetRadius = RadiusLookup[bug].Value;
                }

                float combinedRadius = stats.HitRadius + targetRadius;
                float maxHitDistSq = combinedRadius * combinedRadius;

                if (math.distancesq(transform.Position.xy, TransformLookup[bug].Position.xy) <= maxHitDistSq)
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
        if (trigger.EntityHit != Entity.Null) ECB.AppendToBuffer(chunkIndex, trigger.EntityHit, new DamageEvent { Value = stats.Damage });

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
        in ProjectilePayloadStats stats
        )
    {
        if (!trigger.IsTriggered) return;

        // Apply damage
        if (trigger.EntityHit != Entity.Null) ECB.AppendToBuffer(chunkIndex, trigger.EntityHit, new DamageEvent { Value = stats.Damage });

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

[BurstCompile]
public partial struct ObstacleTriggerJob : IJobEntity
{
    [ReadOnly] public NativeArray<int> ObstacleGrid;
    public int2 ObstacleWorldOrigin;
    public int ObstacleGridWidth;
    public int ObstacleGridHeight;

    private void Execute(
        ref ProjectileTriggerState state,
        ref LocalTransform transform)
    {
        if (state.IsTriggered) return;

        float2 currentPos = transform.Position.xy;
        if (GridUtility.GetTerrainAtWorldPos(currentPos, ObstacleGrid, ObstacleWorldOrigin, ObstacleGridWidth, ObstacleGridHeight) == 1)
        {
            state.IsTriggered = true;
        }
    }
}