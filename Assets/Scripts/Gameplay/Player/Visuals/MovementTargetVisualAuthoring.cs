using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Used to show movement target of selected ships after right click is released
/// </summary>
class MovementTargetVisualAuthoring : MonoBehaviour
{
    class Baker : Baker<MovementTargetVisualAuthoring>
    {
        public override void Bake(MovementTargetVisualAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ShipReference()
            {
                Value = Entity.Null,
            });

            AddComponent<NeedsShipReferenceTag>(entity);

            AddComponent(entity, new MovementTargetVisualComponent
            {
                Scale = authoring.transform.localScale.x,
            });
        }
    }
}

public struct MovementTargetVisualComponent : IComponentData
{
    public float Scale;
}