using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// For weapons that target specific enemies
/// </summary>
public struct PrecisionTargetingComponent : IComponentData
{
    public float Range;
    public float AngleBias;
}

/// <summary>
/// For weapons that target the highest density
/// </summary>
public struct HeatmapTargetingComponent : IComponentData
{
    public float Range;
    public float AngleBias;
}

/// <summary>
/// The angles that the weapon is allowed to point towards
/// </summary>
public struct WeaponFiringArc : IComponentData
{
    public float3 BaseDirection;
    public float MaxAngle;
}

public struct TurretComponent : IComponentData
{
    public float TurnSpeed;
}

public struct HitscanWeapon : IComponentData
{
    public float Damage;
}

public struct ProjectileWeapon : IComponentData
{
    public Entity ProjectilePrefab;
    public float Damage;
    public float HitRadius;
    public float ExplosionRadiusSq;
    public int PierceCount;
    public float ProjectileSpeed;
    public float Spread;
    public float TriggerTime;
}

public struct WeaponState : IComponentData
{
    public float FireTimer;
    public float FireRate;
    public float2 TargetPos;
    public bool HasTarget;
}