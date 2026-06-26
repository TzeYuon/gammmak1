using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
partial struct LevelGen_PlayerFleetSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameStateSingleton>();
        state.RequireForUpdate<LevelGenerationStatus>();
        state.RequireForUpdate<PlayerEconomy>();
        state.RequireForUpdate<MothershipPrefabDatabase>();
        state.RequireForUpdate<MothershipBlueprint>();
        state.RequireForUpdate<LevelSpawnData>();
    }

    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GameStateSingleton>(out var gameState) ||
            gameState.CurrentState != GameState.Generating) return;

        if (!SystemAPI.TryGetSingletonRW<LevelGenerationStatus>(out var status)) return;
        if (status.ValueRO.PlayerFleetSpawned) return; // player fleet already spawned in

        if (!SystemAPI.TryGetSingletonRW<PlayerEconomy>(out var playerEconomy)) return;

        if (!SystemAPI.TryGetSingleton<MothershipPrefabDatabase>(out var prefabDatabase)) return;
        if (!SystemAPI.TryGetSingletonEntity<MothershipBlueprint>(out var mothershipBlueprintEntity)) return;
        if (!SystemAPI.TryGetSingleton<MothershipBlueprint>(out var mothershipBlueprint)) return;

        var weaponBufferLookup = SystemAPI.GetBufferLookup<RosterWeaponBlueprint>(true);
        if (!weaponBufferLookup.HasBuffer(mothershipBlueprintEntity)) return;

        // mark as complete
        status.ValueRW.PlayerFleetSpawned = true;

        playerEconomy.ValueRW.TotalBiomass = 50;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(state.WorldUnmanaged);

        // Generate mothership
        Entity mothershipEntity = ecb.Instantiate(prefabDatabase.MothershipPrefab);
        var spawnData = SystemAPI.GetSingleton<LevelSpawnData>();
        ecb.SetComponent(mothershipEntity, LocalTransform.FromPosition(spawnData.MothershipSpawn));
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SnapToPosition(spawnData.MothershipSpawn);
        }

        // Set ship stats
        ecb.SetComponent(mothershipEntity, mothershipBlueprint.ShipHealthComponent);
        ecb.SetComponent(mothershipEntity, mothershipBlueprint.ShipCollectionRadiusStat);

        // spawn weapons
        var weaponBuffer = weaponBufferLookup[mothershipBlueprintEntity];
        for (int w = 0; w < weaponBuffer.Length; w++)
        {
            var weaponBlueprint = weaponBuffer[w];

            Entity weaponPrefab = weaponBlueprint.WeaponPrefab;
            if (weaponPrefab == Entity.Null) continue;

            Entity weapon = ecb.Instantiate(weaponPrefab);
            ecb.AddComponent(weapon, new Parent { Value = mothershipEntity });
            ecb.SetComponent(weapon, LocalTransform.FromPosition(new float3(weaponBlueprint.Position, 0)));

            // Set weapon stats
            ecb.SetComponent(weapon, weaponBlueprint.WeaponTargetingStats);
            ecb.SetComponent(weapon, weaponBlueprint.WeaponCombatStats);

            ecb.AppendToBuffer(mothershipEntity, new LinkedEntityGroup { Value = weapon });
        }
    }
}
