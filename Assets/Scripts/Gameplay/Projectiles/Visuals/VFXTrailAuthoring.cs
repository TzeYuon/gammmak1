using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class VFXTrailAuthoring : MonoBehaviour
{
    public Color Colour;
    public float DropDistance;
    public float Size;
    public float Duration;

    class VFXTrailAuthoringBaker : Baker<VFXTrailAuthoring>
    {
        public override void Bake(VFXTrailAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new VFXTrailEmitter
            {
                DropDistanceSq = authoring.DropDistance * authoring.DropDistance,
                Colour = new(authoring.Colour.r, authoring.Colour.g, authoring.Colour.b, authoring.Colour.a),
                Size = authoring.Size,
                Duration = authoring.Duration,
                IsInitialized = false
            });
        }
    }
}
