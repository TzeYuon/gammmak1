using Unity.Entities;
using Unity.Mathematics;

public enum MapShape
{
    Standard = 0,
    Caldera = 1,
    Island = 2,
    Canyon = 3
}

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
    public MapShape Shape;

    public float NoiseScale;

    // Anything above this number becomes a wall
    public float WallThreshold;

    // Anything above this number becomes Poison
    public float PoisonThreshold;

    // Anything below this number becomes Mud
    public float MudThreshold;

    public uint Seed;
}
