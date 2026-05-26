using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

class SelectionVisualAuthoring : MonoBehaviour
{
    class Baker : Baker<SelectionVisualAuthoring>
    {
        public override void Bake(SelectionVisualAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ShipReference()
            {
                Value = Entity.Null,
            });

            AddComponent(entity, new SelectionVisualComponent
            {
                Scale = authoring.transform.localScale.x,
            });
        }
    }
}

public struct SelectionVisualComponent : IComponentData
{
    public float Scale;
}