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

    public bool IsComplete()
    {
        return PlayerFleetSpawned && TerrainGenerated && HivesGenerated;
    }
}
public struct LevelSpawnData : IComponentData
{
    public float3 MothershipSpawn;
    public unsafe fixed float HiveSpawnsX[8];
    public unsafe fixed float HiveSpawnsY[8];

    public int TotalHiveSpots;
    public int ActiveHiveCount;
}

/// <summary>
/// The settings passed to level generation systems to determine type of terrain, type of enemies, difficulty and others
/// </summary>
public struct LevelBlueprint : IComponentData
{
    public uint Seed;
    public TerrainType TerrainType;

    // Gameplay and Campaign settings
    public NodeType NodeType;
    public RewardType RewardType;
    public ThreatLevel ThreatLevel;
    public ObjectiveType ObjectiveType;
    public EnemyProfile EnemyProfile;
    public AnomalyType AnomalyType;
    public int UpgradeCoreReward;

    // Terrain Generation setttings
    public int GridWidth;
    public int GridHeight;
    public float NoiseScale;
    public float WallThreshold; // Anything above this number becomes a wall
    public float PoisonThreshold; // Anything above this number becomes Poison
    public float MudThreshold; // Anything below this number becomes Mud
}