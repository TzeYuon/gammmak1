using Unity.Entities;
using Unity.Mathematics;

public struct VFXTrailEmitter : IComponentData
{
    public float3 LastDropPosition;
    public float DropDistanceSq;
    public float4 Colour;
    public float Size;
    public float Duration;

    public bool IsInitialized;
}
