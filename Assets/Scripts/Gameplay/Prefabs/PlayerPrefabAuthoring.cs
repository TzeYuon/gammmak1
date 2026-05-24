using Unity.Entities;
using UnityEngine;

class PlayerPrefabAuthoring : MonoBehaviour
{
    public GameObject PlayerFighter;

    class Baker : Baker<PlayerPrefabAuthoring>
    {
        public override void Bake(PlayerPrefabAuthoring authoring)
        {
            // Get the prefab entities
            var fighterPrefab = GetEntity(authoring.PlayerFighter, TransformUsageFlags.None);

            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new PlayerFleetConfig
            {
                FighterPrefab = fighterPrefab,
                FighterCost = 10,
            });
        }
    }
}