using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(EnvironmentUpdateGroup))]
public partial struct ShipReferencesInitSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var parentLookup = SystemAPI.GetComponentLookup<Parent>(isReadOnly: true);
        var shipLookup = SystemAPI.GetComponentLookup<PlayerTag>(isReadOnly: true);

        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var job = new LinkShipReferencesJob
        {
            ParentLookup = parentLookup,
            ShipLookup = shipLookup,
            ECB = ecb
        };

        job.ScheduleParallel();
    }
}

[BurstCompile]
[WithAll(typeof(NeedsShipReferenceTag))]
public partial struct LinkShipReferencesJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<Parent> ParentLookup;
    [ReadOnly] public ComponentLookup<PlayerTag> ShipLookup;
    public EntityCommandBuffer.ParallelWriter ECB;

    private void Execute(Entity entity,
        [ChunkIndexInQuery] int chunkIndex,
        ref ShipReference shipRef,
        in Parent initialParent)
    {
        Entity currentEntity = initialParent.Value;

        while (currentEntity != Entity.Null)
        {
            if (ShipLookup.HasComponent(currentEntity))
            {
                shipRef.Value = currentEntity;
                ECB.RemoveComponent<NeedsShipReferenceTag>(chunkIndex, entity);
                return;
            }

            // Move up to the next parent
            if (ParentLookup.HasComponent(currentEntity))
            {
                currentEntity = ParentLookup[currentEntity].Value;
            }
            else
            {
                // Reached the root, no ship found
                break;
            }
        }
    }
}