using Unity.Entities;

[InternalBufferCapacity(4)]
public struct ActiveRosterSlot : IBufferElementData
{
    public int SlotIndex;
    public ShipID ShipID;
    public Entity PrefabEntity;
    public int BiomassCost;
}

[InternalBufferCapacity(20)]
public struct BakedShipData : IBufferElementData
{
    public ShipID ShipID;
    public Entity PrefabEntity;
}