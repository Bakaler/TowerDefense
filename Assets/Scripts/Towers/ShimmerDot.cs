using UnityEngine;

/// <summary>
/// Self-destroying sprite that fades out and shrinks over its lifetime.
/// Spawned by ResearchOrb to create a shimmer trail.
/// </summary>
public class ShimmerDot : MonoBehaviour
{
    public float life = 1.1f;

    private SpriteRenderer _sr;
    private float          _elapsed;
    private Vector3        _startScale;
    private Color          _startColor;

    void Awake()
    {
        _sr         = GetComponent<SpriteRenderer>();
        _startScale = transform.localScale;
        _startColor = _sr != null ? _sr.color : Color.white;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        float t = _elapsed / life;          // 0 → 1

        if (t >= 1f) { Destroy(gameObject); return; }

        // Cubic fade — drops quickly
        float inv   = 1f - t;
        float alpha = inv * inv * inv;
        if (_sr != null)
        {
            var c = _startColor;
            c.a  = _startColor.a * alpha;
            _sr.color = c;
        }

        transform.localScale = _startScale * Mathf.Lerp(1f, 0.2f, t);
    }
}
