using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TowerDefense/Placement Zones", fileName = "PlacementZones")]
public class PlacementZones : ScriptableObject
{
    public enum ZoneType { Circle, Lane }

    [System.Serializable]
    public class Zone
    {
        public ZoneType  type;
        // Circle
        public Vector2   center;
        public float     radius;
        // Lane — parallel arrays; halfWidths[i] applies to segment i→i+1
        public Vector2[] points     = System.Array.Empty<Vector2>();
        public float[]   halfWidths = System.Array.Empty<float>();  // per-point, length == points.Length
        public float     halfWidth  = 1.5f;  // fallback when halfWidths is empty
    }

    public List<Zone> zones = new();

    public bool Contains(Vector2 p)
    {
        foreach (var z in zones)
        {
            if (z.type == ZoneType.Circle)
            {
                if (Vector2.SqrMagnitude(p - z.center) <= z.radius * z.radius)
                    return true;
            }
            else if (MinDistToPolyline(p, z) <= 0f)
                return true;
        }
        return false;
    }

    public bool Overlaps(Vector2 p, float r)
    {
        foreach (var z in zones)
        {
            if (z.type == ZoneType.Circle)
            {
                float d = z.radius + r;
                if (Vector2.SqrMagnitude(p - z.center) <= d * d)
                    return true;
            }
            else if (MinDistToPolyline(p, z) <= r)
                return true;
        }
        return false;
    }

    // Returns (dist - halfWidth) for the nearest segment; negative means inside the zone.
    static float MinDistToPolyline(Vector2 p, Zone z)
    {
        var pts = z.points;
        if (pts == null || pts.Length == 0) return float.MaxValue;
        if (pts.Length == 1)
        {
            float hw = HalfWidthAt(z, 0, 0);
            return Vector2.Distance(p, pts[0]) - hw;
        }

        float best = float.MaxValue;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            float hwA = HalfWidthAt(z, i,     i);
            float hwB = HalfWidthAt(z, i + 1, i);
            float dist = DistToSegment(p, pts[i], pts[i + 1]);
            // Interpolate halfWidth along the segment
            float t  = SegmentT(p, pts[i], pts[i + 1]);
            float hw = Mathf.Lerp(hwA, hwB, t);
            best = Mathf.Min(best, dist - hw);
        }
        return best;
    }

    static float HalfWidthAt(Zone z, int ptIndex, int segIndex)
    {
        if (z.halfWidths != null && ptIndex < z.halfWidths.Length)
            return z.halfWidths[ptIndex];
        return z.halfWidth;
    }

    static float SegmentT(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a, ap = p - a;
        float lenSq = Vector2.Dot(ab, ab);
        if (lenSq < 0.0001f) return 0f;
        return Mathf.Clamp01(Vector2.Dot(ap, ab) / lenSq);
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a, ap = p - a;
        float lenSq = Vector2.Dot(ab, ab);
        if (lenSq < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / lenSq);
        return Vector2.Distance(p, a + ab * t);
    }
}
