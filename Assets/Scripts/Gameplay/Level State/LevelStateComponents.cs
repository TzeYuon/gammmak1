using Unity.Entities;

public enum LevelState : byte
{
    Playing,
    Victory,
    Defeat
}

public struct LevelStateComponent : IComponentData
{
    public LevelState Result;
    public bool IsInitialized;
}