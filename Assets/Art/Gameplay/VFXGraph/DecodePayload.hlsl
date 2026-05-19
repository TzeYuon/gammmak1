void FloatFunction(ByteAddressBuffer buffer, uint index, out float3 position, out uint type, out float4 colour, out float4 customData)
{
    uint offset = index * 48;
    position = asfloat(buffer.Load3(offset));
    type = buffer.Load(offset + 12);
    colour = asfloat(buffer.Load4(offset + 16));
    customData = asfloat(buffer.Load4(offset + 32));
}