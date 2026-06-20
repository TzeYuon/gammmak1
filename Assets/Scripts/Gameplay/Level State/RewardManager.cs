using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;

public partial class RewardManager : SystemBase
{
    private bool init = false;

    private PlayerSaveData saveData;
    private LootDatabase lootDB;

    public static event System.Action OnRewardGranted;

    protected override void OnDestroy()
    {
        GameStateSystem.OnStateChanged -= HandleStateChanged;
    }

    protected override void OnUpdate()
    {
        if (!init)
        {
            GameStateSystem.OnStateChanged += HandleStateChanged;
            saveData = RunManager.SaveData;
            lootDB = RunManager.LootDB;
            init = true;
        }
    }

    private void HandleStateChanged(GameState prev, GameState newState)
    {
        if (newState == GameState.Victory)
        {
            GenerateAndApplyReward();
        }
    }

    private void GenerateAndApplyReward()
    {
        if (!init) return;

        LevelNode currentNode = saveData.ActiveSector.CurrentNode;
        saveData.UpgradeCoresRemaining += currentNode.UpgradeCoreReward;
        switch (currentNode.RewardType)
        {
            case RewardType.UpgradeCores:
                saveData.UpgradeCoresRemaining += currentNode.UpgradeCoreReward;
                break;

            case RewardType.WeaponBlueprint:
                if (lootDB.DropableWeapons.Count == 0)
                {
                    saveData.UpgradeCoresRemaining += currentNode.UpgradeCoreReward;
                }
                else
                {
                    int wIndex = Random.Range(0, lootDB.DropableWeapons.Count);
                    WeaponData weapon = lootDB.DropableWeapons[wIndex];
                    lootDB.DropableWeapons.RemoveAt(wIndex);
                    saveData.AvailableWeapons.Add(weapon);
                }
                break;

            case RewardType.ShipBlueprint:
                if (lootDB.DropableShips.Count == 0)
                {
                    saveData.UpgradeCoresRemaining += currentNode.UpgradeCoreReward;
                }
                else
                {
                    int sIndex = Random.Range(0, lootDB.DropableShips.Count);
                    ShipBlueprintData ship = lootDB.DropableShips[sIndex];
                    lootDB.DropableShips.RemoveAt(sIndex);
                    saveData.AvailableShips.Add(ship);
                }
                break;
        }

        OnRewardGranted?.Invoke();
    }
}