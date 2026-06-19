using Unity.Entities;
using UnityEngine;

public class TerrainPrefabsAuthoring : MonoBehaviour
{
    public GameObject wallPrefab;
    public GameObject mudPrefab;
    public GameObject poisonPrefab;

    class Baker : Baker<TerrainPrefabsAuthoring>
    {
        public override void Bake(TerrainPrefabsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new TerrainPrefabsComponent
            {
                WallEntity = GetEntity(authoring.wallPrefab, TransformUsageFlags.Dynamic),
                MudEntity = GetEntity(authoring.mudPrefab, TransformUsageFlags.Dynamic),
                PoisonEntity = GetEntity(authoring.poisonPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }
}

public struct TerrainPrefabsComponent : IComponentData
{
    public Entity WallEntity;
    public Entity MudEntity;
    public Entity PoisonEntity;
}