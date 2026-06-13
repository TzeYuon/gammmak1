using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Starmap
{
    public class Map
    {
        public List<LevelNode> AllNodes;
        public List<LevelNode> StartNodes;
        public List<LevelNode> PathTaken;

        public Map(List<LevelNode> nodes, List<LevelNode> startNodes)
        {
            AllNodes = nodes;
            StartNodes = startNodes;
            PathTaken = new();
        }
    }
}