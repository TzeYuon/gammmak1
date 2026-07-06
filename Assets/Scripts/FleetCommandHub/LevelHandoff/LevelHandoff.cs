using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

public class LevelHandoff : MonoBehaviour
{
    [SerializeField] private PlayerSaveData playerSaveData;
    [SerializeField] private Material ghostMaterial;

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
        LevelNode currentLevel = playerSaveData.ActiveSector.SectorMap.CurrentLevel;
        LevelBlueprint levelBlueprint = ConvertLevelToComponent(entityManager, currentLevel);
        entityManager.AddComponentData(generatingStatus, levelBlueprint);

        // Convert enemy profile to buffer
        entityManager.AddBuffer<HiveBlueprint>(generatingStatus);
        foreach (var archetype in currentLevel.EnemyProfile.PossibleHives.Pool)
        {
            if (archetype.Weight <= 0) continue;

            Entity recipeEntity = entityManager.CreateEntity();

            var trickleBuffer = entityManager.AddBuffer<TrickleSpawnerData>(recipeEntity);
            foreach (var trickle in archetype.Item.TrickleSpawns)
            {
                trickleBuffer.Add(new TrickleSpawnerData
                {
                    Type = trickle.Type,
                    SpawnCount = trickle.SpawnCount,
                    Interval = trickle.Interval,
                    NextSpawnTime = trickle.Interval,
                });
            }

            var buffer = entityManager.GetBuffer<HiveBlueprint>(generatingStatus);
            buffer.Add(new HiveBlueprint
            {
                Weight = archetype.Weight,
                RecipeEntity = recipeEntity,
                HiveSurgeComponent = new HiveSurgeComponent
                {
                    SurgeEnemyType = archetype.Item.SurgeType,
                    SurgeSpawnCount = archetype.Item.SurgeCount,
                    SurgeSpawnRate = 100,
                    SurgeSpawnAccumulator = 0,
                    SurgePendingSpawns = 0,
                }
            });
        }

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
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

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

        // Prepare variables for ghost
        var shipVisualDB = entityManager.GetComponentData<ShipVisualDatabase>(databaseEntity);
        Entity ghostPrefabEntity = shipVisualDB.GhostVisualPrefab;
        Entity arcPrefabEntity = shipVisualDB.ArcVisualPrefab;
        var graphicsSystem = world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
        BatchMaterialID hologramMatID = graphicsSystem.RegisterMaterial(ghostMaterial);

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

            // Create ghost for movement visual
            entityManager = CreateGhostPrefab(entityManager, blueprint, out Entity ghostPrefab);

            // add blueprint to spawn in the ship
            entityManager.AddComponentData(blueprintEntity, new RosterShipBlueprint
            {
                SlotIndex = count++,
                ShipPrefab = matchedPrefab,
                GhostEntity = ghostPrefab,
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
            entityManager = CreateWeaponBlueprints(entityManager, weaponDB, blueprint, blueprintEntity);
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
            return CreateWeaponBlueprints(entityManager, weaponDB, mBlueprint, mBlueprintEntity);
        }

        EntityManager CreateWeaponBlueprints(EntityManager entityManager, NativeArray<BakedWeaponData> weaponDB, ShipBlueprintData blueprint, Entity blueprintEntity)
        {
            var weaponBuffer = entityManager.AddBuffer<RosterWeaponBlueprint>(blueprintEntity);
            for (int m = 0; m < blueprint.Mounts.Count; m++)
            {
                var mount = blueprint.Mounts[m];
                WeaponData weapon = mount.EquippedWeapon;
                if (weapon == null) continue;

                Entity weaponPrefab = GetPrefabFromDatabase(weaponDB, weapon.WeaponID);

                float range = weapon.GetStat(WeaponStat.Range);
                float explosionRadius = weapon.GetStat(WeaponStat.ExplosionRadius);
                float projectileSpeed = weapon.GetStat(WeaponStat.ProjectileSpeed);
  
                // add blueprint to spawn weapon
                weaponBuffer.Add(new RosterWeaponBlueprint
                {
                    WeaponPrefab = weaponPrefab,
                    Position = mount.Position,
                    OriginalRotation = mount.OriginalRotation,

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
                        ProjectileCount = weapon.GetStatInt(WeaponStat.ProjectileCount),
                        ProjectileSpeed = weapon.GetStat(WeaponStat.ProjectileSpeed),
                        ProjectileTurnSpeed = weapon.GetStat(WeaponStat.ProjectileTurnSpeed),
                        HitRadius = weapon.GetStat(WeaponStat.HitRadius),
                        ExplosionRadiusSq = explosionRadius * explosionRadius,
                        PierceCount = Mathf.RoundToInt(weapon.GetStat(WeaponStat.PierceCount)),
                        TriggerTime = weapon.GetStat(WeaponStat.TriggerTime),
                        ProjectileLifetime = range / projectileSpeed,
                    },

                    WeaponFiringArc = new WeaponFiringArc
                    {
                        RestingRotation = mount.OriginalRotation,
                        MaxAngle = mount.MaxAngle,
                    }
                });
            }

            return entityManager;
        }
    
        EntityManager CreateGhostPrefab(EntityManager entityManager, ShipBlueprintData blueprint, out Entity ghostPrefab)
        {
            ghostPrefab = entityManager.Instantiate(ghostPrefabEntity);

            if (blueprint.ShipMesh != null)
            {
                BatchMeshID ghostMeshID = graphicsSystem.RegisterMesh(blueprint.ShipMesh);
                entityManager.SetComponentData(ghostPrefab, new MaterialMeshInfo
                {
                    MeshID = ghostMeshID,
                    MaterialID = hologramMatID,
                    SubMesh = 0
                });
            }
            else
            {
                Debug.LogWarning($"[Handoff] {blueprint.ShipName} is missing a ShipMesh in its ScriptableObject!");
            }

            for (int m = 0; m < blueprint.Mounts.Count; m++)
            {
                var mount = blueprint.Mounts[m];
                if (mount.EquippedWeapon == null) continue;

                // Spawn the arc visual
                Entity arcVisual = entityManager.Instantiate(arcPrefabEntity);
                entityManager.AddComponentData(arcVisual, new Parent { Value = ghostPrefab });
                entityManager.AddComponentData(arcVisual, LocalTransform.FromPositionRotation((Vector3)mount.Position, mount.OriginalRotation));

                entityManager.AddComponentData(arcVisual, new ArcAngleOverride
                {
                    Value = mount.MaxAngle,
                });

                // add weapon arc to same group as the ghost
                if (!entityManager.HasBuffer<LinkedEntityGroup>(ghostPrefab))
                {
                    var newBuffer = entityManager.AddBuffer<LinkedEntityGroup>(ghostPrefab);
                    newBuffer.Add(new LinkedEntityGroup { Value = ghostPrefab });
                }

                var linkedGroup = entityManager.GetBuffer<LinkedEntityGroup>(ghostPrefab);
                linkedGroup.Add(new LinkedEntityGroup { Value = arcVisual });
            }

            return entityManager;
        }
    }

    LevelBlueprint ConvertLevelToComponent(EntityManager entityManager, LevelNode currentLevel)
    {
        // Copy relevant level generation details over to component
        return new LevelBlueprint
        {
            NodeType = currentLevel.NodeType,
            RewardType = currentLevel.RewardType,
            ThreatLevel = currentLevel.ThreatLevel,
            ObjectiveType = currentLevel.ObjectiveType,
            TerrainType = currentLevel.TerrainType,
            AnomalyType = currentLevel.AnomalyType,
            UpgradeCoreReward = currentLevel.UpgradeCoreReward,

            // Generate random seed
            Seed = (uint)System.DateTime.Now.Ticks,

            // Store enemy profile variables
            TotalHiveWeight = currentLevel.EnemyProfile.PossibleHives.TotalWeight,
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
