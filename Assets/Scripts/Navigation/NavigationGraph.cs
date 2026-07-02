using System.Collections.Generic;
using UnityEngine;

public class NavigationGraph : MonoBehaviour
{
    public static NavigationGraph Instance { get; private set; }

    private NavNode[] _nodes;

    // Nodes and consecutive node pairs of the most recently computed path. Used for gizmo highlighting.
    private readonly HashSet<NavNode> _pathNodes = new HashSet<NavNode>();
    private readonly HashSet<(NavNode, NavNode)> _pathEdges = new HashSet<(NavNode, NavNode)>();

    void Awake()
    {
        Instance = this;
        _nodes = GetComponentsInChildren<NavNode>();
        BuildGraph();
    }

    /// <summary>True if the node is part of the most recently computed path.</summary>
    public bool IsOnPath(NavNode node) => _pathNodes.Contains(node);

    /// <summary>True if a and b are consecutive nodes on the most recently computed path (either direction).</summary>
    public bool IsPathEdge(NavNode a, NavNode b) => _pathEdges.Contains((a, b));

    /// <summary>Clears the highlighted path (e.g. when a delivery ends).</summary>
    public void ClearPath()
    {
        _pathNodes.Clear();
        _pathEdges.Clear();
    }

    private List<Vector3> StorePath(List<NavNode> pathNodes)
    {
        ClearPath();
        var result = new List<Vector3>();
        for (int i = 0; i < pathNodes.Count; i++)
        {
            _pathNodes.Add(pathNodes[i]);
            result.Add(pathNodes[i].transform.position);
            if (i > 0)
            {
                _pathEdges.Add((pathNodes[i - 1], pathNodes[i]));
                _pathEdges.Add((pathNodes[i], pathNodes[i - 1]));
            }
        }
        return result;
    }

    void BuildGraph()
    {
        foreach (var node in _nodes)
            node.neighbors.Clear();

        // Manual connections only. Each manual link is treated as two-way: wiring one side
        // in the Inspector is enough.
        int edgeCount = 0;
        foreach (var node in _nodes)
        {
            foreach (var n in node.manualNeighbors)
            {
                if (n == null) continue;
                if (!node.neighbors.Contains(n)) { node.neighbors.Add(n); edgeCount++; }
                if (!n.neighbors.Contains(node)) { n.neighbors.Add(node); edgeCount++; }
            }
        }

        Debug.Log($"[NavGraph] Built graph: {_nodes.Length} nodes, {edgeCount} directed edges (manual links, two-way)");
        foreach (var node in _nodes)
            if (node.neighbors.Count == 0)
                Debug.LogWarning($"[NavGraph] Node '{node.name}' has no neighbors — it is unreachable.");
    }

    public NavNode FindNearest(Vector3 position)
    {
        NavNode nearest = null;
        float bestSq = float.MaxValue;
        foreach (var node in _nodes)
        {
            float sq = (node.transform.position - position).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; nearest = node; }
        }
        return nearest;
    }

    /// <summary>
    /// Returns ordered node positions from the node nearest 'from' to the node nearest 'to'.
    /// Returns an empty list when no nodes are present.
    /// </summary>
    public List<Vector3> FindPath(Vector3 from, Vector3 to)
    {
        if (_nodes.Length == 0) { Debug.LogWarning("[NavGraph] No nodes loaded!"); _pathNodes.Clear(); return new List<Vector3>(); }

        NavNode startNode = FindNearest(from);
        NavNode endNode   = FindNearest(to);

        Debug.Log($"[NavGraph] FindPath: start={startNode.name} ({startNode.neighbors.Count} neighbors), end={endNode.name} ({endNode.neighbors.Count} neighbors)");

        if (startNode == endNode)
        {
            Debug.Log("[NavGraph] start==end, returning single node");
            return StorePath(new List<NavNode> { endNode });
        }

        var open      = new HashSet<NavNode> { startNode };
        var closed    = new HashSet<NavNode>();
        var cameFrom  = new Dictionary<NavNode, NavNode>();
        var gScore    = new Dictionary<NavNode, float>();

        foreach (var n in _nodes) gScore[n] = float.MaxValue;
        gScore[startNode] = 0f;

        while (open.Count > 0)
        {
            NavNode current = null;
            float bestF = float.MaxValue;
            foreach (var n in open)
            {
                float f = gScore.GetValueOrDefault(n, float.MaxValue)
                        + Vector3.Distance(n.transform.position, endNode.transform.position);
                if (f < bestF) { bestF = f; current = n; }
            }

            if (current == null || current == endNode) break;
            open.Remove(current);
            closed.Add(current);

            foreach (var neighbor in current.neighbors)
            {
                if (neighbor == null || closed.Contains(neighbor)) continue;
                float tentativeG = gScore.GetValueOrDefault(current, float.MaxValue)
                                 + Vector3.Distance(current.transform.position, neighbor.transform.position);
                if (tentativeG < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor]   = tentativeG;
                    open.Add(neighbor);
                }
            }
        }

        var path = new List<NavNode>();
        var cursor = endNode;
        int guard = _nodes.Length + 2;
        while (cursor != null && guard-- > 0)
        {
            path.Insert(0, cursor);
            cursor = cameFrom.TryGetValue(cursor, out var prev) ? prev : null;
        }

        return StorePath(path);
    }
}
