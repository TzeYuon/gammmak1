using Starmap;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class StarmapScreen : UIScreen
{
    private Map map;

    private VisualElement mapContainer;
    private Dictionary<LevelNode, MapNodeVisualElement> mapNodes = new();

    MapPathCanvas pathCanvas;

    private Button deployBtn;
    private EntityQuery gameStateQuery;

    private LevelNode currentNode;
    private LevelNode selectedNode;

    public StarmapScreen(UIScreenManager manager, VisualElement root) : base(manager, root)
    {
        mapContainer = root.Q<VisualElement>("MapContainer");
        pathCanvas = root.Q<MapPathCanvas>("MapPathCanvas");

        deployBtn = root.Q<Button>("DeployBtn");
        deployBtn.clicked += OnDeployBtnClicked;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            var entityManager = world.EntityManager;
            gameStateQuery = entityManager.CreateEntityQuery(typeof(GameStateSingleton));
        }

        InitMap();

        UpdateDeployBtn();

        void InitMap()
        {
            map = RunManager.SaveData.ActiveRun.SectorMap;

            // add invisible active node to connect to start nodes
            currentNode = new(NodeType.Encounter, new Vector2Int(-1, -1));
            foreach (var startNode in map.StartNodes)
            {
                currentNode.Outgoing.Add(startNode);
            }

            // add node visual elements to map
            foreach (LevelNode node in map.AllNodes)
            {
                var pos = node.Position;
                MapNodeVisualElement nodeElement = UIScreenManager.UIAssetsDatabase.MapNode.Instantiate().Q<MapNodeVisualElement>();

                nodeElement.style.position = Position.Absolute;
                nodeElement.style.top = pos.y * 105 + 50;
                nodeElement.style.left = pos.x * 105 + 50;

                nodeElement.RegisterCallback<ClickEvent>(evt => OnMapNodeClicked(node));

                mapContainer.Add(nodeElement);
                mapNodes.Add(node, nodeElement);
            }
            UpdateMapNodeStates();

            // Draw paths between nodes
            DrawLines();
        }
    }

    private void DrawLines()
    {
        List<PathConnection> pathsToDraw = new();
        foreach (LevelNode node in map.AllNodes)
        {
            MapNodeVisualElement nodeElement = mapNodes[node];
            Vector2 start = new(nodeElement.style.left.value.value, nodeElement.style.top.value.value);
            foreach (var outgoingNode in node.Outgoing)
            {
                MapNodeVisualElement outgoingElement = mapNodes[outgoingNode];
                Vector2 end = new(outgoingElement.style.left.value.value, outgoingElement.style.top.value.value);

                pathsToDraw.Add(new PathConnection
                {
                    Start = start,
                    End = end,
                    IsActivePath = nodeElement.NodeState == MapNodeState.Current,
                });
            }
        }
        pathCanvas.SetPaths(pathsToDraw);
    }

    protected override void OnHide()
    {

    }

    protected override void OnShow()
    {
        UpdateMapNodeStates();
        UpdateDeployBtn();
        DrawLines();
    }

    void UpdateMapNodeStates()
    {
        foreach (var node in mapNodes.Values)
        {
            node.NodeState = MapNodeState.Default;
        }

        // set nodes that are on the path that the player took
        foreach (var node in map.PathTaken)
        {
            if (node != null && mapNodes.ContainsKey(node))
            {
                mapNodes[node].NodeState = MapNodeState.OnPath;
            }
        }

        // set current node if it exists
        if (currentNode != null && mapNodes.ContainsKey(currentNode))
        {
            mapNodes[currentNode].NodeState = MapNodeState.Current;
        }

        // set nodes that are reachable
        foreach (var selectable in currentNode.Outgoing)
        {
            mapNodes[selectable].NodeState = MapNodeState.Selectable;
        }

        // set selected node if it exists
        if (selectedNode != null && mapNodes.ContainsKey(selectedNode))
        {
            mapNodes[selectedNode].NodeState = MapNodeState.Selected;
        }
    }

    void OnMapNodeClicked(LevelNode node)
    {
        if (!mapNodes.ContainsKey(node)) return; // Node does not exist
        MapNodeVisualElement element = mapNodes[node];
        if (element.NodeState != MapNodeState.Selectable) return; // Cannot be selected

        selectedNode = node;
        UpdateMapNodeStates();
        UpdateDeployBtn();
    }

    void UpdateDeployBtn()
    {
        if (selectedNode == null) deployBtn.SetEnabled(false);
        else deployBtn.SetEnabled(true);
    }

    void OnDeployBtnClicked()
    {
        if (selectedNode == null) return; // no next level selected

        if (gameStateQuery.IsEmptyIgnoreFilter) return;
        gameStateQuery.CompleteDependency();
        var gameState = gameStateQuery.GetSingletonRW<GameStateSingleton>();
        gameState.ValueRW.CurrentState = GameState.Generating;

        map.PathTaken.Add(currentNode);

        currentNode = selectedNode;
        selectedNode = null;
    }
}

public enum MapNodeState
{
    Default,
    Current,
    Selected,
    OnPath,
    Selectable,
}

[UxmlElement("MapNode")]
public partial class MapNodeVisualElement : VisualElement
{
    private MapNodeState m_NodeState;

    [UxmlAttribute]
    public MapNodeState NodeState
    {
        get => m_NodeState;
        set
        {
            m_NodeState = value;
            Rebuild();
        }
    }

    public MapNodeVisualElement()
    {
        AddToClassList("map-node");
        Rebuild();
    }

    private void Rebuild()
    {
        RemoveFromClassList("map-node-default");
        RemoveFromClassList("map-node-current");
        RemoveFromClassList("map-node-selected");
        RemoveFromClassList("map-node-onpath");
        RemoveFromClassList("map-node-selectable");

        switch (m_NodeState)
        {
            case MapNodeState.Default:
                AddToClassList("map-node-default");
                break;
            case MapNodeState.Current:
                AddToClassList("map-node-current");
                break;
            case MapNodeState.Selected:
                AddToClassList("map-node-selected");
                break;
            case MapNodeState.OnPath:
                AddToClassList("map-node-onpath");
                break;
            case MapNodeState.Selectable:
                AddToClassList("map-node-selectable");
                break;
        }
    }
}

public struct PathConnection
{
    public Vector2 Start;
    public Vector2 End;
    public bool IsActivePath;
}

[UxmlElement("MapPathCanvas")]
public partial class MapPathCanvas : VisualElement
{
    private List<PathConnection> connections = new List<PathConnection>();

    public MapPathCanvas()
    {
        generateVisualContent += OnGenerateVisualContent;
        pickingMode = PickingMode.Ignore;
    }

    public void SetPaths(List<PathConnection> newConnections)
    {
        connections = newConnections;
        MarkDirtyRepaint();
    }

    private void OnGenerateVisualContent(MeshGenerationContext ctx)
    {
        if (connections == null || connections.Count == 0) return;

        var painter = ctx.painter2D;

        Color defaultColor = new Color(0f, 1f, 1f, 0.15f);
        Color activeColor = new Color(0f, 1f, 1f, 0.8f);

        painter.lineCap = LineCap.Round;
        painter.lineJoin = LineJoin.Round;

        foreach (var conn in connections)
        {
            painter.BeginPath();
            painter.MoveTo(conn.Start);
            painter.LineTo(conn.End);

            painter.lineWidth = conn.IsActivePath ? 3.0f : 1.5f;
            painter.strokeColor = conn.IsActivePath ? activeColor : defaultColor;

            painter.Stroke();
        }
    }
}