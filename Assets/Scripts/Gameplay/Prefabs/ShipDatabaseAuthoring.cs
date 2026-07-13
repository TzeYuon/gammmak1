using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

class ShipDatabaseAuthoring : MonoBehaviour
{
    [Header("Ship prefabs")]
    [SerializeField] private GameObject mothershipPrefab;
    [SerializeField] private List<ShipBlueprintData> allGameShips;

    [Header("Weapon prefabs")]
    [SerializeField] private List<WeaponData> allGameWeapons;

    [Header("Ship visual prefabs")]
    [SerializeField] private GameObject ghostPrefab;
    [SerializeField] private GameObject arcVisualPrefab;
    [SerializeField] private GameObject shieldVisualPrefab;

    class Baker : Baker<ShipDatabaseAuthoring>
    {
        public override void Bake(ShipDatabaseAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            var mothershipEntity = GetEntity(authoring.mothershipPrefab, TransformUsageFlags.Dynamic);
            AddComponent(entity, new MothershipPrefabDatabase { MothershipPrefab = mothershipEntity });

            // Create the database buffer
            var shipDBBuffer = AddBuffer<BakedShipData>(entity);
            foreach (var shipSO in authoring.allGameShips)
            {
                if (shipSO != null && shipSO.ShipPrefab != null)
                {
                    Entity bakedPrefab = GetEntity(shipSO.ShipPrefab, TransformUsageFlags.Dynamic);
                    shipDBBuffer.Add(new BakedShipData
                    {
                        ShipID = shipSO.ShipID,
                        PrefabEntity = bakedPrefab
                    });
                }
            }

            var weaponDBBuffer = AddBuffer<BakedWeaponData>(entity);
            foreach (var weaponSO in authoring.allGameWeapons)
            {
                if (weaponSO != null && weaponSO.WeaponPrefab != null)
                {
                    Entity bakedPrefab = GetEntity(weaponSO.WeaponPrefab, TransformUsageFlags.Dynamic);
                    weaponDBBuffer.Add(new BakedWeaponData
                    {
                        WeaponID = weaponSO.WeaponID,
                        PrefabEntity = bakedPrefab
                    });
                }
            }

            AddComponent(entity, new ShipVisualDatabase
            {
                GhostVisualPrefab = GetEntity(authoring.ghostPrefab, TransformUsageFlags.Dynamic),
                ArcVisualPrefab = GetEntity(authoring.arcVisualPrefab, TransformUsageFlags.Dynamic),
                ShieldVisualPrefab = GetEntity(authoring.shieldVisualPrefab, TransformUsageFlags.Dynamic),
            });
        }
    }
}

public partial struct ShipVisualDatabase : IComponentData
{
    public Entity GhostVisualPrefab;
    public Entity ArcVisualPrefab;
    public Entity ShieldVisualPrefab;
}

public partial struct MothershipPrefabDatabase : IComponentData
{
    public Entity MothershipPrefab;
}