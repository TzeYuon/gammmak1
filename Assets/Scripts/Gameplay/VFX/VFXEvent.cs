using Unity.Mathematics;

public enum VFXType : uint
{
    DeathExplosion = 0,
    Laser = 1,
    Explosion = 2,
}

// Must be 48 bytes for HLSL
public struct VFXPayload
{
    public float3 Position; 
    public uint Type;        

    public float4 Colour;

    public float4 CustomData;
}