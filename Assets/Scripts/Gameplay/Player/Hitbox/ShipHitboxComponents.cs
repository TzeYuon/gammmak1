using Unity.Entities;
using Unity.Mathematics;

public enum HitboxShape : byte
{
    Circle,
    Rectangle,
    Triangle
}

[InternalBufferCapacity(4)] 
public struct Hitbox : IBufferElementData
{
    public HitboxShape Shape;
    public float2 LocalOffset;   
    public float LocalRotation;  
    public float2 Size;          // Circle (Radius, _), Rectangle (HalfWidth, HalfLength), Triangle (HalfBase, Height)
}

public struct HitboxCombined : IComponentData
{
    public float MaxBoundingRadiusSq; // The circle that encompasses all hitboxes
}