//using System;
//using Unity.Collections;
//using Unity.Mathematics;

//// 1. NATIVE MIN-HEAP (Burst cannot use the .NET Priority Queue)
//public struct NativeMinHeap<T> : IDisposable where T : unmanaged, IComparable<T>
//{
//    private NativeList<T> _items;
//    public int Count => _items.Length;

//    public NativeMinHeap(int capacity, Allocator allocator)
//    {
//        _items = new NativeList<T>(capacity, allocator);
//    }

//    public void Enqueue(T item)
//    {
//        _items.Add(item);
//        BubbleUp(_items.Length - 1);
//    }

//    public T Dequeue()
//    {
//        T minItem = _items[0];
//        _items[0] = _items[_items.Length - 1];
//        _items.RemoveAt(_items.Length - 1);
//        if (_items.Length > 0) BubbleDown(0);
//        return minItem;
//    }

//    private void BubbleUp(int index)
//    {
//        while (index > 0)
//        {
//            int parentIndex = (index - 1) / 2;
//            if (_items[index].CompareTo(_items[parentIndex]) < 0)
//            {
//                T temp = _items[index];
//                _items[index] = _items[parentIndex];
//                _items[parentIndex] = temp;
//                index = parentIndex;
//            }
//            else break;
//        }
//    }

//    private void BubbleDown(int index)
//    {
//        while (true)
//        {
//            int left = 2 * index + 1;
//            int right = 2 * index + 2;
//            int smallest = index;

//            if (left < _items.Length && _items[left].CompareTo(_items[smallest]) < 0) smallest = left;
//            if (right < _items.Length && _items[right].CompareTo(_items[smallest]) < 0) smallest = right;

//            if (smallest != index)
//            {
//                T temp = _items[index];
//                _items[index] = _items[smallest];
//                _items[smallest] = temp;
//                index = smallest;
//            }
//            else break;
//        }
//    }

//    public void Dispose()
//    {
//        if (_items.IsCreated) _items.Dispose();
//    }
//}

//// 2. PATH NODE STRUCT
//public struct PathNode : IComparable<PathNode>
//{
//    public int2 Position;
//    public float GCost;
//    public float HCost;
//    public float FCost => GCost + HCost;

//    public int CompareTo(PathNode other)
//    {
//        int compare = FCost.CompareTo(other.FCost);
//        if (compare == 0) compare = HCost.CompareTo(other.HCost);
//        return compare;
//    }
//}

//// 3. BURST PATHFINDER
//public struct BurstPathfinder
//{
//    public static void CalculateAStar(
//        int2 startGrid, int2 endGrid,
//        NativeArray<int> grid, int gridWidth, int gridHeight, int2 worldOrigin,
//        ref NativeList<float2> finalSmoothedPath)
//    {
//        if (grid[endGrid.x + endGrid.y * gridWidth] == 1) return; // Target is in a wall

//        NativeMinHeap<PathNode> openList = new NativeMinHeap<PathNode>(1000, Allocator.Temp);

//        // Burst is insanely fast with flat arrays instead of HashSets!
//        NativeArray<float> gCostMap = new NativeArray<float>(gridWidth * gridHeight, Allocator.Temp);
//        NativeArray<int2> parentMap = new NativeArray<int2>(gridWidth * gridHeight, Allocator.Temp);

//        for (int i = 0; i < gCostMap.Length; i++) gCostMap[i] = 99999;

//        PathNode startNode = new PathNode { Position = startGrid, GCost = 0, HCost = math.distance(startGrid, endGrid) };
//        openList.Enqueue(startNode);
//        gCostMap[startGrid.x + startGrid.y * gridWidth] = 0;

//        NativeArray<int2> neighbors = new NativeArray<int2>(4, Allocator.Temp);
//        neighbors[0] = new int2(0, 1); neighbors[1] = new int2(1, 0);
//        neighbors[2] = new int2(-1, 0); neighbors[3] = new int2(0, -1);

//        int loops = 0;
//        bool found = false;

//        while (openList.Count > 0 && loops < 10000)
//        {
//            loops++;
//            PathNode curr = openList.Dequeue();

//            if (curr.Position.Equals(endGrid))
//            {
//                found = true;
//                break;
//            }

//            for (int i = 0; i < 4; i++)
//            {
//                int2 nPos = curr.Position + neighbors[i];

//                if (nPos.x >= 0 && nPos.x < gridWidth && nPos.y >= 0 && nPos.y < gridHeight)
//                {
//                    int tileType = grid[nPos.x + nPos.y * gridWidth];
//                    if (tileType == 1) continue;

//                    int moveCost = (tileType == 2) ? 5 : (tileType == 3) ? 15 : 1;
//                    float newGCost = curr.GCost + moveCost;
//                    int nIdx = nPos.x + nPos.y * gridWidth;

//                    if (newGCost < gCostMap[nIdx])
//                    {
//                        gCostMap[nIdx] = newGCost;
//                        parentMap[nIdx] = curr.Position;

//                        openList.Enqueue(new PathNode
//                        {
//                            Position = nPos,
//                            GCost = newGCost,
//                            HCost = (int)math.distance(nPos, endGrid)
//                        });
//                    }
//                }
//            }
//        }

//        if (found)
//        {
//            NativeList<int2> rawPath = new NativeList<int2>(Allocator.Temp);
//            int2 trace = endGrid;
//            while (!trace.Equals(startGrid))
//            {
//                rawPath.Add(trace);
//                trace = parentMap[trace.x + trace.y * gridWidth];
//            }
//            rawPath.Add(startGrid);

//            // Reverse the array
//            for (int i = 0; i < rawPath.Length / 2; i++)
//            {
//                int2 temp = rawPath[i];
//                rawPath[i] = rawPath[rawPath.Length - 1 - i];
//                rawPath[rawPath.Length - 1 - i] = temp;
//            }

//            SmoothPath(rawPath, grid, gridWidth, gridHeight, worldOrigin, ref finalSmoothedPath);
//            rawPath.Dispose();
//        }

//        openList.Dispose();
//        gCostMap.Dispose();
//        parentMap.Dispose();
//        neighbors.Dispose();
//    }

//    private static void SmoothPath(NativeList<int2> path, NativeArray<int> grid, int gridWidth, int gridHeight, int2 worldOrigin, ref NativeList<float2> smoothed)
//    {
//        if (path.Length <= 2)
//        {
//            for (int i = 0; i < path.Length; i++) smoothed.Add(new float2(path[i].x + worldOrigin.x, path[i].y + worldOrigin.y));
//            return;
//        }

//        smoothed.Add(new float2(path[0].x + worldOrigin.x, path[0].y + worldOrigin.y));
//        int currentIndex = 0;

//        while (currentIndex < path.Length - 1)
//        {
//            int furthest = currentIndex + 1;
//            for (int i = currentIndex + 2; i < path.Length; i++)
//            {
//                if (HasBurstLOS(path[currentIndex], path[i], grid, gridWidth, gridHeight)) furthest = i;
//                else break;
//            }
//            smoothed.Add(new float2(path[furthest].x + worldOrigin.x, path[furthest].y + worldOrigin.y));
//            currentIndex = furthest;
//        }
//    }

//    private static bool HasBurstLOS(int2 start, int2 end, NativeArray<int> grid, int gridWidth, int gridHeight)
//    {
//        int x0 = start.x, y0 = start.y;
//        int x1 = end.x, y1 = end.y;
//        int dx = math.abs(x1 - x0), dy = math.abs(y1 - y0);
//        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
//        int err = dx - dy;

//        int loops = 0;
//        while (loops++ < 1000)
//        {
//            if (x0 < 0 || x0 >= gridWidth || y0 < 0 || y0 >= gridHeight) return false;

//            int tileType = grid[x0 + y0 * gridWidth];
//            if (tileType != 0) return false; // Ban diagonal cutting through any bad terrain

//            if (x0 == x1 && y0 == y1) break;

//            int e2 = 2 * err;
//            if (e2 > -dy) { err -= dy; x0 += sx; }
//            if (e2 < dx) { err += dx; y0 += sy; }
//        }
//        return true;
//    }
//}