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
        if (cur == GameState.Generating)
        {
            StartGeneratingLevel();
        }
    }

    void StartGeneratingLevel()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // Converts all ship and weapon stats to proper prefabs to be spawned in
        ConvertRosterToComponents(entityManager);

        var checklistQuery = entityManager.CreateEntityQuery(typeof(LevelGenerationStatus));
        if (!checklistQuery.IsEmptyIgnoreFilter) return; // LevelGenerationStatus singleton already exists

        // Create an entity to track the generation of levels
        Entity generatingStatus = entityManager.CreateEntity();

        // Convert the level data to usable components
        LevelBlueprint levelBlueprint = ConvertLevelToComponent(entityManager);
        entityManager.AddComponentData(generatingStatus, levelBlueprint);
        
        // init all to incomplete
        entityManager.AddComponentData(generatingStatus, new LevelGenerationStatus
        {
            PlayerFleetSpawned = false,
            TerrainGenerated = false,
            HivesGenerated = false
        });
    }

    void ConvertRosterToComponents(EntityManager entityManager)
    {
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

        // Update mothership stats and weapons
        entityManager = CreateMothershipBlueprint(entityManager, weaponDB);

        // fill up roster based on database
        int count = 0;
        for (int i = 0; i < playerSaveData.ActiveRoster.Length; i++)
        {
            ShipBlueprintData blueprint = playerSaveData.ActiveRoster[i];
            if (blueprint == null) continue;

            Entity matchedPrefab = GetPrefabFromDatabase(shipDB, blueprint.ShipID);
            if (matchedPrefab == Entity.Null) continue;

            Entity blueprintEntity = entityManager.CreateEntity();

            // Calculate final ship stats
            float health = blueprint.GetStat(ShipStat.MaxHealth);
            float collectionRadius = blueprint.GetStat(ShipStat.CollectionRadius);

            // Calculate braking distance
            float speed = blueprint.GetStat(ShipStat.Speed);
            float acceleration = blueprint.GetStat(ShipStat.Acceleration);
            float stoppingDistance = (speed * speed) / (2.0f * acceleration);
            float finalBrakingRadius = stoppingDistance * 1.25f;
            finalBrakingRadius = Mathf.Max(finalBrakingRadius, 1.0f);

            entityManager.AddComponentData(blueprintEntity, new LevelEntityTag());

            // add blueprint to spawn in the ship
            entityManager.AddComponentData(blueprintEntity, new RosterShipBlueprint
            {
                SlotIndex = count++,
                ShipPrefab = matchedPrefab,
                BiomassCost = Mathf.RoundToInt(blueprint.GetStat(ShipStat.Cost)),

                ShipHealthComponent = new HealthComponent
                {
                    MaxValue = health,
                    Value = health
                },

                ShipMovementStats = new ShipMovementStatsComponent
                {
                    MaxSpeed = speed,
                    Acceleration = acceleration,
                    TurnSpeed = blueprint.GetStat(ShipStat.TurnSpeed),
                    BrakingDistance = finalBrakingRadius
                },

                ShipCollectionRadiusStat = new ShipCollectionRadiusComponent
                {
                    RadiusSq = collectionRadius * collectionRadius
                }
            });

            // Create weapon blueprints for each weapon mount on the ship
            var weaponBuffer = entityManager.AddBuffer<RosterWeaponBlueprint>(blueprintEntity);
            for (int m = 0; m < blueprint.Mounts.Count; m++)
            {
                var mount = blueprint.Mounts[m];
                WeaponData weapon = mount.EquippedWeapon;
                if (weapon == null) continue;

                Entity weaponPrefab = GetPrefabFromDatabase(weaponDB, weapon.WeaponID);

                float range = weapon.GetStat(WeaponStat.Range);
                float explosionRadius = weapon.GetStat(WeaponStat.ExplosionRadius);

                // add blueprint to spawn weapon
                weaponBuffer.Add(new RosterWeaponBlueprint
                {
                    WeaponPrefab = weaponPrefab,
                    Position = mount.Position,

                    WeaponTargetingStats = new WeaponTargetingStats
                    {
                        IgnoreObstacles = weapon.IgnoreObstacles,
                        Range = weapon.GetStat(WeaponStat.Range),
                        RangeSq = range * range,
                        DensityBias = weapon.DensityBias,
                        AngleBias = weapon.AngleBias,
                        DistanceBias = weapon.DistanceBias,
                    },

                    WeaponCombatStats = new WeaponCombatStats
                    {
                        Damage = weapon.GetStat(WeaponStat.Damage),
                        FireRate = weapon.GetStat(WeaponStat.FireRate),
                        Spread = weapon.GetStat(WeaponStat.Spread),
                        IgnoreObstacles = weapon.IgnoreObstacles,
                        ProjectileSpeed = weapon.GetStat(WeaponStat.ProjectileSpeed),
                        ProjectileTurnSpeed = weapon.GetStat(WeaponStat.ProjectileTurnSpeed),
                        HitRadius = weapon.GetStat(WeaponStat.HitRadius),
                        ExplosionRadiusSq = explosionRadius * explosionRadius,
                        PierceCount = Mathf.RoundToInt(weapon.GetStat(WeaponStat.PierceCount)),
                        TriggerTime = weapon.GetStat(WeaponStat.TriggerTime),
                    }
                });
            }
        }

        shipDB.Dispose();
        weaponDB.Dispose();

        EntityManager CreateMothershipBlueprint(EntityManager entityManager, NativeArray<BakedWeaponData> weaponDB)
        {
            ShipBlueprintData mBlueprint = playerSaveData.Mothership;
            Entity mBlueprintEntity = entityManager.CreateEntity();

            // Calculate final ship stats
            float mHealth = mBlueprint.GetStat(ShipStat.MaxHealth);
            float mCollectionRadius = mBlueprint.GetStat(ShipStat.CollectionRadius);

            entityManager.AddComponentData(mBlueprintEntity, new LevelEntityTag());

            // add blueprint to spawn in the ship
            entityManager.AddComponentData(mBlueprintEntity, new MothershipBlueprint
            {
                ShipHealthComponent = new HealthComponent
                {
                    MaxValue = mHealth,
                    Value = mHealth
                },

                ShipCollectionRadiusStat = new ShipCollectionRadiusComponent
                {
                    RadiusSq = mCollectionRadius * mCollectionRadius
                }
            });

            // Create weapon blueprints for each weapon mount on the ship
            var mWeaponBuffer = entityManager.AddBuffer<RosterWeaponBlueprint>(mBlueprintEntity);
            for (int m = 0; m < mBlueprint.Mounts.Count; m++)
            {
                var mount = mBlueprint.Mounts[m];
                WeaponData weapon = mount.EquippedWeapon;
                if (weapon == null) continue;

                Entity weaponPrefab = GetPrefabFromDatabase(weaponDB, weapon.WeaponID);

                float range = weapon.GetStat(WeaponStat.Range);
                float explosionRadius = weapon.GetStat(WeaponStat.ExplosionRadius);

                // add blueprint to spawn weapon
                mWeaponBuffer.Add(new RosterWeaponBlueprint
                {
                    WeaponPrefab = weaponPrefab,
                    Position = mount.Position,

                    WeaponTargetingStats = new WeaponTargetingStats
                    {
                        IgnoreObstacles = weapon.IgnoreObstacles,
                        Range = weapon.GetStat(WeaponStat.Range),
                        RangeSq = range * range,
                        DensityBias = weapon.DensityBias,
                        AngleBias = weapon.AngleBias,
                        DistanceBias = weapon.DistanceBias,
                    },

                    WeaponCombatStats = new WeaponCombatStats
                    {
                        Damage = weapon.GetStat(WeaponStat.Damage),
                        FireRate = weapon.GetStat(WeaponStat.FireRate),
                        Spread = weapon.GetStat(WeaponStat.Spread),
                        IgnoreObstacles = weapon.IgnoreObstacles,
                        ProjectileSpeed = weapon.GetStat(WeaponStat.ProjectileSpeed),
                        ProjectileTurnSpeed = weapon.GetStat(WeaponStat.ProjectileTurnSpeed),
                        HitRadius = weapon.GetStat(WeaponStat.HitRadius),
                        ExplosionRadiusSq = explosionRadius * explosionRadius,
                        PierceCount = Mathf.RoundToInt(weapon.GetStat(WeaponStat.PierceCount)),
                        TriggerTime = weapon.GetStat(WeaponStat.TriggerTime),
                    }
                });
            }

            return entityManager;
        }
    }

    LevelBlueprint ConvertLevelToComponent(EntityManager entityManager)
    {
        LevelNode levelToGenerate = playerSaveData.ActiveSector.SectorMap.CurrentLevel;

        // Copy relevant level generation details over to component
        return new LevelBlueprint
        {
            NodeType = levelToGenerate.NodeType,
            RewardType = levelToGenerate.RewardType,
            ThreatLevel = levelToGenerate.ThreatLevel,
            ObjectiveType = levelToGenerate.ObjectiveType,
            TerrainType = levelToGenerate.TerrainType,
            EnemyProfile = levelToGenerate.EnemyProfile,
            AnomalyType = levelToGenerate.AnomalyType,
            UpgradeCoreReward = levelToGenerate.UpgradeCoreReward,

            // Generate random seed
            Seed = (uint)System.DateTime.Now.Ticks
        };
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
