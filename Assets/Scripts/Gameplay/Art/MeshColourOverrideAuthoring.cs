using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

public class MeshColourOverrideAuthoring : MonoBehaviour
{
    [SerializeField] Color colour;

    class Baker : Baker<MeshColourOverrideAuthoring>
    {
        public override void Bake(MeshColourOverrideAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new MeshMaterialOverride
            {
                Value = new(
                    authoring.colour.r,
                    authoring.colour.g,
                    authoring.colour.b,
                    authoring.colour.a
                    )
            });
        }
    }
}

[MaterialProperty("_Colour")]
public struct MeshMaterialOverride : IComponentData
{
    public float4 Value;
}