using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public class StarmapScreen : UIScreen
{
    private Starmap map;

    private VisualElement mapContainer;
    private Dictionary<LevelNode, MapNodeVisualElement> mapNodes = new();
    MapPathCanvas pathCanvas;

    private VisualElement mapDetailsContainer;
    private string mapDetailsOpenClass = "details-panel--open";
    private LevelNode pendingTargetNode = null;
    private bool isMapDetailsContainerOpen { get { return mapDetailsContainer.ClassListContains(mapDetailsOpenClass); } }
    private Label nodeTypeLbl, threatLevelLbl, objectiveLbl, enemyProfileLbl, terrainTypeLbl, rewardTypeLbl, anomalyLbl;

    private Button deployBtn;
    private EntityQuery gameStateQuery;

    private LevelNode currentNode;
    private LevelNode selectedNode;

    public StarmapScreen(UIScreenManager manager, VisualElement root) : base(manager, root)
    {
        mapContainer = root.Q<VisualElement>("MapContainer");
        mapDetailsContainer = root.Q<VisualElement>("MapDetailsContainer");
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

        InitMapDetailsContainer();

        UpdateDeployBtn();

        void InitMap()
        {
            map = RunManager.SaveData.ActiveSector.SectorMap;

            // add invisible active node to connect to start nodes
            currentNode = new(-1, -1);
            foreach (var startNode in map.StartNodes)
            {
                currentNode.Outgoing.Add(startNode);
            }

            // add node visual elements to map
            foreach (LevelNode node in map.AllNodes)
            {
                MapNodeVisualElement nodeElement = UIFactory.BuildMapNode(node);

                nodeElement.style.position = Position.Absolute;
                nodeElement.RegisterCallback<ClickEvent>(evt => OnMapNodeClicked(node));

                mapContainer.Add(nodeElement);
                mapNodes.Add(node, nodeElement);
            }
            UpdateMapNodeStates();
        }

        void InitMapDetailsContainer()
        {
            nodeTypeLbl = mapDetailsContainer.Q<Label>("NodeTypeLbl");
            threatLevelLbl = mapDetailsContainer.Q<Label>("ThreatLevelLbl");
            objectiveLbl = mapDetailsContainer.Q<Label>("ObjectiveLbl");
            enemyProfileLbl = mapDetailsContainer.Q<Label>("EnemyProfileLbl");
            terrainTypeLbl = mapDetailsContainer.Q<Label>("TerrainTypeLbl");
            rewardTypeLbl = mapDetailsContainer.Q<Label>("RewardTypeLbl");
            anomalyLbl = mapDetailsContainer.Q<Label>("AnomalyLbl");
        }
    }

    public void OnGeometryChanged(GeometryChangedEvent evt)
    {
        LayoutMap();
    }

    private void OnTransitionFinished(TransitionEndEvent evt)
    {
        if (evt.stylePropertyNames.Contains("height") && !isMapDetailsContainerOpen)
        {
            if (pendingTargetNode != null)
            {
                UpdateMapDetailsContainer(pendingTargetNode);
                pendingTargetNode = null;
                ToggleMapDetailsContainer(true);
            }
        }
    }

    private void LayoutMap()
    {
        float containerWidth = mapContainer.resolvedStyle.width;
        float containerHeight = mapContainer.resolvedStyle.height;

        // boundaries of map
        float minX = float.MaxValue; 
        float maxX = float.MinValue;
        float minY = float.MaxValue; 
        float maxY = float.MinValue;

        foreach (var node in map.AllNodes)
        {
            if (node.Layer < minX) minX = node.Layer;
            if (node.Layer > maxX) maxX = node.Layer;
            if (node.Lane < minY) minY = node.Lane;
            if (node.Lane > maxY) maxY = node.Lane;
        }

        float mapWidth = math.max(1f, maxX - minX);
        float mapHeight = math.max(1f, maxY - minY);

        // Calculate Scale and Offset
        float paddingMultiplierX = 0.85f;
        float paddingMultiplierY = 0.8f;
        float scaleX = (containerWidth * paddingMultiplierX) / mapWidth;
        float scaleY = (containerHeight * paddingMultiplierY) / mapHeight;

        float offsetX = containerWidth * (1f - paddingMultiplierX) * 0.5f;
        float offsetY = containerHeight * (1f - paddingMultiplierY) * 0.5f;

        float randomOffsetX = 0.2f * scaleX;
        float randomOffsetY = 0.15f * scaleY;
        var originalRandomState = UnityEngine.Random.state;

        // Position nodes in the middle of mapContainer
        foreach (var nodeData in map.AllNodes)
        {
            if (mapNodes.TryGetValue(nodeData, out MapNodeVisualElement nodeVisual))
            {
                float localX = nodeData.Layer - minX;
                float localY = nodeData.Lane - minY;
                float uiLocalY = mapHeight - localY;

                UnityEngine.Random.InitState(nodeData.GetHashCode());
                float rand_x = UnityEngine.Random.Range(-randomOffsetX, randomOffsetX);
                float rand_y = UnityEngine.Random.Range(-randomOffsetY, randomOffsetY);

                float finalUI_X = (localX * scaleX) + offsetX + rand_x;
                float finalUI_Y = (uiLocalY * scaleY) + offsetY + rand_y;

                nodeVisual.style.left = finalUI_X;
                nodeVisual.style.top = finalUI_Y;
            }
        }
        UnityEngine.Random.state = originalRandomState;

        DrawLines();

        void DrawLines()
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
    }

    protected override void OnHide()
    {
        mapContainer.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        mapDetailsContainer.UnregisterCallback<TransitionEndEvent>(OnTransitionFinished);

        ToggleMapDetailsContainer(selectedNode != null);
    }

    protected override void OnShow()
    {
        mapContainer.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        mapDetailsContainer.RegisterCallback<TransitionEndEvent>(OnTransitionFinished);

        UpdateMapNodeStates();
        UpdateDeployBtn();

        ToggleMapDetailsContainer(selectedNode != null);
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

        if (isMapDetailsContainerOpen)
        {
            pendingTargetNode = node;
            ToggleMapDetailsContainer(false);
        }
        else
        {
            UpdateMapDetailsContainer(node);
            ToggleMapDetailsContainer(true);
        }
    }

    void ToggleMapDetailsContainer(bool open)
    {
        if (open) mapDetailsContainer.AddToClassList(mapDetailsOpenClass);
        else mapDetailsContainer.RemoveFromClassList(mapDetailsOpenClass);
    }

    void UpdateMapDetailsContainer(LevelNode node)
    {
        // populate node details
        if (node == null)
        {
            nodeTypeLbl.text = "NODE TYPE : [ NONE ]";
            threatLevelLbl.text = "THREAT LEVEL : [ NONE ]";
            objectiveLbl.text = "OBJECTIVE : [ NONE ]";
            enemyProfileLbl.text = "ENEMY PROFILE : [ NONE ]";
            terrainTypeLbl.text = "TERRAIN : [ NONE ]";
            rewardTypeLbl.text = "REWARD : [ NONE ]";
            anomalyLbl.text = "ANOMALY : [ NONE ]";
            return;
        }

        nodeTypeLbl.text = $"NODE TYPE : [ {node.NodeType.ToString().ToUpper()} ]";
        threatLevelLbl.text = $"THREAT LEVEL : [ {node.ThreatLevel.ToString().ToUpper()} ]";
        objectiveLbl.text = $"OBJECTIVE : [ {node.ObjectiveType.ToString().ToUpper()} ]";
        enemyProfileLbl.text = $"ENEMY PROFILE : [ {node.EnemyProfile.ToString().ToUpper()} ]";
        terrainTypeLbl.text = $"TERRAIN : [ {node.TerrainType.ToString().ToUpper()} ]";
        rewardTypeLbl.text = $"REWARD : [ {node.RewardType.ToString().ToUpper()} ]";
        anomalyLbl.text = $"ANOMALY : [ {node.AnomalyType.ToString().ToUpper()} ]";
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

        map.CurrentLevel = selectedNode;

        map.PathTaken.Add(currentNode);

        currentNode = selectedNode;
        selectedNode = null;

        RunManager.SaveData.MoveToNode(currentNode);
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
    private NodeType nodeType;
    private ThreatLevel threatLevel;
    private RewardType rewardType;

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

    public void Init(LevelNode node)
    {
        nodeType = node.NodeType;
        threatLevel = node.ThreatLevel;
        rewardType = node.RewardType;

        VisualElement nodeTypeSymbol = this.Q<VisualElement>("NodeTypeSymbol");
        VisualElement threatBorder = this.Q<VisualElement>("ThreatBorder");
        VisualElement rewardSymbol = this.Q<VisualElement>("RewardSymbol");

        nodeTypeSymbol.RemoveFromClassList("node-encounter");
        nodeTypeSymbol.RemoveFromClassList("node-elite");
        nodeTypeSymbol.RemoveFromClassList("node-store");
        nodeTypeSymbol.RemoveFromClassList("node-boss");
        switch (nodeType)
        {
            case NodeType.Encounter:
                nodeTypeSymbol.AddToClassList("node-encounter");
                break;
            case NodeType.EliteEncounter:
                nodeTypeSymbol.AddToClassList("node-elite");
                break;
            case NodeType.Store:
                nodeTypeSymbol.AddToClassList("node-store");
                break;
            case NodeType.Boss:
                nodeTypeSymbol.AddToClassList("node-boss");
                break;
        }

        threatBorder.RemoveFromClassList("threat-low");
        threatBorder.RemoveFromClassList("threat-medium");
        threatBorder.RemoveFromClassList("threat-high");
        threatBorder.RemoveFromClassList("threat-boss");
        switch (threatLevel)
        {
            case ThreatLevel.None:
                break;
            case ThreatLevel.Low:
                threatBorder.AddToClassList("threat-low");
                break;
            case ThreatLevel.Medium:
                threatBorder.AddToClassList("threat-medium");
                break;
            case ThreatLevel.High:
                threatBorder.AddToClassList("threat-high");
                break;
            case ThreatLevel.Boss:
                threatBorder.AddToClassList("threat-boss");
                break;
        }

        rewardSymbol.RemoveFromClassList("reward-cores");
        rewardSymbol.RemoveFromClassList("reward-weapon");
        rewardSymbol.RemoveFromClassList("reward-ship");
        rewardSymbol.RemoveFromClassList("reward-repair");
        switch (rewardType)
        {
            case RewardType.None:
                break;
            case RewardType.UpgradeCores:
                rewardSymbol.AddToClassList("reward-cores");
                break;
            case RewardType.WeaponBlueprint:
                rewardSymbol.AddToClassList("reward-weapon");
                break;
            case RewardType.ShipBlueprint:
                rewardSymbol.AddToClassList("reward-ship");
                break;
            case RewardType.MothershipRepair:
                rewardSymbol.AddToClassList("reward-repair");
                break;
        }
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