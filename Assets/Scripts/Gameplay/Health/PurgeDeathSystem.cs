using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(PurgeSystemGroup))]
partial struct PurgeDeathSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var purgeJob = new PurgeEntitiesJob
        {
            ECB = ecb
        };

        state.Dependency = purgeJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(DeadTag))]
public partial struct PurgeEntitiesJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    private void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex)
    {
        ECB.DestroyEntity(chunkIndex, entity);
    }
}