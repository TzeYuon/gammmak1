using Unity.Entities;
using Unity.Mathematics;

public struct WeaponStats : IComponentData
{
    public float Range;
}

public struct WeaponState : IComponentData
{
    public float FireTimer;
    public float FireRate;
    public float2 TargetPos;
    public bool HasTarget;
}