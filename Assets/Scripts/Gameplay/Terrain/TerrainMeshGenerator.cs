using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainMeshGenerator : MonoBehaviour
{
    public static TerrainMeshGenerator Instance { get; private set; }

    [Header("Assign Your Materials Here!")]
    public Material wallMaterial;
    public Material mudMaterial;
    public Material poisonMaterial;

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
                generator.Awake(); // Force setup!
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

        int vertexIndex = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int tileType = grid[x, y];
                if (tileType == 0) continue; // Empty space

                // Center the quad
                Vector3 pos = new Vector3(x + worldOrigin.x, y + worldOrigin.y, 0);

                // Add the 4 corners of the tile
                vertices.Add(pos + new Vector3(-0.5f, -0.5f, 0));
                vertices.Add(pos + new Vector3(-0.5f, 0.5f, 0));
                vertices.Add(pos + new Vector3(0.5f, -0.5f, 0));
                vertices.Add(pos + new Vector3(0.5f, 0.5f, 0));

                // UV Mapping
                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));

                // Assign triangles to the correct material list
                List<int> activeTriangles = wallTriangles;
                if (tileType == 2) activeTriangles = mudTriangles;
                if (tileType == 3) activeTriangles = poisonTriangles;

                activeTriangles.Add(vertexIndex + 0);
                activeTriangles.Add(vertexIndex + 1);
                activeTriangles.Add(vertexIndex + 2);
                activeTriangles.Add(vertexIndex + 2);
                activeTriangles.Add(vertexIndex + 1);
                activeTriangles.Add(vertexIndex + 3);

                vertexIndex += 4;
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();

         //Tell Unity this mesh has 3 materials
        mesh.subMeshCount = 3;
        mesh.SetTriangles(wallTriangles.ToArray(), 0);
        mesh.SetTriangles(mudTriangles.ToArray(), 1);
        mesh.SetTriangles(poisonTriangles.ToArray(), 2);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;
        meshRenderer.materials = new Material[] { wallMaterial, mudMaterial, poisonMaterial };

        Debug.Log($"Mesh built with {vertices.Count} vertices and {wallTriangles.Count / 3} wall triangles!");
    }
}