using Unity.Entities;
using Unity.Mathematics;

public struct HiveComponent : IComponentData
{
    // for targeting and damage
    public float Size;

    public EnemyType SurgeEnemyType;
    public int SurgeSpawnCount;
    public int SurgePendingSpawns;
    public float SurgeSpawnRate;
    public float SurgeSpawnAccumulator;
}

[InternalBufferCapacity(4)]
public struct TrickleSpawnerData : IBufferElementData
{
    public EnemyType Type;
    public int SpawnCount;
    public float Interval;
    public float NextSpawnTime;

    // For scaling during levels
    public float BaseInterval;
    public float BaseSpawnCount;
}

public struct SwarmMovementStatsComponent : IComponentData
{
    public float BaseSpeed;   
    public float SpawnIndex;
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