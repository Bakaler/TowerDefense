using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A concrete, ordered list of world-space waypoints that a unit follows
/// from spawn to terminus. Built once at spawn time by PathGraph.BuildRoute().
/// </summary>
public class Route
{
    public readonly List<Vector2> Waypoints;

    public Route(List<Vector2> waypoints)
    {
        Waypoints = waypoints ?? new List<Vector2>();
    }

    public bool IsEmpty => Waypoints.Count == 0;
}
