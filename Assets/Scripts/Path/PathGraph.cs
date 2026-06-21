using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level directed graph of PathNodes.
/// - Place this component on a single persistent GameObject.
/// - Populate 'nodes' with every PathNode in the scene, in any order.
/// - Assign PathNode.connections to form the directed edges.
///
/// At spawn time, UnitSpawner calls BuildRoute(headNode) which:
///   1. Walks the graph from head to terminus, picking a random edge at each junction.
///   2. Samples a Catmull-Rom spline between consecutive nodes.
///   3. Returns a Route (ordered waypoint list) ready for RouteFollower.
///
/// Color legend (Gizmos):
///   Cyan   = standard node
///   Yellow = junction (branching)
///   Red    = terminus (enemies reach here → take lives)
///   Green  = head (entry point for spawners)
/// </summary>
public class PathGraph : MonoBehaviour
{
    public static PathGraph Instance { get; private set; }

    [Tooltip("All PathNodes that belong to this graph. Order doesn't matter.")]
    public List<PathNode> nodes = new List<PathNode>();

    [Tooltip("How many waypoints to sample along each node-to-node spline segment.")]
    [Range(4, 40)]
    public int samplesPerSegment = 12;

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Disable this duplicate component — do NOT destroy the GameObject,
            // as it may contain path nodes other spawners hold references to.
            enabled = false;
            return;
        }
        Instance = this;
        BuildIncomingMap();
    }

    void OnValidate() => BuildIncomingMap();

    [ContextMenu("Scan Scene for PathNodes")]
    public void ScanScene()
    {
        nodes = new List<PathNode>(FindObjectsByType<PathNode>(FindObjectsSortMode.None));
        BuildIncomingMap();
        Debug.Log($"[PathGraph] Scanned scene — found {nodes.Count} PathNode(s).");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // ── Incoming-edge map (for head detection) ────────────────────────

    private HashSet<PathNode> _hasIncoming = new HashSet<PathNode>();

    void BuildIncomingMap()
    {
        _hasIncoming.Clear();
        foreach (var node in nodes)
        {
            if (node == null) continue;
            foreach (var next in node.connections)
                if (next != null) _hasIncoming.Add(next);
        }
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>Returns all nodes that have no incoming connections (spawn entry points).</summary>
    public List<PathNode> GetHeads()
    {
        var heads = new List<PathNode>();
        foreach (var n in nodes)
            if (n != null && !_hasIncoming.Contains(n))
                heads.Add(n);
        return heads;
    }

    /// <summary>
    /// Build a complete Route from the given head to any terminus.
    /// At each junction a random outgoing edge is chosen.
    /// </summary>
    public Route BuildRoute(PathNode head)
    {
        if (head == null) return new Route(null);

        var waypoints = new List<Vector2>();
        PathNode prev    = null;
        PathNode current = head;

        // Guard against cycles (shouldn't exist, but safety first)
        var visited = new HashSet<PathNode>();

        while (current != null)
        {
            if (!visited.Add(current))
            {
                Debug.LogWarning($"[PathGraph] Cycle detected at node '{current.name}'. Stopping route.");
                break;
            }

            if (prev == null)
            {
                // First node — just add its position
                waypoints.Add(current.Position);
            }
            else
            {
                // Sample spline from prev → current
                // For Catmull-Rom we need a point before 'prev' and after 'current'
                Vector2 p0 = GetPredecessorHint(prev, current);
                Vector2 p1 = prev.Position;
                Vector2 p2 = current.Position;
                Vector2 p3 = GetSuccessorHint(current);

                for (int i = 1; i <= samplesPerSegment; i++)
                {
                    float t = i / (float)samplesPerSegment;
                    waypoints.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            if (current.IsTerminus) break;

            // Pick next edge (random at junctions)
            PathNode next = current.connections[Random.Range(0, current.connections.Count)];
            prev    = current;
            current = next;
        }

        return new Route(waypoints);
    }

    // ── Spline helpers ────────────────────────────────────────────────

    /// <summary>
    /// Ghost point before 'node' — extrapolated back along the incoming direction.
    /// If node has exactly one incoming connection in the graph, use that node's position
    /// so the spline enters smoothly; otherwise extrapolate.
    /// </summary>
    private Vector2 GetPredecessorHint(PathNode node, PathNode nextNode)
    {
        // Try to find the actual predecessor in the graph
        foreach (var n in nodes)
        {
            if (n == null || n == node) continue;
            if (n.connections.Contains(node)) return n.Position;
        }
        // Fallback: extrapolate backwards from the node→nextNode direction
        return node.Position - (nextNode.Position - node.Position);
    }

    /// <summary>
    /// Ghost point after 'node' — if node has connections, use the first one;
    /// otherwise extrapolate forward.
    /// </summary>
    private Vector2 GetSuccessorHint(PathNode node)
    {
        if (node.connections.Count > 0 && node.connections[0] != null)
            return node.connections[0].Position;
        // Terminus: extrapolate the last direction
        return node.Position + (node.Position - node.Position); // same point is fine
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    // ── Gizmos ───────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (nodes == null) return;

        BuildIncomingMap(); // keep fresh in editor

        foreach (var node in nodes)
        {
            if (node == null) continue;

            bool isHead     = !_hasIncoming.Contains(node);
            bool isTerm     = node.IsTerminus;
            bool isJunction = node.IsJunction;

            // Node sphere
            Gizmos.color = isTerm     ? new Color(1f, 0.2f, 0.2f) :
                           isHead     ? new Color(0f, 1f, 0.4f)   :
                           isJunction ? new Color(1f, 0.9f, 0f)   :
                                        new Color(0.2f, 0.8f, 1f);
            Gizmos.DrawSphere(node.transform.position, 0.2f);

            // Spline edges
            foreach (var next in node.connections)
            {
                if (next == null) continue;

                Vector2 p0 = GetPredecessorHint(node, next);
                Vector2 p1 = node.Position;
                Vector2 p2 = next.Position;
                Vector2 p3 = GetSuccessorHint(next);

                Gizmos.color = new Color(1f, 0.75f, 0f, 0.9f);
                Vector2 prev = p1;
                int drawSamples = samplesPerSegment * 2;
                for (int i = 1; i <= drawSamples; i++)
                {
                    float t    = i / (float)drawSamples;
                    Vector2 pt = CatmullRom(p0, p1, p2, p3, t);
                    Gizmos.DrawLine(prev, pt);
                    prev = pt;
                }
            }
        }
    }
}
