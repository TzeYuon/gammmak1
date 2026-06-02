using Unity.Entities;
using UnityEngine;

public class LevelHandoff : MonoBehaviour
{
    [SerializeField] private PlayerSaveData playerSaveData;

    private void Start()
    {
        GameStateSystem.OnStateChanged += OnStateChanged;
    }

    private void OnStateChanged(GameState prev, GameState cur)
    {
        if (cur == GameState.Gameplay) ConvertSaveToComponents();
    }

    public void ConvertSaveToComponents()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        var query = entityManager.CreateEntityQuery(typeof(BakedShipData), typeof(ActiveRosterSlot));
        if (query.IsEmptyIgnoreFilter)
        {
            Debug.LogError("Could not find the Ship Database in the ECS World");
            return;
        }

        Entity databaseEntity = query.GetSingletonEntity();

        // Prepare roster for next level
        var rosterBuffer = entityManager.GetBuffer<ActiveRosterSlot>(databaseEntity);
        rosterBuffer.Clear();

        // Get database
        var databaseBuffer = entityManager.GetBuffer<BakedShipData>(databaseEntity);

        // fill up roster based on database
        int count = 0;
        for (int i = 0; i < playerSaveData.ActiveRoster.Length; i++)
        {
            ShipBlueprintData blueprint = playerSaveData.ActiveRoster[i];
            if (blueprint == null) continue;

            Entity matchedPrefab = Entity.Null;
            foreach (var bakedShip in databaseBuffer)
            {
                if (bakedShip.ShipID == blueprint.ShipID)
                {
                    matchedPrefab = bakedShip.PrefabEntity;
                    break;
                }
            }

            if (matchedPrefab != Entity.Null)
            {
                rosterBuffer.Add(new ActiveRosterSlot
                {
                    SlotIndex = count++,
                    ShipID = blueprint.ShipID,
                    PrefabEntity = matchedPrefab,
                    BiomassCost = blueprint.CostToSpawn,
                    MaxSpawns = blueprint.MaxSpawnsPerLevel
                });
            }
        }
    }
}
