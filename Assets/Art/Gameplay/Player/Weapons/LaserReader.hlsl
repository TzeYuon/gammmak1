void ReadLaserBuffer_float(ByteAddressBuffer buffer, uint index, out float3 midpoint, out float length, out float4 color, out float angle)
{
    uint offset = index * 48;

    midpoint.x = asfloat(buffer.Load(offset + 0));
    midpoint.y = asfloat(buffer.Load(offset + 4));
    midpoint.z = asfloat(buffer.Load(offset + 8));
    
    length = asfloat(buffer.Load(offset + 12));
    
    color.x = asfloat(buffer.Load(offset + 16));
    color.y = asfloat(buffer.Load(offset + 20));
    color.z = asfloat(buffer.Load(offset + 24));
    color.w = asfloat(buffer.Load(offset + 28));
    
    angle = asfloat(buffer.Load(offset + 32));
}