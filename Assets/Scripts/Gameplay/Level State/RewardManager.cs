using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;

public partial class RewardManager : SystemBase
{
    private bool init = false;

    private PlayerSaveData saveData;
    private LootDatabase lootDB;

    public static event System.Action<List<LevelReward>> OnRewardGranted;

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

        List<LevelReward> rewardList = new List<LevelReward>();

        LevelNode currentNode = saveData.ActiveSector.CurrentNode;
        saveData.UpgradeCoresRemaining += currentNode.UpgradeCoreReward;
        rewardList.Add(new LevelReward { 
            Type = RewardType.UpgradeCores, 
            UpgradeCores = currentNode.UpgradeCoreReward 
        });

        switch (currentNode.RewardType)
        {
            case RewardType.UpgradeCores:
                saveData.UpgradeCoresRemaining += currentNode.UpgradeCoreReward;
                rewardList.Add(new LevelReward { 
                    Type = RewardType.UpgradeCores, 
                    UpgradeCores = currentNode.UpgradeCoreReward 
                });
                break;

            case RewardType.WeaponBlueprint:
                if (lootDB.DropableWeapons.Count == 0)
                {
                    saveData.UpgradeCoresRemaining += currentNode.UpgradeCoreReward;
                    rewardList.Add(new LevelReward { 
                        Type = RewardType.UpgradeCores, 
                        UpgradeCores = currentNode.UpgradeCoreReward,
                        Details = "Duplicate blueprint recycled"
                    });
                }
                else
                {
                    int wIndex = Random.Range(0, lootDB.DropableWeapons.Count);
                    WeaponData weapon = lootDB.DropableWeapons[wIndex];
                    lootDB.DropableWeapons.RemoveAt(wIndex);
                    saveData.AvailableWeapons.Add(weapon);

                    rewardList.Add(new LevelReward { 
                        Type = RewardType.WeaponBlueprint, 
                        WeaponBlueprint = weapon 
                    });
                }
                break;

            case RewardType.ShipBlueprint:
                if (lootDB.DropableShips.Count == 0)
                {
                    saveData.UpgradeCoresRemaining += currentNode.UpgradeCoreReward;
                    rewardList.Add(new LevelReward { 
                        Type = RewardType.UpgradeCores, 
                        UpgradeCores = currentNode.UpgradeCoreReward,
                        Details = "Duplicate blueprint recycled"
                    });
                }
                else
                {
                    int sIndex = Random.Range(0, lootDB.DropableShips.Count);
                    ShipBlueprintData ship = lootDB.DropableShips[sIndex];
                    lootDB.DropableShips.RemoveAt(sIndex);
                    saveData.AvailableShips.Add(ship);

                    rewardList.Add(new LevelReward { 
                        Type = RewardType.ShipBlueprint, 
                        ShipBlueprint = ship 
                    });
                }
                break;
        }

        OnRewardGranted?.Invoke(rewardList);
    }
}

public class LevelReward
{
    public string Details;
    public RewardType Type;
    public int UpgradeCores;
    public WeaponData WeaponBlueprint;
    public ShipBlueprintData ShipBlueprint;
}