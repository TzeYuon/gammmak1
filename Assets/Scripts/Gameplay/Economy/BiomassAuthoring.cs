using UnityEngine;
using Unity.Entities;

public class BiomassAuthoring : MonoBehaviour
{
    class Baker : Baker<BiomassAuthoring>
    {
        public override void Bake(BiomassAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new BiomassLoot());
        }
    }
}
