using UnityEngine;

public static class EnumUtils
{
    public static string GetStatName(ShipStat stat)
    {
        switch (stat)
        {
            case ShipStat.Cost:
                return "Deployment Cost";
            case ShipStat.MaxHealth:
                return "Hull";
            case ShipStat.Speed:
                return "Max Speed";
            case ShipStat.TurnSpeed:
                return "Turn Speed";
            case ShipStat.CollectionRadius:
                return "Collection Radius";
            case ShipStat.RevealFogRadius:
                return "Fog Reveal Radius";
            case ShipStat.WeaponCapacity:
                return "Weapon Capacity";
            case ShipStat.DamageReduction:
                return "Damage Reduction";
            case ShipStat.ShieldMaxCapacity:
                return "Shield Max Capacity";
            case ShipStat.ShieldRegenPerSecond:
                return "Shield Recharge Rate";
            case ShipStat.ShieldDamage:
                return "Shield Damage";
            case ShipStat.ShieldEfficiencyBonus:
                return "Shield Efficiency";
            default:
                return stat.ToString();
        }
    }

    public static string GetStatName(WeaponStat stat)
    {
        return stat switch
        {
            WeaponStat.Damage => "Damage",
            WeaponStat.FireRate => "Fire Rate",
            WeaponStat.Range => "Range",
            WeaponStat.Spread => "Accuracy",
            WeaponStat.HitRadius => "Projectile Size",
            WeaponStat.ExplosionRadius => "Blast Radius",
            WeaponStat.ProjectileSpeed => "Projectile Speed",
            WeaponStat.ProjectileTurnSpeed => "Projectile Turn Speed",
            WeaponStat.PierceCount => "Piercing",
            WeaponStat.ProjectileCount => "Number of projectiles",
            _ => stat.ToString(),
        };
    }
}
