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

public struct PathLeaderComponent : IComponentData
{
    public float Density;
    public float Width;
    public float Speed;
    public float MaxSpeedMult; // Max speed multiplier for enemies that are behind desired position
    public int MaxPathLength;

    public float2 MoveDirection;
    public float2 LastTargetPos;
}

[InternalBufferCapacity(128)]
public struct PathPosition : IBufferElementData
{
    public float3 Value;
    public static implicit operator PathPosition(float3 v) => new() { Value = v };
    public static implicit operator float3(PathPosition e) => e.Value;
}

public struct PathFollowerComponent : IComponentData
{
    public Entity PathLeaderEntity; 
    public float Rank;
    public float PathPosition;
    public float Offset; // how far left or right of the path should the enemy be, value between -1 to 1
    public float NoiseStrength;
}

public struct VelocityComponent : IComponentData
{
    public float2 Value;
}

public struct SwarmTag : IComponentData { }