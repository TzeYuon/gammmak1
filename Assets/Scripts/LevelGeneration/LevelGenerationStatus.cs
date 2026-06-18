using Unity.Entities;

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

/// <summary>
/// The settings passed to level generation systems to determine type of terrain, type of enemies, difficulty and others
/// </summary>
public struct LevelBlueprint : IComponentData
{
    public NodeType NodeType;

    public RewardType RewardType;
    public ThreatLevel ThreatLevel;
    public ObjectiveType ObjectiveType;
    public TerrainType TerrainType;
    public EnemyProfile EnemyProfile;
    public AnomalyType AnomalyType;

    public int UpgradeCoreReward;

    public uint Seed;
}