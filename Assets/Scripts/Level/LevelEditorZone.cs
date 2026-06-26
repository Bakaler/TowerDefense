using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a placement zone. Circle: position + radius. Lane: ordered Point_N children
/// with LevelEditorLanePoint components; each point has its own width.
/// </summary>
public class LevelEditorZone : MonoBehaviour
{
    public enum ZoneType { Circle, Lane }

    public ZoneType type   = ZoneType.Circle;
    public float    radius = 2f;

    // ── Point helpers ─────────────────────────────────────────────────

    public List<LevelEditorLanePoint> GetPoints()
    {
        var list = new List<LevelEditorLanePoint>();
        foreach (Transform child in transform)
        {
            var p = child.GetComponent<LevelEditorLanePoint>();
            if (p != null) list.Add(p);
        }
        return list;
    }

    public LevelEditorLanePoint AddPoint(Vector3 worldPos, float width = 3f)
    {
        int idx = GetPoints().Count;
        var go  = new GameObject($"Point_{idx}");
        go.transform.SetParent(transform);
        go.transform.position = worldPos;
        var lp  = go.AddComponent<LevelEditorLanePoint>();
        lp.width = width;
        return lp;
    }

    // ── Gizmos ───────────────────────────────────────────────────────

    void OnDrawGizmos()         => DrawZone(false);
    void OnDrawGizmosSelected() => DrawZone(true);

    void DrawZone(bool selected)
    {
        if (type == ZoneType.Circle)
        {
            Gizmos.color = selected
                ? new Color(1f, 1f, 0.3f, 0.9f)
                : new Color(0.2f, 0.85f, 1f, 0.7f);
            DrawCircleWire(transform.position, radius, 48);
            Gizmos.color = selected
                ? new Color(1f, 1f, 0.3f, 0.1f)
                : new Color(0.2f, 0.85f, 1f, 0.07f);
            DrawCircleFill(transform.position, radius, 48);
            return;
        }

        var pts = GetPoints();
        if (pts.Count < 1) return;

        Color outline = selected ? new Color(1f, 1f, 0.3f, 0.9f) : new Color(0.4f, 1f, 0.55f, 0.85f);
        Color fill    = selected ? new Color(1f, 1f, 0.3f, 0.1f) : new Color(0.4f, 1f, 0.55f, 0.08f);

        // Draw centerline
        Gizmos.color = outline;
        for (int i = 0; i < pts.Count - 1; i++)
            Gizmos.DrawLine(pts[i].transform.position, pts[i + 1].transform.position);

        // Draw variable-width ribbon segments
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 a   = pts[i].transform.position;
            Vector3 b   = pts[i + 1].transform.position;
            float   hwA = pts[i].width * 0.5f;
            float   hwB = pts[i + 1].width * 0.5f;

            Vector3 dir  = (b - a).normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

            Vector3 aL = a + perp * hwA;
            Vector3 aR = a - perp * hwA;
            Vector3 bL = b + perp * hwB;
            Vector3 bR = b - perp * hwB;

            Gizmos.color = outline;
            Gizmos.DrawLine(aL, bL);
            Gizmos.DrawLine(aR, bR);

            Gizmos.color = fill;
            Gizmos.DrawLine(aL, bR);
            Gizmos.DrawLine(aR, bL);
        }

        // End caps
        if (pts.Count >= 1)
        {
            DrawCapArc(pts[0].transform.position, pts.Count > 1 ? pts[1].transform.position : pts[0].transform.position, pts[0].width * 0.5f, outline, true);
            int last = pts.Count - 1;
            DrawCapArc(pts[last].transform.position, pts.Count > 1 ? pts[last - 1].transform.position : pts[last].transform.position, pts[last].width * 0.5f, outline, false);
        }
    }

    static void DrawCapArc(Vector3 centre, Vector3 towards, float r, Color col, bool flip)
    {
        Vector3 dir  = (towards - centre).normalized;
        if (dir == Vector3.zero) dir = Vector3.right;
        // Draw a half-circle on the end facing away from the path
        Gizmos.color = col;
        int seg = 16;
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + (flip ? 90f : -90f);
        Vector3 prev = centre + Quaternion.Euler(0, 0, baseAngle) * Vector3.right * r;
        for (int i = 1; i <= seg; i++)
        {
            float angle = baseAngle + (flip ? -1f : 1f) * 180f * i / seg;
            Vector3 pt  = centre + Quaternion.Euler(0, 0, angle) * Vector3.right * r;
            Gizmos.DrawLine(prev, pt);
            prev = pt;
        }
    }

    // ── Circle helpers ────────────────────────────────────────────────

    static void DrawCircleWire(Vector3 c, float r, int seg)
    {
        Vector3 prev = c + new Vector3(r, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = (float)i / seg * Mathf.PI * 2f;
            var   p = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    static void DrawCircleFill(Vector3 c, float r, int seg)
    {
        for (int i = 0; i < seg; i++)
        {
            float a0 = (float)i       / seg * Mathf.PI * 2f;
            float a1 = (float)(i + 1) / seg * Mathf.PI * 2f;
            Gizmos.DrawLine(c, c + new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r));
            Gizmos.DrawLine(c, c + new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r));
        }
    }
}
