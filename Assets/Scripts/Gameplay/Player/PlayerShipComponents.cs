using Unity.Entities;
using Unity.Mathematics;

public struct PlayerTag : IComponentData { }
public struct MothershipTag : IComponentData { }
public struct SelectedTag : IComponentData, IEnableableComponent { }

public struct MoveCommand : IComponentData, IEnableableComponent
{
    public float2 MoveTarget;
}

public struct ShipMovementStatsComponent : IComponentData
{
    public float MaxSpeed;
    public float Acceleration;
    public float TurnSpeed;
    public float BrakingDistance;
}

public struct ShipVelocity : IComponentData
{
    public float2 Value;
}

/// <summary>
/// Used by other parts of the ships (weapons) to get ship components
/// </summary>
public struct ShipReference : IComponentData
{
    public Entity Value;
}

public struct NeedsShipReferenceTag : IComponentData { }

public struct ShipCollectionRadiusComponent : IComponentData
{
    public float RadiusSq;
}