using Unity.Entities;
using UnityEngine;

/// <summary>
/// Replaces TerrainPrefabsComponent for rendering purposes. You no longer
/// need a prefab per tile type - just a flat-shaded material per type,
/// applied to the merged chunk meshes. Keep TerrainPrefabsComponent around
/// only if you still need per-tile prefabs for something else (e.g. a
/// separate collision setup) - it's no longer required by the renderer.
/// </summary>
public struct TerrainMaterialsComponent : IComponentData
{
    public UnityObjectRef<Material> WallMaterial;
    public UnityObjectRef<Material> MudMaterial;
    public UnityObjectRef<Material> PoisonMaterial;
}

public class TerrainMaterialsAuthoring : MonoBehaviour
{
    [Tooltip("Flat unlit material for wall/asteroid chunks.")]
    public Material WallMaterial;

    [Tooltip("Flat unlit material for mud hazard chunks.")]
    public Material MudMaterial;

    [Tooltip("Flat unlit material for poison hazard chunks.")]
    public Material PoisonMaterial;

    private class Baker : Baker<TerrainMaterialsAuthoring>
    {
        public override void Bake(TerrainMaterialsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new TerrainMaterialsComponent
            {
                WallMaterial = authoring.WallMaterial,
                MudMaterial = authoring.MudMaterial,
                PoisonMaterial = authoring.PoisonMaterial
            });
        }
    }
}