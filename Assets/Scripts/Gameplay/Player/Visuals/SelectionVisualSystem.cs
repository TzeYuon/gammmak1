using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(PresentationSystemGroup))]
partial struct SelectionVisualSystem : ISystem
{
    private ComponentLookup<SelectedTag> selectedLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        selectedLookup = state.GetComponentLookup<SelectedTag>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        selectedLookup.Update(ref state);

        var job = new UpdateSelectionRingsJob
        {
            SelectedLookup = selectedLookup,
            DeltaTime = SystemAPI.Time.DeltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
public partial struct UpdateSelectionRingsJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<SelectedTag> SelectedLookup;
    public float DeltaTime;

    private void Execute(ref LocalTransform transform, in SelectionVisualComponent visual, in ShipReference shipReference)
    {
        if (shipReference.Value == Entity.Null) return; // Invalid ship reference

        if (!SelectedLookup.HasComponent(shipReference.Value)) // ship does not have selected component
        {
            transform.Scale = 0f;
            return;
        }

        if (SelectedLookup.IsComponentEnabled(shipReference.Value)) // ship selected
        {
            transform.Scale = visual.Scale;
            //transform.Rotation = math.mul(transform.Rotation, quaternion.RotateZ(1.5f * DeltaTime));
        }
        else // ship not selected
        {
            transform.Scale = 0f;
        }
    }
}