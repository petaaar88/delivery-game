using System.Collections.Generic;
using UnityEngine;

public class NavNode : MonoBehaviour
{
    [Tooltip("One-way manual connections. For two-way, add each node to the other's list too.")]
    public List<NavNode> manualNeighbors = new List<NavNode>();

    // Populated at runtime by NavigationGraph — not serialized.
    [System.NonSerialized] public List<NavNode> neighbors = new List<NavNode>();

    void OnDrawGizmos()
    {
        bool onPath = Application.isPlaying
            && NavigationGraph.Instance != null
            && NavigationGraph.Instance.IsOnPath(this);

        Gizmos.color = onPath ? Color.green : Color.yellow;
        Gizmos.DrawSphere(transform.position, onPath ? 0.9f : 0.5f);

        // In play mode show computed graph; in edit mode show manual connections.
        List<NavNode> toShow = Application.isPlaying ? neighbors : manualNeighbors;
        Gizmos.color = Color.cyan;
        foreach (var n in toShow)
            if (n != null) Gizmos.DrawLine(transform.position, n.transform.position);

        // Show auto-connect radius sourced from NavigationGraph.
        NavigationGraph graph = Application.isPlaying
            ? NavigationGraph.Instance
            : Object.FindObjectOfType<NavigationGraph>();
        if (graph != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
            Gizmos.DrawSphere(transform.position, graph.autoConnectRadius);
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, graph.autoConnectRadius);
        }
    }
}
