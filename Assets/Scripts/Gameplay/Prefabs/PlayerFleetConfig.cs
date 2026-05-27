using Unity.Entities;

public struct PlayerFleetConfig : IComponentData
{
    public Entity FrigatePrefab;
    public float FrigateCost;
    public Entity DestroyerPrefab;
    public float DestroyerCost;
    public Entity CruiserPrefab;
    public float CruiserCost;
}
