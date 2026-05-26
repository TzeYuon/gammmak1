using Unity.Entities;
using Unity.Mathematics;

public struct HiveComponent : IComponentData
{
    public Entity Prefab;
    public float SpawnAmount;
    
    public float EnemySpeed;
    public float MaxSpeedMult; // Max speed multiplier for enemies that are behind desired position
    public float Density;
    public float Width;

    public float SpawnCooldown;
    public float SpawnTimer;
}

public struct SwarmComponent : IComponentData
{
    public float BaseSpeed;   
    public float WiggleOffset;
}   

public struct VelocityComponent : IComponentData
{
    public float2 Value;
}

public struct SwarmTag : IComponentData { }