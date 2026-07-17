using Unity.Entities;
using Unity.Mathematics;

public enum TerrainTileType
{
    Empty = 0,
    Wall = 1,
    Mud = 2,
    Poison = 3
}

public struct LevelGenerationStatus : IComponentData
{
    public bool PlayerFleetSpawned;
    public bool TerrainGenerated;
    public bool HivesGenerated;
    public bool HiveSiegePathsGenerated;

    public bool IsComplete()
    {
        return PlayerFleetSpawned && TerrainGenerated && HivesGenerated && HiveSiegePathsGenerated;
    }
}
public struct LevelSpawnData : IComponentData
{
    public float3 MothershipSpawn;
    public unsafe fixed float HiveSpawnsX[8];
    public unsafe fixed float HiveSpawnsY[8];
    public int ActiveHiveCount;

    public unsafe fixed float ExpansionSpotsX[8];
    public unsafe fixed float ExpansionSpotsY[8];
    public int ExpansionSpotCount;
}

/// <summary>
/// The settings passed to level generation systems to determine type of terrain, type of enemies, difficulty and others
/// </summary>
public struct LevelBlueprint : IComponentData
{
    public int Layer;

    public uint Seed;
    public TerrainType TerrainType;

    // Gameplay and Campaign settings
    public NodeType NodeType;
    public RewardType RewardType;
    public ThreatLevel ThreatLevel;
    public ObjectiveType ObjectiveType;
    public AnomalyType AnomalyType;
    public int UpgradeCoreReward;

    // Terrain Generation setttings
    public int GridWidth;
    public int GridHeight;
    public float NoiseScale;
    public float WallThreshold; // Anything above this number becomes a wall
    public float PoisonThreshold; // Anything above this number becomes Poison
    public float MudThreshold; // Anything below this number becomes Mud

    // Enemy profile variables
    public float TotalHiveWeight;
}

public struct HiveBlueprint : IComponentData
{
    public int Weight;
    public Entity RecipeEntity;
    public HiveSurgeComponent HiveSurgeComponent;
}

public struct EnemyBlueprint : IBufferElementData
{
    public EnemyType Type;
    public Entity Prefab;

    public HealthComponent BaseHealthComponent;
    public HealthComponent ScaledHealthComponent;
}

public struct EnemyBlueprintDatabase : IComponentData { }