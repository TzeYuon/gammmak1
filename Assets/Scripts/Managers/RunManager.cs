using System;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance;

    [SerializeField] private PlayerSaveData playerSaveData;
    public static PlayerSaveData SaveData { get { return Instance.playerSaveData; } }

    [SerializeField] private LootDatabase lootDB;
    public static LootDatabase LootDB { get { return Instance.lootDB; } }

    public MapGenerationConfig Sector1Config;

    public static Action OnRunStart;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartNewRun()
    {
        Starmap generatedMap = MapGenerator.GenerateMap(Sector1Config);
        MapDecorator.DecorateSector(generatedMap.AllNodes, Sector1Config);

        // Clear previous run data
        SaveData.OnStartNewRun(generatedMap);
        lootDB.Init(SaveData);

        OnRunStart?.Invoke();
    }
}
