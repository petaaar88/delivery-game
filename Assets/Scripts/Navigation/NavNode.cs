using System.Collections.Generic;
using UnityEngine;

public class NavNode : MonoBehaviour
{
    [Tooltip("Manual connections. Each link is two-way: adding a node here connects both directions, no need to wire the other side.")]
    public List<NavNode> manualNeighbors = new List<NavNode>();

    // Populated at runtime by NavigationGraph — not serialized.
    [System.NonSerialized] public List<NavNode> neighbors = new List<NavNode>();

    void OnDrawGizmos()
    {
        bool onPath = Application.isPlaying
            && NavigationGraph.Instance != null
            && NavigationGraph.Instance.IsOnPath(this);

        Gizmos.color = onPath ? Color.green : Color.red;
        Gizmos.DrawSphere(transform.position, onPath ? 0.9f : 0.5f);

        // In play mode show computed graph; in edit mode show manual connections.
        // Links that are part of the current path to the goal are drawn red.
        List<NavNode> toShow = Application.isPlaying ? neighbors : manualNeighbors;
        foreach (var n in toShow)
        
        {
            if (n == null) continue;
            bool pathEdge = Application.isPlaying
                && NavigationGraph.Instance != null
                && NavigationGraph.Instance.IsPathEdge(this, n);
            Gizmos.color = pathEdge ? Color.yellow : Color.cyan;
            Gizmos.DrawLine(transform.position, n.transform.position);
        }
    }
}
