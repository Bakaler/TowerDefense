using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws placement zone outlines in the Game view using LineRenderers.
/// Created at runtime by LevelManager alongside PlacementZones.
/// </summary>
public class PlacementZoneVisualizer : MonoBehaviour
{
    [Tooltip("Show zone outlines during play. Turn off for release.")]
    public bool showInGame = true;

    public Color  zoneColor    = new Color(0.2f, 0.9f, 1f, 0.6f);
    public float  lineWidth    = 0.06f;
    public int    circleSegs   = 48;
    public string sortingLayer = "Default";
    public int    sortingOrder = 5;

    private readonly List<LineRenderer> _lines = new();
    private Material _mat;

    public void Build(PlacementZones zones)
    {
        Clear();
        if (!showInGame || zones == null) return;

        foreach (var z in zones.zones)
        {
            if (z.type == PlacementZones.ZoneType.Circle)
                BuildCircle(z.center, z.radius);
            else
                BuildLane(z);
        }
    }

    void Clear()
    {
        foreach (var lr in _lines)
            if (lr != null) Destroy(lr.gameObject);
        _lines.Clear();
    }

    void OnDestroy() => Clear();

    // ── Circle ────────────────────────────────────────────────────────

    void BuildCircle(Vector2 center, float radius)
    {
        var pts = new Vector3[circleSegs + 1];
        for (int i = 0; i <= circleSegs; i++)
        {
            float a = (float)i / circleSegs * Mathf.PI * 2f;
            pts[i]  = new Vector3(center.x + Mathf.Cos(a) * radius,
                                  center.y + Mathf.Sin(a) * radius, 0f);
        }
        AddLine(pts);
    }

    // ── Lane ──────────────────────────────────────────────────────────

    void BuildLane(PlacementZones.Zone z)
    {
        var pts = z.points;
        if (pts == null || pts.Length < 2) return;

        // Collect left edge, then right edge reversed, then close
        var left  = new List<Vector3>();
        var right = new List<Vector3>();

        for (int i = 0; i < pts.Length; i++)
        {
            float hwA = HalfWidth(z, i);
            Vector2 perp;

            if (i == 0)
                perp = Perp(pts[1] - pts[0]);
            else if (i == pts.Length - 1)
                perp = Perp(pts[i] - pts[i - 1]);
            else
            {
                Vector2 d1 = (pts[i]     - pts[i - 1]).normalized;
                Vector2 d2 = (pts[i + 1] - pts[i]    ).normalized;
                perp = Perp((d1 + d2) * 0.5f);
            }

            left.Add ((Vector3)(pts[i] + perp * hwA));
            right.Add((Vector3)(pts[i] - perp * hwA));
        }

        // Outline: left edge → right edge (reversed) → close
        var outline = new List<Vector3>();
        outline.AddRange(left);
        for (int i = right.Count - 1; i >= 0; i--)
            outline.Add(right[i]);
        outline.Add(left[0]); // close

        AddLine(outline.ToArray());
    }

    static Vector2 Perp(Vector2 d)
    {
        d = d.normalized;
        return new Vector2(-d.y, d.x);
    }

    static float HalfWidth(PlacementZones.Zone z, int i)
    {
        if (z.halfWidths != null && i < z.halfWidths.Length)
            return z.halfWidths[i];
        return z.halfWidth;
    }

    // ── LineRenderer helper ───────────────────────────────────────────

    void AddLine(Vector3[] pts)
    {
        var go = new GameObject("ZoneLine");
        go.transform.SetParent(transform);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace    = true;
        lr.positionCount    = pts.Length;
        lr.SetPositions(pts);
        lr.startWidth       = lineWidth;
        lr.endWidth         = lineWidth;
        lr.numCapVertices   = 2;
        lr.sortingLayerName = sortingLayer;
        lr.sortingOrder     = sortingOrder;
        lr.material         = GetMat();
        lr.startColor       = zoneColor;
        lr.endColor         = zoneColor;
        _lines.Add(lr);
    }

    Material GetMat()
    {
        if (_mat == null)
            _mat = new Material(Shader.Find("Sprites/Default"));
        return _mat;
    }
}
