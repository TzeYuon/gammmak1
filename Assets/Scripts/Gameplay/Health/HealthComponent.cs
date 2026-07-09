using Unity.Entities;
using Unity.Mathematics;

public struct HealthComponent : IComponentData
{
    public float Value;
    public float MaxValue;
    public float FlatDamageReduction;
}

public struct DeathVFXEvent
{
    public float3 Position;
    public float3 Colour;
}

public struct DeadTag : IComponentData, IEnableableComponent { }