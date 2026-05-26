using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[UpdateInGroup(typeof(PostBakingSystemGroup))] 
public partial struct ShipReferencesInitSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var parentLookup = SystemAPI.GetComponentLookup<Parent>(isReadOnly: true);
        var shipLookup = SystemAPI.GetComponentLookup<PlayerTag>(isReadOnly: true);

        var job = new LinkShipReferencesJob
        {
            ParentLookup = parentLookup,
            ShipLookup = shipLookup
        };

        job.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct LinkShipReferencesJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<Parent> ParentLookup;
    [ReadOnly] public ComponentLookup<PlayerTag> ShipLookup;

    private void Execute(Entity entity, ref ShipReference shipRef, in Parent initialParent)
    {
        Entity currentEntity = initialParent.Value;

        while (currentEntity != Entity.Null)
        {
            if (ShipLookup.HasComponent(currentEntity))
            {
                shipRef.Value = currentEntity;
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