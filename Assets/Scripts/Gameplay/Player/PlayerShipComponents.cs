using Unity.Entities;
using Unity.Mathematics;

public struct PlayerTag : IComponentData { }
public struct SelectedTag : IComponentData, IEnableableComponent { }

public struct MoveCommand : IComponentData, IEnableableComponent
{
    public float2 MoveTarget;
}

public struct ShipMovementStatsComponent : IComponentData
{
    public float Thrust;
    public float MaxSpeed;
}
