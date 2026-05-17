using UnityEngine;

public class Node
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
}