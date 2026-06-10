using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(LateGameplayGroup))]
public partial struct ProcessDamageSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        state.CompleteDependency();

        var job = new ApplyDamageJob
        {
            ECB = ecb
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[InternalBufferCapacity(4)]
public struct DamageEvent : IBufferElementData
{
    public float Value;
}

[BurstCompile]
[WithDisabled(typeof(DeadTag))]
public partial struct ApplyDamageJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ECB;

    private void Execute(
        Entity entity,
        [ChunkIndexInQuery] int chunkIndex,
        ref HealthComponent health,
        ref DynamicBuffer<DamageEvent> damageBuffer)
    {
        if (damageBuffer.IsEmpty) return;

        float totalDamage = 0f;
        for (int i = 0; i < damageBuffer.Length; i++)
        {
            totalDamage += damageBuffer[i].Value;
        }

        health.Value -= totalDamage;
        damageBuffer.Clear();

        if (health.Value <= 0)
        {
            ECB.SetComponentEnabled<DeadTag>(chunkIndex, entity, true);
        }
    }
}