using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

class ShipDatabaseAuthoring : MonoBehaviour
{
    public List<ShipBlueprintData> AllGameShips;
    public List<WeaponData> AllGameWeapons;

    class Baker : Baker<ShipDatabaseAuthoring>
    {
        public override void Bake(ShipDatabaseAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            // Create the database buffer
            var shipDBBuffer = AddBuffer<BakedShipData>(entity);
            foreach (var shipSO in authoring.AllGameShips)
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
            foreach (var weaponSO in authoring.AllGameWeapons)
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
        }
    }
}