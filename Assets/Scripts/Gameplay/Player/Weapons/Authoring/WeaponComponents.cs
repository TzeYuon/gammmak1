using System.Drawing;
using Unity.Entities;
using Unity.Mathematics;

public struct WeaponTargetingStats : IComponentData
{
    public bool IgnoreObstacles;
    public bool RequireTargetEntity;
    public float Range;
    public float RangeSq;
    public float DensityBias;
    public float ThreatBias;
    public float AngleBias;
    public float DistanceBias;
}

/// <summary>
/// The angles that the weapon is allowed to point towards
/// </summary>
public struct WeaponFiringArc : IComponentData
{
    public quaternion RestingRotation;
    public float MaxAngle;
}

public struct TurretComponent : IComponentData
{
    public float TurnSpeed;
}

public struct WeaponCombatStats : IComponentData
{
    public float Damage;
    public float FireRate;
    public float Spread;

    public bool IgnoreObstacles;
    public int ProjectileCount;
    public float BurstInterval;
    public float ProjectileSpeed;
    public float ProjectileTurnSpeed;
    public float HitRadius;
    public float ExplosionRadiusSq;
    public int PierceCount;
    public float TriggerTime;

    public float ProjectileLifetime;
}

public struct HitscanWeapon : IComponentData
{
    public float2 WeaponStartOffset;
    public float4 VFXColour;
    public AudioType AudioType;
}

public struct ProjectileWeapon : IComponentData
{
    public Entity ProjectilePrefab;
    public float2 WeaponStartOffset;
    public float4 VFXColour;
    public AudioType AudioType;
}

public struct WeaponState : IComponentData
{
    public float FireTimer;

    public int BurstRemaining; // projectiles left this fire
    public float BurstInterval; // interval between projectiles in one burst
    public float SubFireTimer; // timer to track interval

    public float2 TargetPos;
    public Entity TargetEntity;
    public bool HasTarget;
}