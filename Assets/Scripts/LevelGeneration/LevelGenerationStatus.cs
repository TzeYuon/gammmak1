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