using System;
using System.Collections.Generic;
using UnityEngine;

public class Pathfinder : MonoBehaviour
{
    public static Pathfinder Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static List<Vector2Int> Astar(Vector2Int worldStart, Vector2Int worldEnd, bool forcePath = false)
    {
        if (Unity.Entities.World.DefaultGameObjectInjectionWorld == null) return null;

        var entityManager = Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = entityManager.CreateEntityQuery(typeof(TerrainGridComponent));

        if (!query.HasSingleton<TerrainGridComponent>()) return null;

        var terrainComp = query.GetSingleton<TerrainGridComponent>();
        if (!terrainComp.Grid.IsCreated) return null;

        int gridWidth = terrainComp.GridWidth;
        int gridHeight = terrainComp.GridHeight;
        var grid = terrainComp.Grid;

        Vector2Int origin = new Vector2Int(terrainComp.WorldOrigin.x, terrainComp.WorldOrigin.y);
        Vector2Int a = worldStart - origin;
        Vector2Int b = worldEnd - origin;

        a.x = Mathf.Clamp(a.x, 0, gridWidth - 1);
        a.y = Mathf.Clamp(a.y, 0, gridHeight - 1);

        b.x = Mathf.Clamp(b.x, 0, gridWidth - 1);
        b.y = Mathf.Clamp(b.y, 0, gridHeight - 1);

        if (grid[b.x + b.y * gridWidth] == 1) return null;

        int maxIterations = gridWidth * gridHeight;
        int currentLoop = 0;

        Node currNode = new Node(a.x, a.y, true);
        Node destNode = new Node(b.x, b.y, true);

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        PriorityQueue<Node> pq = new PriorityQueue<Node>();
        pq.Enqueue(currNode);

        var adj = new List<(int, int)>
        {
            (0, 1), (1, 0), (-1, 0), (0, -1),   
            (1, 1), (1, -1), (-1, 1), (-1, -1)  
        };

        var resPath = new List<Vector2Int>();

        while (pq.Count > 0)
        {
            currentLoop++;
            if (currentLoop > maxIterations) return null;

            currNode = pq.Dequeue();

            Vector2Int currPos = new Vector2Int(currNode.x, currNode.y);
            if (visited.Contains(currPos)) continue;
            visited.Add(currPos);

            if (currNode.x == destNode.x && currNode.y == destNode.y)
            {
                Node temp = currNode;
                while (temp != null)
                {
                    resPath.Add(new Vector2Int(temp.x, temp.y));
                    temp = temp.parent;
                }

                resPath.Reverse();

                for (int i = 0; i < resPath.Count; i++)
                {
                    resPath[i] = resPath[i] + origin;
                }

                return resPath;
            }

            foreach (var tup in adj)
            {
                int tempX = tup.Item1 + currNode.x;
                int tempY = tup.Item2 + currNode.y;
                Vector2Int tempPos = new Vector2Int(tempX, tempY);

                if (tempX >= 0 && tempX < gridWidth && tempY >= 0 && tempY < gridHeight)
                {
                    int tileType = grid[tempX + tempY * gridWidth];

                    if (tileType == 1) continue;
                    if (visited.Contains(tempPos)) continue;

                    if (tup.Item1 != 0 && tup.Item2 != 0)
                    {
                        if (grid[currNode.x + tup.Item1 + currNode.y * gridWidth] == 1 ||
                            grid[currNode.x + (currNode.y + tup.Item2) * gridWidth] == 1)
                        {
                            continue; 
                        }
                    }

                    int moveCost = (tup.Item1 != 0 && tup.Item2 != 0) ? 14 : 10;

                    if (!forcePath)
                    {
                        if (tileType == (int)TerrainTileType.Mud) moveCost += 5;
                        if (tileType == (int)TerrainTileType.Poison) moveCost += 15;
                    }

                    Node tempNode = new Node(tempX, tempY, true);
                    tempNode.gCost = currNode.gCost + moveCost;

                    int dstX = Mathf.Abs(destNode.x - tempNode.x);
                    int dstY = Mathf.Abs(destNode.y - tempNode.y);
                    tempNode.hCost = (dstX > dstY) ?
                        14 * dstY + 10 * (dstX - dstY) :
                        14 * dstX + 10 * (dstY - dstX);

                    tempNode.parent = currNode;

                    pq.Enqueue(tempNode);
                }
            }
        }
        return resPath;
    }
}

// Helper class/functions
public class Node : IComparable<Node>
{
    public int x;
    public int y;
    public bool walkable;

    public int gCost = 0;
    public int hCost = 0;
    public int fCost => gCost + hCost;

    public Node parent;

    public Node(int x, int y, bool walkable)
    {
        this.x = x;
        this.y = y;
        this.walkable = walkable;
    }

    public int CompareTo(Node other)
    {
        int compare = fCost.CompareTo(other.fCost);
        if (compare == 0) compare = hCost.CompareTo(other.hCost);
        return compare;
    }
}

public class PriorityQueue<TElement> where TElement : IComparable<TElement>
{
    private List<TElement> elements = new List<TElement>();

    public int Count => elements.Count;

    public void Enqueue(TElement item)
    {
        elements.Add(item);
        BubbleUp(elements.Count - 1);
    }

    public TElement Dequeue()
    {
        TElement first = elements[0];
        elements[0] = elements[elements.Count - 1];
        elements.RemoveAt(elements.Count - 1);
        if (elements.Count > 0) BubbleDown(0);
        return first;
    }

    private void BubbleUp(int index)
    {
        while (index > 0)
        {
            int parentIndex = (index - 1) / 2;
            if (elements[index].CompareTo(elements[parentIndex]) < 0)
            {
                TElement temp = elements[index];
                elements[index] = elements[parentIndex];
                elements[parentIndex] = temp;
                index = parentIndex;
            }
            else break;
        }
    }

    private void BubbleDown(int index)
    {
        while (true)
        {
            int leftChild = 2 * index + 1;
            int rightChild = 2 * index + 2;
            int smallest = index;

            if (leftChild < elements.Count && elements[leftChild].CompareTo(elements[smallest]) < 0)
                smallest = leftChild;

            if (rightChild < elements.Count && elements[rightChild].CompareTo(elements[smallest]) < 0)
                smallest = rightChild;

            if (smallest != index)
            {
                TElement temp = elements[index];
                elements[index] = elements[smallest];
                elements[smallest] = temp;
                index = smallest;
            }
            else break;
        }
    }
}