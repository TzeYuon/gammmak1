using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

class ShipDatabaseAuthoring : MonoBehaviour
{
    public List<ShipBlueprintData> AllGameShips;

    class Baker : Baker<ShipDatabaseAuthoring>
    {
        public override void Bake(ShipDatabaseAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            // Create the database buffer
            var dbBuffer = AddBuffer<BakedShipData>(entity);
            foreach (var shipSO in authoring.AllGameShips)
            {
                if (shipSO != null && shipSO.ShipPrefab != null)
                {
                    Entity bakedPrefab = GetEntity(shipSO.ShipPrefab, TransformUsageFlags.Dynamic);
                    dbBuffer.Add(new BakedShipData
                    {
                        ShipID = shipSO.ShipID,
                        PrefabEntity = bakedPrefab
                    });
                }
            }

            // Create empty Roster buffer
            AddBuffer<ActiveRosterSlot>(entity);
        }
    }
}