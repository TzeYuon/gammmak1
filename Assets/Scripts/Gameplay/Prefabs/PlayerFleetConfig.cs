using Unity.Entities;

public struct PlayerFleetConfig : IComponentData
{
    public Entity FighterPrefab;
    public float FighterCost;
}
