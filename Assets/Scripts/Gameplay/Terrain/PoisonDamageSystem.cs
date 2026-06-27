using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

[UpdateInGroup(typeof(LateGameplayGroup))]
[UpdateBefore(typeof(ProcessDamageSystem))]
partial struct PoisonDamageSystem : ISystem
{
    private float tickTimer;
    private float tickInterval;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TerrainGridComponent>();

        tickTimer = 0.5f;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        tickTimer -= SystemAPI.Time.DeltaTime;
        if (tickTimer > 0) return;

        tickTimer = 0.5f;

        if (!SystemAPI.TryGetSingleton<TerrainGridComponent>(out var terrainGrid)) return;
        if (!terrainGrid.Grid.IsCreated) return;

        var job = new PoisonDamageJob
        {
            Grid = terrainGrid.Grid,
            GridWidth = terrainGrid.GridWidth,
            GridHeight = terrainGrid.GridHeight,
            WorldOrigin = terrainGrid.WorldOrigin,
            PoisonDamagePerTick = 1.0f
        };

        // Schedule in Parallel
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithDisabled(typeof(DeadTag))] // Don't poison dead things
public partial struct PoisonDamageJob : IJobEntity
{
    [ReadOnly] public NativeArray<int> Grid;
    public int GridWidth;
    public int GridHeight;
    public int2 WorldOrigin;
    public float PoisonDamagePerTick;

    void Execute(ref LocalTransform transform, ref DynamicBuffer<DamageEvent> damageBuffer)
    {
        float3 curPos = transform.Position;

        int gridX = (int)math.round(curPos.x) - WorldOrigin.x;
        int gridY = (int)math.round(curPos.y) - WorldOrigin.y;

        if (gridX >= 0 && gridX < GridWidth && gridY >= 0 && gridY < GridHeight)
        {
            int tileType = Grid[gridX + gridY * GridWidth];

            if (tileType == (int)TerrainTileType.Poison)
            {
                damageBuffer.Add(new DamageEvent { Value = PoisonDamagePerTick });
            }
        }
    }
}