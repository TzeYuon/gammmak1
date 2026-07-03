using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    [Header("Fog Settings")]
    public float fogAlpha = 1f; // 1 = pitch black, 0 = transparent
    public Color fogColor = Color.black;
    public Material fogMaterial;
    public byte MaxFogDarkness = 255;

    [HideInInspector] public NativeArray<Color32> fogVisibilityArray;
    private Texture2D fogTexture;
    private GameObject fogQuad;

    public int width, height;
    public Vector2Int origin;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void InitializeFog(int gridWidth, int gridHeight, Vector2Int worldOrigin)
    {
        width = gridWidth;
        height = gridHeight;
        origin = worldOrigin;

        int totalCells = width * height;

        if (fogVisibilityArray.IsCreated) fogVisibilityArray.Dispose();
        fogVisibilityArray = new NativeArray<Color32>(totalCells, Allocator.Persistent);

        for (int i = 0; i < totalCells; i++)
        {
            fogVisibilityArray[i] = new Color32(0, 0, 0, MaxFogDarkness);
        }

        fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        fogTexture.filterMode = FilterMode.Bilinear;
        fogTexture.wrapMode = TextureWrapMode.Clamp;

        if (fogQuad == null)
        {
            fogQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fogQuad.name = "FogOfWar_Overlay";
            Destroy(fogQuad.GetComponent<MeshCollider>());
            fogQuad.GetComponent<MeshRenderer>().material = fogMaterial;
        }

        fogQuad.transform.position = new Vector3(origin.x + (width / 2f), origin.y + (height / 2f), -5f);
        fogQuad.transform.localScale = new Vector3(width, height, 1);

        fogMaterial.SetTexture("_MainTex", fogTexture);
        fogMaterial.SetColor("_BaseColor", new Color(fogColor.r, fogColor.g, fogColor.b, fogAlpha));
    }

    private void LateUpdate()
    {
        if (!fogVisibilityArray.IsCreated || fogTexture == null) return;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        world.EntityManager.CompleteAllTrackedJobs();

        fogTexture.SetPixelData(fogVisibilityArray, 0);
        fogTexture.Apply();

        FadeFog();
    }
    // Set fog to fade to 200 over time
    private void FadeFog()
    {
        for (int i = 0; i < fogVisibilityArray.Length; i++)
        {
            byte currentAlpha = fogVisibilityArray[i].a;
            if (currentAlpha < 200)
            {
                fogVisibilityArray[i] = new Color32(0, 0, 0, (byte)(currentAlpha + 1));
            }
        }
    }

    private void OnDestroy()
    {
        if (fogVisibilityArray.IsCreated) fogVisibilityArray.Dispose();
        if (fogTexture != null) Destroy(fogTexture);
    }
}