using Unity.Entities;
using Unity.Mathematics;

public struct StandardProjectile : IComponentData
{
    public float Damage;
}

public struct ProjectileMovement : IComponentData
{
    public float2 Velocity;
    public float HitRadiusSq;
}