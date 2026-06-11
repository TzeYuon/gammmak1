using Unity.Entities;
using Unity.Mathematics;

public struct HiveComponent : IComponentData
{
    public int SpawnAmount;
    
    public float EnemySpeed;
    public float MaxSpeedMult; // Max speed multiplier for enemies that are behind desired position
    public float Density;
    public float Width;

    public float SpawnCooldown;
    public float SpawnTimer;

    public float Size;
}

public struct SwarmMovementStatsComponent : IComponentData
{
    public float BaseSpeed;   
    public float WiggleOffset;
}   

public struct SwarmVelocityComponent : IComponentData
{
    public float2 Value;
}

public struct SwarmTag : IComponentData { }

public struct CollisionRadius : IComponentData
{
    public float Value;
}