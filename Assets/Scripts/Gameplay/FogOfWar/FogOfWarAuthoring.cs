using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class FogOfWarAuthoring : MonoBehaviour
{
    public Material FogMaterial;
    public byte MemFogValue = 200;
    class FogOfWarBaker : Baker<FogOfWarAuthoring>
    {
        public override void Bake(FogOfWarAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            AddComponentObject(entity, new FogVisualsComponent
            { 
                FogMaterial = authoring.FogMaterial 
            });

            AddComponent(entity, new FogOfWarSettings
            {
                MemFogValue = authoring.MemFogValue,
            });
        }
    }
}

public class FogVisualsComponent : IComponentData
{
    public Material FogMaterial;
}

public struct FogOfWarSettings : IComponentData
{
    public int MemFogValue;
}

public struct FogOfWarState : IComponentData
{
    public NativeArray<Color32> FogArray;
}