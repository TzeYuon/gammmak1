using Unity.Entities;
using UnityEngine;

class PlayerPrefabAuthoring : MonoBehaviour
{
    public GameObject PlayerFrigate;
    public GameObject PlayerDestroyer;
    public GameObject PlayerCruiser;

    class Baker : Baker<PlayerPrefabAuthoring>
    {
        public override void Bake(PlayerPrefabAuthoring authoring)
        {
            // Get the prefab entities
            var frigate = GetEntity(authoring.PlayerFrigate, TransformUsageFlags.None);
            var destroyer = GetEntity(authoring.PlayerDestroyer, TransformUsageFlags.None);
            var cruiser = GetEntity(authoring.PlayerCruiser, TransformUsageFlags.None);

            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new PlayerFleetConfig
            {
                FrigatePrefab = frigate,
                FrigateCost = 10,
                DestroyerPrefab = destroyer,
                DestroyerCost = 30,
                CruiserPrefab = cruiser,
                CruiserCost = 50,
            });
        }
    }
}