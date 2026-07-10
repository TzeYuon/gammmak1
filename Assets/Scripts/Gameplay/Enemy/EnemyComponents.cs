using Unity.Entities;
using Unity.Mathematics;

public struct HiveComponent : IComponentData
{
    // for targeting and damage
    public float Size;
}

public struct HiveSurgeComponent : IComponentData
{
    public EnemyType SurgeEnemyType;
    public bool SurgeCountIgnoreScaling;
    public int SurgeSpawnCount;
    public int SurgePendingSpawns;
    public float SurgeSpawnRate;
    public float SurgeSpawnAccumulator;
}

public struct ActiveHiveTag : IComponentData, IEnableableComponent { }

[InternalBufferCapacity(4)]
public struct TrickleSpawnerData : IBufferElementData
{
    public EnemyType Type;
    public int SpawnCount;
    public float Interval;
    public float NextSpawnTime;
}

[InternalBufferCapacity(16)]
public struct SiegePathWaypointElement : IBufferElementData
{
    public float2 Position;
}

public struct SwarmMovementStatsComponent : IComponentData
{
    public float BaseSpeed;   
    public float SpawnIndex;
    public float SeparationWeight;
    public float TerrainAvoidanceWeight;
    public float NoiseWeight;
}   

public struct SwarmDamageComponent : IComponentData
{
    public float Damage;
}

public struct SwarmVelocityComponent : IComponentData
{
    public float2 Value;
}

public struct SwarmTag : IComponentData { }

public struct PathFollowerComponent : IComponentData
{
    public Entity TargetPath;
    public float CurrentProgress;
}

public struct EnemyValueComponent : IComponentData
{
    public int Value;
}

public struct CollisionRadius : IComponentData
{
    public float Value;
}