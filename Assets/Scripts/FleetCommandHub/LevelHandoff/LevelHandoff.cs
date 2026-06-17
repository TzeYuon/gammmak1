using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class LevelHandoff : MonoBehaviour
{
    [SerializeField] private PlayerSaveData playerSaveData;

    private void Start()
    {
        GameStateSystem.OnStateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        GameStateSystem.OnStateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameState prev, GameState cur)
    {
        if (cur == GameState.Generating) ConvertSaveToComponents();
    }

    public void ConvertSaveToComponents()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        var query = entityManager.CreateEntityQuery(typeof(BakedShipData), typeof(BakedWeaponData));
        if (query.IsEmptyIgnoreFilter)
        {
            Debug.LogError("Could not find the Ship Database in the ECS World");
            return;
        }

        Entity databaseEntity = query.GetSingletonEntity();

        // Get database
        var shipDB = entityManager.GetBuffer<BakedShipData>(databaseEntity).ToNativeArray(Allocator.Temp);
        var weaponDB = entityManager.GetBuffer<BakedWeaponData>(databaseEntity).ToNativeArray(Allocator.Temp);

        // fill up roster based on database
        int count = 0;
        for (int i = 0; i < playerSaveData.ActiveRoster.Length; i++)
        {
            ShipBlueprintData blueprint = playerSaveData.ActiveRoster[i];
            if (blueprint == null) continue;

            Entity matchedPrefab = GetPrefabFromDatabase(shipDB, blueprint.ShipID);
            if (matchedPrefab == Entity.Null) continue;

            Entity recipeEntity = entityManager.CreateEntity();

            // Calculate final ship stats
            float finalHealth = 10;
            float finalSpeed = 10;

            // add blueprint to spawn in the ship
            entityManager.AddComponentData(recipeEntity, new RosterShipBlueprint
            {
                SlotIndex = count++,
                ShipPrefab = matchedPrefab,
                BiomassCost = blueprint.CostToSpawn,
                MaxHealth = finalHealth,
                MaxSpeed = finalSpeed
            });

            // Create weapon blueprints for each weapon mount on the ship
            var weaponBuffer = entityManager.AddBuffer<RosterWeaponBlueprint>(recipeEntity);
            for (int m = 0; m < blueprint.Mounts.Count; m++)
            {
                var mount = blueprint.Mounts[m];
                WeaponData weapon = mount.EquippedWeapon;
                if (weapon == null) continue;

                Entity weaponPrefab = GetPrefabFromDatabase(weaponDB, weapon.WeaponID);

                // add blueprint to spawn weapon
                weaponBuffer.Add(new RosterWeaponBlueprint
                {
                    WeaponPrefab = weaponPrefab,
                    Position = mount.Position,
                    
                    WeaponTargetingStats = new WeaponTargetingStats
                    {
                        Range = weapon.GetStatAtLevel(WeaponStat.Range),
                        RangeSq = weapon.GetStatAtLevel(WeaponStat.Range) * weapon.GetStatAtLevel(WeaponStat.Range),
                        DensityBias =weapon.DensityBias,
                        AngleBias = weapon.AngleBias,
                        DistanceBias = weapon.DistanceBias,
                    },

                    WeaponCombatStats = new WeaponCombatStats
                    {
                        Damage = weapon.GetStatAtLevel(WeaponStat.Damage),
                        FireRate = weapon.GetStatAtLevel(WeaponStat.FireRate),
                        Spread = weapon.GetStatAtLevel(WeaponStat.Spread),
                        ProjectileSpeed = weapon.GetStatAtLevel(WeaponStat.ProjectileSpeed),
                        ProjectileTurnSpeed = weapon.GetStatAtLevel(WeaponStat.ProjectileTurnSpeed),
                        HitRadius = weapon.GetStatAtLevel(WeaponStat.HitRadius),
                        ExplosionRadiusSq = weapon.GetStatAtLevel(WeaponStat.ExplosionRadius) * weapon.GetStatAtLevel(WeaponStat.ExplosionRadius),
                        PierceCount = Mathf.RoundToInt(weapon.GetStatAtLevel(WeaponStat.PierceCount)),
                        TriggerTime = weapon.GetStatAtLevel(WeaponStat.TriggerTime),
                    }
                });
            }
        }

        shipDB.Dispose();
        weaponDB.Dispose();

        var checklistQuery = entityManager.CreateEntityQuery(typeof(LevelGenerationStatus));
        if (!checklistQuery.IsEmptyIgnoreFilter) return; // LevelGenerationStatus singleton already exists

        Entity generatingStatus = entityManager.CreateEntity();
        entityManager.AddComponentData(generatingStatus, new LevelGenerationStatus
        {
            PlayerFleetSpawned = false,
            TerrainGenerated = false,
            HivesGenerated = false
        });
    }

    Entity GetPrefabFromDatabase(NativeArray<BakedShipData> db, ShipID id)
    {
        for (int i = 0; i < db.Length; i++)
        {
            if (db[i].ShipID == id)
            {
                return db[i].PrefabEntity;
            }
        }

        return Entity.Null;
    }

    Entity GetPrefabFromDatabase(NativeArray<BakedWeaponData> db, WeaponID id)
    {
        for (int i = 0; i < db.Length; i++)
        {
            if (db[i].WeaponID == id)
            {
                return db[i].PrefabEntity;
            }
        }

        return Entity.Null;
    }
}
