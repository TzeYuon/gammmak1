using Unity.Entities;
using Unity.Mathematics;

// Movement
public struct ProjectileMovementStats : IComponentData
{
    public float Speed;    
    public float HomingRange;
    public float TurnSpeed;
}

public struct ProjectileMovement : IComponentData
{
    public float2 MovedThisFrame;
}

/// <summary>
/// Projectile flies straight until trigger
/// </summary>
public struct LinearFlight : IComponentData { }

/// <summary>
/// Steers towards target position
/// </summary>
public struct HomingFlight : IComponentData 
{
    public float DistanceBias;
    public float AngleBias;
}

// Trigger
/// <summary>
/// projectile hit entities stored in a buffer on the projectile
/// </summary>
[InternalBufferCapacity(4)]
public struct ProjectileHitElement : IBufferElementData
{
    public Entity HitEntity;
}

/// <summary>
/// Universal trigger for the payload to read
/// </summary>
public struct ProjectileTriggerState : IComponentData
{
    public bool IsTriggered;
    public int RemainingHits;
}

public struct ProjectileTriggerStats : IComponentData
{
    public float HitRadius;
    public float2 TargetPos;
    public float TriggerTime;
}

/// <summary>
/// Triggers when it hits an entity
/// </summary>
public struct ContactTrigger : IComponentData { }

/// <summary>
/// Triggers when it reaches target
/// </summary>
public struct CoordinateTrigger : IComponentData { }

/// <summary>
/// Triggers only after a fixed duration
/// </summary>
public struct TimerTrigger : IComponentData { }

// Payload
public struct ProjectilePayloadStats : IComponentData
{
    public float Damage;
    public float ExplosionRadiusSq;
}

/// <summary>
/// Used by contact trigger projectiles to apply damage directly to entities hit, can be used for piercing weapons as well
/// </summary>
public struct KineticPayload : IComponentData { }

/// <summary>
/// Used by any trigger to create an explosion
/// </summary>
public struct ExplosivePayload : IComponentData { }

// Lifespan
public struct ProjectileLifespan : IComponentData
{
    public double ExpirationTime;
}

public struct CollideWithObstacles : IComponentData
{}