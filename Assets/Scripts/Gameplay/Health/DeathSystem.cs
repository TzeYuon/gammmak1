using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(PostHealthUpdateSystemGroup))]
public partial class DeathSystem : SystemBase
{
    private NativeQueue<DeathVFXEvent> deathQueue;

    [BurstCompile]
    protected override void OnCreate()
    {
        RequireForUpdate<HealthComponent>();

        deathQueue = new NativeQueue<DeathVFXEvent>(Allocator.Persistent);
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged).AsParallelWriter();
        var deathJob = new ProcessDeathJob
        {
            ECB = ecb,
            DeathQueueWriter = deathQueue.AsParallelWriter()
        };

        Dependency = deathJob.ScheduleParallel(Dependency);
    }

    [BurstCompile]
    protected override void OnDestroy()
    {
        if (deathQueue.IsCreated) deathQueue.Dispose();
    }

    public NativeQueue<DeathVFXEvent> GetDeathQueue()
    {
        Dependency.Complete();
        return deathQueue;
    }
}

[BurstCompile]
public partial struct ProcessDeathJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;
    public NativeQueue<DeathVFXEvent>.ParallelWriter DeathQueueWriter;

    private void Execute(
        Entity entity,
        [ChunkIndexInQuery] int chunkIndex,
        in HealthComponent health,
        in LocalTransform transform
        )
    {
        // Check if dead
        if (health.Value > 0) return;

        DeathQueueWriter.Enqueue(new DeathVFXEvent
        {
            Position = transform.Position,
            Colour = new float3(255, 0, 0)
        });

        ECB.SetComponentEnabled<DeadTag>(chunkIndex, entity, true);
    }
}