using Unity.Mathematics;

public struct LaserEvent
{
    public float3 Midpoint; // 12 bytes
    public float Length;    // 4 bytes
    public float4 Color;    // 16 bytes
    public float AngleZ;    // 4 bytes
    public float3 Padding;  // 12 bytes padding to reach 48 bytes total
}