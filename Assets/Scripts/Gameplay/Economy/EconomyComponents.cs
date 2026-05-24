using Unity.Entities;

public struct BiomassLoot : IComponentData
{
    public float Value;
    public float TimeRemaining;
}

public struct PlayerEconomy : IComponentData
{
    public float TotalBiomass;
}