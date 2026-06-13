using UnityEngine;
using Starmap;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance;

    [SerializeField] private PlayerSaveData playerSaveData;
    public static PlayerSaveData SaveData { get { return Instance.playerSaveData; } }

    public MapConfig Sector1Config;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        StartNewRun();
    }

    public void StartNewRun()
    {
        var generatedMap = MapGenerator.GetMap(Sector1Config);

        // Clear previous run data
        SaveData.OnStartNewRun(generatedMap);
    }
}
