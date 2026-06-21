using Unity.Entities;

public enum TerrainTileType
{
    Empty = 0,
    Wall = 1,
    Mud = 2,
    Poison = 3
}

public struct LevelBlueprintComponent : IComponentData
{
    public int GridWidth;
    public int GridHeight;
    public TerrainType TerrainShape;

    public float NoiseScale;

    // Anything above this number becomes a wall
    public float WallThreshold;

    // Anything above this number becomes Poison
    public float PoisonThreshold;

    // Anything below this number becomes Mud
    public float MudThreshold;

    public uint Seed;
}
