using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A tech orb that travels a curved spline from off-screen toward its research tower.
/// Click it in-flight for full value; let it arrive for half value.
/// </summary>
public class ResearchOrb : MonoBehaviour
{
    // ── Config ────────────────────────────────────────────────────────
    public int   fullValue    = 2;
    public int   arrivalValue = 1;
    public float travelTime   = 9f;
    public float clickRadius  = 0.45f;

    private static readonly Color OrbTint     = new Color(0.35f, 1f, 0.45f, 1f);
    private static readonly Color ShimmerTint = new Color(0.35f, 1f, 0.45f, 0.7f);

    // ── Internal ──────────────────────────────────────────────────────
    private Vector3 _p0, _p1, _p2;   // quadratic bezier control points
    private float   _t;
    private bool    _done;

    // shimmer trail
    private float _shimmerAccum;
    private const float ShimmerInterval = 0.10f;  // spawn a dot every N seconds
    private const float ShimmerLife     = 0.35f;

    // ── Factory ───────────────────────────────────────────────────────

    public static ResearchOrb Spawn(Vector3 towerWorldPos, string spriteSheet, int spriteIndex,
                                    int fullValue, int arrivalValue, float travelTime)
    {
        Vector3 spawnPos = GetOffscreenPosition(towerWorldPos);

        var go = new GameObject("ResearchOrb");
        go.transform.position   = spawnPos;
        go.transform.localScale = Vector3.one * 0.6f;

        // Sprite
        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Units";
        sr.sortingOrder     = 25;
        sr.color            = OrbTint;

        if (!string.IsNullOrEmpty(spriteSheet))
        {
            var sheet = Resources.LoadAll<Sprite>(spriteSheet);
            if (sheet != null && spriteIndex < sheet.Length)
                sr.sprite = sheet[spriteIndex];
        }

        var orb = go.AddComponent<ResearchOrb>();
        orb.fullValue    = fullValue;
        orb.arrivalValue = arrivalValue;
        orb.travelTime   = travelTime;
        orb.Init(spawnPos, towerWorldPos);
        return orb;
    }

    void Init(Vector3 from, Vector3 to)
    {
        _p0 = from;
        _p2 = to;

        // Curved control point — offset perpendicular to the straight line
        Vector3 mid  = Vector3.Lerp(from, to, 0.5f);
        Vector3 dir  = to - from;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f).normalized;
        float   mag  = dir.magnitude;
        _p1 = mid + perp * Random.Range(-0.35f, 0.35f) * mag;
    }

    void SpawnShimmerDot(Vector3 pos)
    {
        var go              = new GameObject("ShimmerDot");
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * Random.Range(0.10f, 0.22f);

        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sprite           = GetComponent<SpriteRenderer>()?.sprite;
        sr.color            = ShimmerTint;
        sr.sortingLayerName = "Units";
        sr.sortingOrder     = 23;

        var dot             = go.AddComponent<ShimmerDot>();
        dot.life            = ShimmerLife * Random.Range(0.7f, 1.0f);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Update()
    {
        if (_done) return;

        _t += Time.deltaTime / travelTime;

        if (_t >= 1f) { Arrive(); return; }

        transform.position = Bezier(_t);

        // Shimmer trail — spawn fading dots behind the orb
        _shimmerAccum += Time.deltaTime;
        if (_shimmerAccum >= ShimmerInterval)
        {
            _shimmerAccum -= ShimmerInterval;
            SpawnShimmerDot(transform.position);
        }

        // Click detection (distance-based, bypasses colliders)
        if (Input.GetMouseButtonDown(0) &&
            !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) &&
            !(TowerPlacer.Instance != null && TowerPlacer.Instance.IsPlacing))
        {
            Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            if (Vector2.Distance(mouse, transform.position) <= clickRadius)
                Collect(fullValue);
        }
    }

    void Arrive()
    {
        Collect(arrivalValue);
    }

    public void CollectByTower() => Collect(fullValue);

    void Collect(int value)
    {
        if (_done) return;
        _done = true;
        TechManager.Instance?.AddTech(value);
        Destroy(gameObject);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    Vector3 Bezier(float t)
    {
        float u = 1f - t;
        return u * u * _p0 + 2f * u * t * _p1 + t * t * _p2;
    }

    static Vector3 GetOffscreenPosition(Vector3 toward)
    {
        var cam = Camera.main;
        if (cam == null) return toward + Vector3.left * 12f;

        float h   = cam.orthographicSize + 1.5f;
        float w   = h * cam.aspect + 1.5f;
        Vector3 c = cam.transform.position;

        // Pick a random screen edge
        return Random.Range(0, 4) switch
        {
            0 => new Vector3(c.x + Random.Range(-w, w), c.y + h, 0f),   // top
            1 => new Vector3(c.x + Random.Range(-w, w), c.y - h, 0f),   // bottom
            2 => new Vector3(c.x - w, c.y + Random.Range(-h, h), 0f),   // left
            _ => new Vector3(c.x + w, c.y + Random.Range(-h, h), 0f),   // right
        };
    }
}
