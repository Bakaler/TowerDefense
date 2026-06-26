using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores painted placement zones as a list of world-space circles.
/// Create via Assets → Create → TowerDefense → Placement Zones.
/// </summary>
[CreateAssetMenu(menuName = "TowerDefense/Placement Zones", fileName = "PlacementZones")]
public class PlacementZones : ScriptableObject
{
    [System.Serializable]
    public struct Zone
    {
        public Vector2 center;
        public float   radius;
    }

    public List<Zone> zones = new();

    /// <summary>Returns true if the point is inside any zone.</summary>
    public bool Contains(Vector2 worldPos)
    {
        foreach (var z in zones)
            if (Vector2.SqrMagnitude(worldPos - z.center) <= z.radius * z.radius)
                return true;
        return false;
    }

    /// <summary>Returns true if a circle of the given radius overlaps any zone.</summary>
    public bool Overlaps(Vector2 worldPos, float radius)
    {
        foreach (var z in zones)
        {
            float minDist = z.radius + radius;
            if (Vector2.SqrMagnitude(worldPos - z.center) <= minDist * minDist)
                return true;
        }
        return false;
    }
}
