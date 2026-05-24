using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

class BiomassPrefabsAuthoring : MonoBehaviour
{
    public GameObject BiomassPrefab;

    class Baker : Baker<BiomassPrefabsAuthoring>
    {
        public override void Bake(BiomassPrefabsAuthoring authoring)
        {
            // Get the biomass prefab entity
            var biomassPrefab = GetEntity(authoring.BiomassPrefab, TransformUsageFlags.Renderable);

            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new BiomassConfig
            {
                BiomassPrefab = biomassPrefab,
            });
        }
    }
}