using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A node in the enemy path graph. Place these as GameObjects in the scene and connect
/// them via the 'connections' list to form the path network.
///
/// - Head node  : no other node points to this one (spawn entry points)
/// - Junction   : connections.Count > 1 (branching point — route picks one at random)
/// - Terminus   : connections.Count == 0 (enemies reaching here deal damage / die)
/// </summary>
public class PathNode : MonoBehaviour
{
    [Tooltip("Nodes this one leads to. Add multiple for branching paths.")]
    public List<PathNode> connections = new List<PathNode>();

    public bool IsTerminus => connections.Count == 0;
    public bool IsJunction => connections.Count > 1;

    public Vector2 Position => transform.position;

    // ── Gizmos ───────────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        Gizmos.color = IsTerminus ? Color.red
                     : IsJunction ? Color.yellow
                     : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.18f);

        // Draw raw straight connections so nodes are visible even before graph bakes
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        foreach (var next in connections)
        {
            if (next == null) continue;
            Gizmos.DrawLine(transform.position, next.transform.position);
        }
    }
}
