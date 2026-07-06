using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public class HangarShipPreviewManager : MonoBehaviour
{
    [SerializeField] Transform SpawnPoint;
    [SerializeField] Camera hangarCamera;

    public float CameraSize { get { return hangarCamera.orthographicSize; } }
    private EntityManager entityManager;
    private Entity currentVisualEntity = Entity.Null;

    private void Awake()
    {
        GameStateSystem.OnStateChanged += OnGameStateChange;
    }

    private void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    private void OnDestroy()
    {
        GameStateSystem.OnStateChanged -= OnGameStateChange;
    }

    void OnGameStateChange(GameState prev, GameState gameState)
    {
        if (gameState == GameState.FleetCommandHub)
        {
            hangarCamera.gameObject.SetActive(true);
        }
        else
        {
            hangarCamera.gameObject.SetActive(false);
        }
    }

    public void DisplayShip(ShipID shipID)
    {
        // Get ship database
        var query = entityManager.CreateEntityQuery(typeof(BakedShipData), typeof(BakedWeaponData));
        if (query.IsEmptyIgnoreFilter)
        {
            Debug.LogError("Could not find the Ship Database in the ECS World");
            return;
        }

        Entity databaseEntity = query.GetSingletonEntity();
        var shipDB = entityManager.GetBuffer<BakedShipData>(databaseEntity).ToNativeArray(Allocator.Temp);

        // remove previous preview
        if (entityManager.Exists(currentVisualEntity))
        {
            entityManager.DestroyEntity(currentVisualEntity);
            currentVisualEntity = Entity.Null;
        }

        if (shipID == ShipID.None) return;

        // spawn new ship preview
        currentVisualEntity = entityManager.Instantiate(GetPrefabFromDatabase(shipDB, shipID));
        entityManager.SetComponentData(currentVisualEntity, LocalTransform.FromPosition(
            SpawnPoint.position));

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
    }
}
