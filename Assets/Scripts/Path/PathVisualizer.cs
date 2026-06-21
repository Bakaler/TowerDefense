using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders the PathGraph spline as LineRenderers so it's visible in the Game view.
/// Attach to the same GameObject as PathGraph.
///
/// Call RebuildLines() after editing nodes, or enable 'autoRebuildInEditor'
/// to refresh automatically whenever a node moves.
/// </summary>
public class PathVisualizer : MonoBehaviour
{
    [Header("Visibility")]
    [Tooltip("Show the path line in the Game view. Keep OFF for release — the path is hidden from players.")]
    public bool showInGame = false;

    [Header("Line Style")]
    public Color  pathColor      = new Color(1f, 0.75f, 0f, 0.85f);
    public float  lineWidth      = 0.08f;
    public int    sortingOrder   = 10;
    public string sortingLayer   = "Default";

    [Header("Arrow markers")]
    [Tooltip("Show direction arrows along the path (editor/debug only — disable for release)")]
    public bool  showArrows     = false;
    public float arrowSpacing   = 2f;   // world units between arrows
    public float arrowSize      = 0.25f;

    // ── Runtime lines ─────────────────────────────────────────────────
    private readonly List<LineRenderer> _lines   = new List<LineRenderer>();
    private readonly List<LineRenderer> _arrows  = new List<LineRenderer>();
    private GameObject                  _lineRoot;

    private PathGraph _graph;

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Awake()
    {
        ResolveGraph();
    }

    void ResolveGraph()
    {
        // Prefer the local PathGraph if it has nodes; otherwise use the scene singleton.
        _graph = GetComponent<PathGraph>();
        if (_graph == null || _graph.nodes.Count == 0)
            _graph = FindFirstObjectByType<PathGraph>();
    }

    void Start()
    {
        if (!showInGame)
            return; // path is hidden from players at runtime

        RebuildLines();
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>Destroy existing LineRenderers and rebuild from current graph state.</summary>
    [ContextMenu("Rebuild Lines")]
    public void RebuildLines()
    {
        // Clear old lines — use DestroyImmediate in edit mode, Destroy at runtime
        if (_lineRoot != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(_lineRoot);
            else
#endif
                Destroy(_lineRoot);
        }
        _lines.Clear();
        _arrows.Clear();

        if (_graph == null) ResolveGraph();
        if (_graph == null) _graph = GetComponent<PathGraph>();
        if (_graph == null || _graph.nodes == null) return;

        _lineRoot = new GameObject("_PathLines");
        _lineRoot.transform.SetParent(transform, worldPositionStays: false);

        foreach (var node in _graph.nodes)
        {
            if (node == null) continue;
            foreach (var next in node.connections)
            {
                if (next == null) continue;
                DrawEdge(node, next);
            }
        }
    }

    // ── Internal ──────────────────────────────────────────────────────

    void DrawEdge(PathNode from, PathNode to)
    {
        // Sample Catmull-Rom — mirror PathGraph's logic
        Vector2 p0 = GetPredecessorHint(from, to);
        Vector2 p1 = from.Position;
        Vector2 p2 = to.Position;
        Vector2 p3 = GetSuccessorHint(to);

        int samples = _graph.samplesPerSegment * 2;
        var points = new Vector3[samples + 1];
        points[0] = p1;
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            points[i] = CatmullRom(p0, p1, p2, p3, t);
        }

        // Main line
        var lr = CreateLine($"Edge_{from.name}_{to.name}");
        lr.positionCount = points.Length;
        lr.SetPositions(points);
        _lines.Add(lr);

        // Arrows
        if (showArrows)
            DrawArrows(points);
    }

    void DrawArrows(Vector3[] points)
    {
        float accumulated = 0f;
        float nextArrow   = arrowSpacing;

        for (int i = 1; i < points.Length; i++)
        {
            float seg = Vector3.Distance(points[i - 1], points[i]);
            accumulated += seg;

            while (accumulated >= nextArrow)
            {
                float overshoot = accumulated - nextArrow;
                float t         = 1f - (overshoot / seg);
                Vector3 pos     = Vector3.Lerp(points[i - 1], points[i], t);
                Vector3 dir     = (points[i] - points[i - 1]).normalized;

                // Draw a V-shaped arrow using two line segments
                Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
                Vector3 tip  = pos + dir * arrowSize;
                Vector3 lft  = pos - dir * arrowSize * 0.5f + perp * arrowSize * 0.5f;
                Vector3 rgt  = pos - dir * arrowSize * 0.5f - perp * arrowSize * 0.5f;

                var a1 = CreateLine("Arrow");
                a1.positionCount = 2;
                a1.SetPosition(0, tip); a1.SetPosition(1, lft);
                _arrows.Add(a1);

                var a2 = CreateLine("Arrow");
                a2.positionCount = 2;
                a2.SetPosition(0, tip); a2.SetPosition(1, rgt);
                _arrows.Add(a2);

                nextArrow += arrowSpacing;
            }
        }
    }

    LineRenderer CreateLine(string goName)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(_lineRoot.transform, worldPositionStays: false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace      = true;
        lr.startWidth         = lineWidth;
        lr.endWidth           = lineWidth;
        lr.sortingLayerName   = sortingLayer;
        lr.sortingOrder       = sortingOrder;
        lr.numCapVertices     = 4;
        lr.material           = GetLineMaterial();
        lr.startColor         = pathColor;
        lr.endColor           = pathColor;
        return lr;
    }

    private Material _lineMat;
    Material GetLineMaterial()
    {
        if (_lineMat == null)
            _lineMat = new Material(Shader.Find("Sprites/Default"));
        return _lineMat;
    }

    // ── Spline helpers (mirror of PathGraph) ──────────────────────────

    Vector2 GetPredecessorHint(PathNode node, PathNode nextNode)
    {
        foreach (var n in _graph.nodes)
        {
            if (n == null || n == node) continue;
            if (n.connections.Contains(node)) return n.Position;
        }
        return node.Position - (nextNode.Position - node.Position);
    }

    Vector2 GetSuccessorHint(PathNode node)
    {
        if (node.connections.Count > 0 && node.connections[0] != null)
            return node.connections[0].Position;
        return node.Position;
    }

    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }
}
