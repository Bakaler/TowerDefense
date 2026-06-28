using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// The sniper's repositionable firing oval.
/// While IsRepositioning: zone follows mouse, scroll wheel rotates it, left-click commits.
/// SniperTurrent skips firing while IsRepositioning is true.
/// </summary>
public class SniperZone : MonoBehaviour, IFactoryInitializable
{
    // ── Config ────────────────────────────────────────────────────────
    public float halfWidth  = 0.68f;  // semi-axis X (narrow)
    public float halfHeight = 2.98f;  // semi-axis Y (long)

    // ── State ─────────────────────────────────────────────────────────
    public Vector2 ZoneCenter      { get; private set; }
    public float   ZoneAngle       { get; private set; }   // degrees
    public bool    IsRepositioning { get; private set; }
    private bool   _selected       = false;
    private float  _outlineAlpha   = 0f;
    private Coroutine _fadeCoroutine;

    // ── Visuals ───────────────────────────────────────────────────────
    private LineRenderer _outline;
    private const int    OvalSegs = 48;

    // ── Registry ──────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("sniper_zone", typeof(SniperZone));

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start()
    {
        ZoneCenter = transform.position;
        BuildOutline();
        RefreshOutline();
    }

    void Update()
    {
        if (!IsRepositioning) return;

        bool dirty = false;

        // Follow mouse
        if (Camera.main != null)
        {
            Vector3 mp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            ZoneCenter = new Vector2(mp.x, mp.y);
            dirty = true;
        }

        // Scroll wheel rotates
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            ZoneAngle = (ZoneAngle + scroll * 72f) % 360f;
            dirty = true;
        }

        if (dirty) RefreshOutline();

        // Left click NOT on UI commits
        if (Input.GetMouseButtonDown(0) &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
        {
            IsRepositioning = false;
            RefreshOutline();
        }
    }

    // ── Public API ────────────────────────────────────────────────────

    public void BeginReposition()
    {
        IsRepositioning = true;
        if (_fadeCoroutine != null) { StopCoroutine(_fadeCoroutine); _fadeCoroutine = null; }
        _outlineAlpha = 0.95f;
        SetOutlineColor();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (_fadeCoroutine != null) { StopCoroutine(_fadeCoroutine); _fadeCoroutine = null; }

        if (selected)
        {
            _outlineAlpha = 0.75f;
            SetOutlineColor();
        }
        else
        {
            _fadeCoroutine = StartCoroutine(FadeOut());
        }
    }

    IEnumerator FadeOut()
    {
        float startAlpha = _outlineAlpha;
        float t = 0f;
        while (t < 3f)
        {
            t += Time.deltaTime;
            _outlineAlpha = Mathf.Lerp(startAlpha, 0f, t / 3f);
            SetOutlineColor();
            yield return null;
        }
        _outlineAlpha = 0f;
        SetOutlineColor();
        _fadeCoroutine = null;
    }

    /// <summary>Returns true if worldPos is inside the rotated oval.</summary>
    public bool Contains(Vector2 worldPos)
    {
        // Transform into oval's local space
        Vector2 delta = worldPos - ZoneCenter;
        float   rad   = ZoneAngle * Mathf.Deg2Rad;
        float   cos   = Mathf.Cos(-rad);
        float   sin   = Mathf.Sin(-rad);
        float   lx    = delta.x * cos - delta.y * sin;
        float   ly    = delta.x * sin + delta.y * cos;

        float nx = lx / halfWidth;
        float ny = ly / halfHeight;
        return nx * nx + ny * ny <= 1f;
    }

    // ── IFactoryInitializable ─────────────────────────────────────────

    [System.Serializable]
    private class ZoneData
    {
        public float halfWidth  = 0.8f;
        public float halfHeight = 3.5f;
    }

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<ZoneData>(dataJson);
        if (d == null) return;
        halfWidth  = d.halfWidth;
        halfHeight = d.halfHeight;
    }

    // ── LineRenderer oval outline ─────────────────────────────────────

    void BuildOutline()
    {
        var go = new GameObject("_ZoneOutline");
        go.transform.SetParent(transform);
        _outline = go.AddComponent<LineRenderer>();
        _outline.useWorldSpace    = true;
        _outline.loop             = true;
        _outline.positionCount    = OvalSegs;
        _outline.startWidth       = 0.06f;
        _outline.endWidth         = 0.06f;
        _outline.numCapVertices   = 2;
        _outline.sortingLayerName = "Default";
        _outline.sortingOrder     = 8;
        _outline.material         = new Material(Shader.Find("Sprites/Default"));
        SetOutlineColor();
    }

    void RefreshOutline()
    {
        if (_outline == null) return;

        float rad = ZoneAngle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        for (int i = 0; i < OvalSegs; i++)
        {
            float t  = (float)i / OvalSegs * Mathf.PI * 2f;
            float lx = Mathf.Cos(t) * halfWidth;
            float ly = Mathf.Sin(t) * halfHeight;

            // Rotate by ZoneAngle
            float wx = lx * cos - ly * sin;
            float wy = lx * sin + ly * cos;

            _outline.SetPosition(i, new Vector3(ZoneCenter.x + wx, ZoneCenter.y + wy, 0f));
        }

        SetOutlineColor();
    }

    void SetOutlineColor()
    {
        if (_outline == null) return;
        Color col = IsRepositioning
            ? new Color(1f, 0.85f, 0.2f, _outlineAlpha)
            : new Color(0.55f, 0.85f, 1f, _outlineAlpha);
        _outline.startColor = col;
        _outline.endColor   = col;
    }
}
