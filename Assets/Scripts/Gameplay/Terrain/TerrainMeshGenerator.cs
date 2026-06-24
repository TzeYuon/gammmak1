using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainMeshGenerator : MonoBehaviour
{
    public static TerrainMeshGenerator Instance { get; private set; }

    [Header("Assign Your Materials Here")]
    public Material wallMaterial;
    public Material mudMaterial;
    public Material poisonMaterial;
    public Material outlineMaterial;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private void Awake()
    {
        if (Instance == null)
        { 
            Instance = this;
        }

        else
        {
            Destroy(gameObject);
            return;
        }

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    public static TerrainMeshGenerator GetInstance()
    {
        if (Instance == null)
        {
            TerrainMeshGenerator generator = FindAnyObjectByType<TerrainMeshGenerator>();
            if (generator != null)
            {
                generator.Awake();
            }
        }
        return Instance;
    }

    public void BuildTerrainMesh(int[,] grid, int width, int height, int2 worldOrigin)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        // Separate lists so we can assign different materials to each terrain type
        List<int> wallTriangles = new List<int>();
        List<int> mudTriangles = new List<int>();
        List<int> poisonTriangles = new List<int>();
        List<int> outlineTriangles = new List<int>();

        int vertexIndex = 0;
        float lineThickness = 0.15f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int tileType = grid[x, y];
                if (tileType == 0) continue; // Empty space

                // Center the quad
                Vector3 pos = new Vector3(x + worldOrigin.x, y + worldOrigin.y, 0);

                List<int> activeTriangles = wallTriangles;
                if (tileType == 2) activeTriangles = mudTriangles;
                if (tileType == 3) activeTriangles = poisonTriangles;

                AddQuad(pos, new Vector2(1f, 1f), activeTriangles, vertices, uvs, ref vertexIndex);

                if (tileType == 1)
                {
                    // Check Top
                    if (y == height - 1 || grid[x, y + 1] != 1)
                        AddQuad(pos + new Vector3(0, 0.5f, 0), new Vector2(1f, lineThickness), outlineTriangles, vertices, uvs, ref vertexIndex);

                    // Check Bottom
                    if (y == 0 || grid[x, y - 1] != 1)
                        AddQuad(pos + new Vector3(0, -0.5f, 0), new Vector2(1f, lineThickness), outlineTriangles, vertices, uvs, ref vertexIndex);

                    // Check Right
                    if (x == width - 1 || grid[x + 1, y] != 1)
                        AddQuad(pos + new Vector3(0.5f, 0, 0), new Vector2(lineThickness, 1f), outlineTriangles, vertices, uvs, ref vertexIndex);

                    // Check Left
                    if (x == 0 || grid[x - 1, y] != 1)
                        AddQuad(pos + new Vector3(-0.5f, 0, 0), new Vector2(lineThickness, 1f), outlineTriangles, vertices, uvs, ref vertexIndex);
                }
            }
        }


        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();

        // Tell Unity this mesh has only 4 materials
        mesh.subMeshCount = 4;
        mesh.SetTriangles(wallTriangles.ToArray(), 0);
        mesh.SetTriangles(mudTriangles.ToArray(), 1);
        mesh.SetTriangles(poisonTriangles.ToArray(), 2);
        mesh.SetTriangles(outlineTriangles.ToArray(), 3); 

        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        meshRenderer.materials = new Material[] { wallMaterial, mudMaterial, poisonMaterial, outlineMaterial };
    }

    private void AddQuad(Vector3 center, Vector2 size, List<int> triangles, List<Vector3> verts, List<Vector2> uvs, ref int vIndex)
    {
        verts.Add(center + new Vector3(-size.x / 2, -size.y / 2, -0.1f));
        verts.Add(center + new Vector3(-size.x / 2, size.y / 2, -0.1f));
        verts.Add(center + new Vector3(size.x / 2, -size.y / 2, -0.1f));
        verts.Add(center + new Vector3(size.x / 2, size.y / 2, -0.1f));

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));

        triangles.Add(vIndex + 0);
        triangles.Add(vIndex + 1);
        triangles.Add(vIndex + 2);
        triangles.Add(vIndex + 2);
        triangles.Add(vIndex + 1);
        triangles.Add(vIndex + 3);

        vIndex += 4;
    }
}