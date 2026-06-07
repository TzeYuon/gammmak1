using UnityEngine;

public class PlayerSaveManager : MonoBehaviour
{
    public static PlayerSaveManager Instance;

    [SerializeField] private PlayerSaveData playerSaveData;
    public static PlayerSaveData PlayerSaveData { get { return Instance.playerSaveData; } }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
}
