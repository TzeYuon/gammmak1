using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

public struct PlayerTag : IComponentData { }
public struct MothershipTag : IComponentData { }
public struct SelectedTag : IComponentData, IEnableableComponent { }

public struct MoveCommandProjection : IComponentData, IEnableableComponent
{
    public float2 MoveTarget;
    public bool UseTargetRotation;
    public quaternion TargetRotation;
}

public struct MoveCommand : IComponentData, IEnableableComponent
{
    public float2 MoveTarget;
    public bool Recalculate;
    public bool ForcePath;
    public float2 NextWaypoint;
    public bool PathExists;

    // for manual rotation
    public bool UseTargetRotation;
    public quaternion TargetRotation;
}

[InternalBufferCapacity(20)]
public struct ShipWaypoint : IBufferElementData
{
    public float2 Value;
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

public struct ShipRevealFogRadius : IComponentData
{
    public float Value;
    public float ValueSq;
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

public struct MovementTargetVisualComponent : IComponentData { }
public struct MovementTargetProjectionVisualComponent : IComponentData { }

[MaterialProperty("_ArcAngle")]
public struct ArcAngleOverride : IComponentData
{
    public float Value;
}

// Shield components
public struct ShieldComponent : IComponentData
{
    public float CurrentCapacity;
    public float MaxCapacity;
    public float RechargePerSecond;

    public float Radius;
    public float DamagePerSecond; // How much damage it deals to a bug
    public float BonusShieldEfficiency; // Capacity loss = damage dealt * (1 - Shield efficiency)

    public float RechargeDelay;
    public float TimeSinceLastHit;
}

[MaterialProperty("_ShieldOpacity")]
public struct ShieldOpacityOverride : IComponentData
{
    public float Value;
}