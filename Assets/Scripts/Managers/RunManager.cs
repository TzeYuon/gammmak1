using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance;

    [SerializeField] private PlayerSaveData playerSaveData;
    public static PlayerSaveData SaveData { get { return Instance.playerSaveData; } }

    public MapGenerationConfig Sector1Config;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        StartNewRun();
    }

    public void StartNewRun()
    {
        Starmap generatedMap = MapGenerator.GenerateMap(Sector1Config);
        MapDecorator.DecorateSector(generatedMap.AllNodes, Sector1Config);

        // Clear previous run data
        SaveData.OnStartNewRun(generatedMap);
    }
}
