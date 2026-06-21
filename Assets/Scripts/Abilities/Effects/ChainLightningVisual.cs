using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws a jagged lightning bolt through a list of world positions then fades and destroys itself.
/// Spawned automatically by Effect_Chain_Lightning — no manual setup needed. 
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ChainLightningVisual : MonoBehaviour
{
    [Tooltip("How long the bolt stays at full brightness before fading.")]
    public float holdTime  = 0.05f;
    [Tooltip("How long the fade-out lasts.")]
    public float fadeTime  = 0.30f;
    [Tooltip("World-unit perpendicular jitter applied to each midpoint for a jagged look.")]
    public float jitter    = 0.25f;
    [Tooltip("Extra sub-segments injected between each pair of anchor points.")]
    public int   subPoints = 3;

    private LineRenderer _lr;

    // ── Public API ────────────────────────────────────────────────────

    public void SetPath(List<Vector3> anchors)
    {
        _lr = GetComponent<LineRenderer>();

        // Build the jittered point list
        var pts = BuildJaggedPath(anchors);

        _lr.positionCount = pts.Count;
        _lr.SetPositions(pts.ToArray());

        // Diamond profile: thin at both ends, wide in the middle
        var curve = new AnimationCurve(
            new Keyframe(0f,    0.005f),
            new Keyframe(0.15f, 0.04f),
            new Keyframe(0.5f,  0.03f),
            new Keyframe(0.85f, 0.04f),
            new Keyframe(1f,    0.005f));
        _lr.widthCurve = curve;
        _lr.widthMultiplier = 1f;
        _lr.useWorldSpace  = true;
        _lr.sortingLayerName = "Towers";
        _lr.sortingOrder     = 20;

        // Material — Unity's built-in sprite/default works; bright cyan tint
        _lr.material = new Material(Shader.Find("Sprites/Default"));
        _lr.startColor = new Color(0.6f, 0.9f, 1f, 1f);
        _lr.endColor   = new Color(0.3f, 0.6f, 1f, 1f);

        StartCoroutine(FadeRoutine());
    }

    // ── Internal ──────────────────────────────────────────────────────

    List<Vector3> BuildJaggedPath(List<Vector3> anchors)
    {
        var pts = new List<Vector3>();
        for (int i = 0; i < anchors.Count - 1; i++)
        {
            pts.Add(anchors[i]);

            Vector3 a   = anchors[i];
            Vector3 b   = anchors[i + 1];
            Vector3 dir = (b - a);
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f).normalized;

            for (int s = 1; s <= subPoints; s++)
            {
                float   t      = s / (float)(subPoints + 1);
                Vector3 lerped = Vector3.Lerp(a, b, t);
                float   offset = Random.Range(-jitter, jitter);
                pts.Add(lerped + perp * offset);
            }
        }
        pts.Add(anchors[anchors.Count - 1]);
        return pts;
    }

    IEnumerator FadeRoutine()
    {
        // Hold
        yield return new WaitForSeconds(holdTime);

        // Fade
        float elapsed = 0f;
        Color startC  = _lr.startColor;
        Color endC    = _lr.endColor;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t   = elapsed / fadeTime;
            float a   = 1f - t;
            _lr.startColor = new Color(startC.r, startC.g, startC.b, a);
            _lr.endColor   = new Color(endC.r,   endC.g,   endC.b,   a);
            yield return null;
        }

        Destroy(gameObject);
    }
}
