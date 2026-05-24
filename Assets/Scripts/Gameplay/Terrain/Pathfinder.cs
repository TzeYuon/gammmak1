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
        if (GridManager.Instance == null || GridManager.Instance.grid == null)
        {
            return null;
        }

        Vector2Int origin = GridManager.Instance.worldOrigin;
        Vector2Int a = worldStart - origin;
        Vector2Int b = worldEnd - origin;

        a.x = Mathf.Clamp(a.x, 0, GridManager.Instance.width - 1);
        a.y = Mathf.Clamp(a.y, 0, GridManager.Instance.height - 1);

        b.x = Mathf.Clamp(b.x, 0, GridManager.Instance.width - 1);
        b.y = Mathf.Clamp(b.y, 0, GridManager.Instance.height - 1);

        if (GridManager.Instance.grid[b.x, b.y] == 1) return null;

        int maxIterations = 40000;
        int currentLoop = 0;

        Node currNode = new Node(a.x, a.y, true);
        Node destNode = new Node(b.x, b.y, true);

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        PriorityQueue<Node> pq = new PriorityQueue<Node>();
        pq.Enqueue(currNode, currNode.fCost);

        var adj = new List<(int, int)> { (0, 1), (1, 0), (-1, 0), (0, -1) };
        var resPath = new List<Vector2Int>();

        while (pq.Count > 0)
        {
            currentLoop++;
            if (currentLoop > maxIterations) { return null; }
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
                //for (int i = 0; i < resPath.Count; i++)
                //{
                //    resPath[i] = resPath[i] + origin;
                //}
                //return resPath;
                var smoothedPath = SmoothPath(resPath, forcePath);
                for (int i = 0; i < smoothedPath.Count; i++)
                {
                    smoothedPath[i] = smoothedPath[i] + origin;
                }
                return smoothedPath; 
            }

            foreach (var tup in adj)
            {
                int tempX = tup.Item1 + currNode.x;
                int tempY = tup.Item2 + currNode.y;
                Vector2Int tempPos = new Vector2Int(tempX, tempY);

                if (0 <= tempX && tempX < GridManager.Instance.width && 0 <= tempY && tempY < GridManager.Instance.height)
                {
                    int tileType = GridManager.Instance.grid[tempX, tempY];

                    if (tileType == 1) continue;
                    if (visited.Contains(tempPos)) continue;

                    int moveCost = 1;
                    if (!forcePath)
                    {
                        if (tileType == 2) moveCost = 5; // Mud
                        if (tileType == 3) moveCost = 15; // Poison
                    }

                    Node tempNode = new Node(tempX, tempY, true);
                    tempNode.gCost = currNode.gCost + moveCost;
                    tempNode.hCost = Mathf.Abs(destNode.x - tempNode.x) + Mathf.Abs(destNode.y - tempNode.y);
                    tempNode.parent = currNode;


                    pq.Enqueue(tempNode, tempNode.fCost);
                }
            }
        }
        return resPath;
    }

    private static bool HasLineOfSight(Vector2Int start, Vector2Int end, bool forcePath = false)
    {
        int x = start.x, y = start.y;
        int dx = Mathf.Abs(end.x - start.x), dy = Mathf.Abs(end.y - start.y);
        int sx = start.x < end.x ? 1 : -1, sy = start.y < end.y ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (x < 0 || x >= GridManager.Instance.width || y < 0 || y >= GridManager.Instance.height) return false;

            int tileType = GridManager.Instance.grid[x, y];
            if (GridManager.Instance.grid[x, y] == 1) return false; // Hit a wall
            if (!forcePath && (tileType == 2 || tileType == 3)) return false;
            if (x == end.x && y == end.y) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
        return true;
    }

    private static List<Vector2Int> SmoothPath(List<Vector2Int> path, bool forcePath)
    {
        if (path == null || path.Count <= 2) return path;
        List<Vector2Int> smoothedPath = new List<Vector2Int> { path[0] };
        int currentIndex = 0;

        while (currentIndex < path.Count - 1)
        {
            int furthestVisibleIndex = currentIndex + 1;
            for (int i = currentIndex + 2; i < path.Count; i++)
            {
                if (HasLineOfSight(path[currentIndex], path[i], forcePath)) furthestVisibleIndex = i;
                else break;
            }
            smoothedPath.Add(path[furthestVisibleIndex]);
            currentIndex = furthestVisibleIndex;
        }
        return smoothedPath;
    }
}