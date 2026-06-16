using System.Collections.Generic;

public class Starmap
{
    public List<LevelNode> AllNodes;
    public List<LevelNode> StartNodes;
    public List<LevelNode> PathTaken;

    public Starmap(List<LevelNode> nodes, List<LevelNode> startNodes)
    {
        AllNodes = nodes;
        StartNodes = startNodes;
        PathTaken = new();
    }
}