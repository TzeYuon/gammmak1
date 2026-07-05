using Unity.Entities;
using UnityEngine;

public class HideInFogAuthoring : MonoBehaviour
{
    class Baker : Baker<HideInFogAuthoring>
    {
        public override void Bake(HideInFogAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<HideInFogTag>(entity);
        }
    }
}

public struct HideInFogTag : IComponentData { }