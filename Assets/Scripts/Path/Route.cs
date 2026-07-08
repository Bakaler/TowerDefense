using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A concrete, ordered list of world-space waypoints that a unit follows
/// from spawn to terminus. Built once at spawn time by PathGraph.BuildRoute().
/// </summary>
public class Route
{
    public readonly List<Vector2> Waypoints;

    /// <summary>Waypoint indices that teleport: reaching index i jumps (with a fade)
    /// straight to waypoint i+1 instead of walking the segment. The value is the
    /// delay in seconds the unit stays vanished between fade-out and fade-in.</summary>
    public readonly Dictionary<int, float> TeleportDepartures;

    public Route(List<Vector2> waypoints, Dictionary<int, float> teleportDepartures = null)
    {
        Waypoints          = waypoints          ?? new List<Vector2>();
        TeleportDepartures = teleportDepartures ?? new Dictionary<int, float>();
    }

    public bool IsEmpty => Waypoints.Count == 0;
}
