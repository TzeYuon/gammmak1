using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class FogOfWarRenderSystem : SystemBase
{
    private Texture2D fogTexture;
    private Mesh quadMesh;

    protected override void OnCreate()
    {
        RequireForUpdate<FogVisualsComponent>();

        // Create a Quad mesh
        quadMesh = new Mesh();
        quadMesh.vertices = new Vector3[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
        quadMesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1) };
        quadMesh.triangles = new int[] { 0, 1, 2, 2, 1, 3 };

        quadMesh.RecalculateBounds();
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<FogOfWarState>(out var fogState)) return;
        if (!SystemAPI.TryGetSingleton<TerrainGridComponent>(out var terrainGrid)) return;
        var visuals = SystemAPI.ManagedAPI.GetSingleton<FogVisualsComponent>();

        int width = terrainGrid.GridWidth;
        int height = terrainGrid.GridHeight;

        if (fogTexture == null || fogTexture.width != width || fogTexture.height != height)
        {
            fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            fogTexture.filterMode = FilterMode.Bilinear;
            fogTexture.wrapMode = TextureWrapMode.Clamp;
            visuals.FogMaterial.SetTexture("_BaseMap", fogTexture);
        }

        Dependency.Complete();

        fogTexture.SetPixelData(fogState.FogArray, 0);
        fogTexture.Apply();

        float3 centerPos = new float3(terrainGrid.WorldOrigin.x + (width / 2f), terrainGrid.WorldOrigin.y + (height / 2f), -5f);
        Matrix4x4 matrix = Matrix4x4.TRS(centerPos, Quaternion.identity, new Vector3(width, height, 1));

        Graphics.DrawMesh(quadMesh, matrix, visuals.FogMaterial, 0);
    }
}