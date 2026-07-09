using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fence tower body: the tower root is post A, and SetEndpoint() (called by
/// TowerPlacer after the second placement click) plants post B as a child.
/// A line is drawn between the posts. The fence holds charges: each enemy
/// that crosses the line consumes one charge and gets zapped with effectId
/// (an effect set — e.g. vulnerability behavior + burst damage). Charges
/// regenerate every rechargeSeconds; the beam is hidden while empty.
/// Upgrading the tower adds chargesPerTier per tier.
///
/// Crossing uses segment-vs-segment intersection against each unit's
/// previous → current position, so fast units can't tunnel through.
///
/// JSON keys: effectId, behaviorId (fallback), maxCharges, chargesPerTier,
///            rechargeSeconds, lineWidth, colorR/G/B/A
/// </summary>
public class FenceLine : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("fence_line", typeof(FenceLine));

    public string effectId        = "";
    public string behaviorId      = "";
    public int    maxCharges      = 1;
    public int    chargesPerTier  = 1;
    public float  rechargeSeconds = 12f;
    public float  lineWidth       = 0.12f;
    public Color  lineColor       = new Color(1f, 0.85f, 0.3f, 0.85f);

    /// <summary>Extra lean added to each post on top of facing its partner —
    /// compensates for the art being drawn slightly off-axis. Shared with
    /// TowerPlacer so the placement previews match the final orientation.</summary>
    public const float PostAngleOffset = 5f;

    /// <summary>Extra query padding so units moving fast still get caught.</summary>
    const float PAD = 1.5f;
    /// <summary>Seconds the line stays brightened after zapping a unit.</summary>
    const float FLASH_TIME = 0.25f;

    private Vector2      _endB;
    private bool         _hasEndpoint;
    private LineRenderer _line;
    private float        _flashTimer;
    private int          _enemyMask;
    private TowerInfo    _info;
    private int          _charges;
    private float        _rechargeTimer;

    /// <summary>Charge cap grows with the tower's upgrade tier.</summary>
    public int MaxCharges =>
        maxCharges + chargesPerTier * ((_info != null ? _info.Tier : 1) - 1);
    public int Charges => _charges;

    private readonly Dictionary<UnitManager, Vector2> _prevPos = new();
    private readonly HashSet<UnitManager>             _seen    = new();
    private static readonly List<UnitManager>         _stale   = new();

    [System.Serializable]
    class Data
    {
        public string effectId        = "";
        public string behaviorId      = "";
        public int    maxCharges      = 1;
        public int    chargesPerTier  = 1;
        public float  rechargeSeconds = 12f;
        public float  lineWidth       = 0.12f;
        public float  colorR = 1f, colorG = 0.85f, colorB = 0.3f, colorA = 0.85f;
    }

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<Data>(dataJson);
        if (d == null) return;
        effectId        = d.effectId;
        behaviorId      = d.behaviorId;
        maxCharges      = Mathf.Max(1, d.maxCharges);
        chargesPerTier  = Mathf.Max(0, d.chargesPerTier);
        rechargeSeconds = Mathf.Max(0.5f, d.rechargeSeconds);
        lineWidth       = d.lineWidth;
        lineColor       = new Color(d.colorR, d.colorG, d.colorB, d.colorA);
    }

    void Awake()
    {
        _enemyMask = LayerMask.GetMask("Enemy");
        _info      = GetComponent<TowerInfo>();
    }

    private GameObject _postB;

    /// <summary>Called by TowerPlacer after the tower is built at post A.</summary>
    public void SetEndpoint(Vector2 worldB)
    {
        _endB        = worldB;
        _hasEndpoint = true;
        _charges     = MaxCharges;

        // Each post's right side (+X) faces its partner: post A points at B,
        // post B (child, local 180°) points back at A — the parent's angle offset
        // carries into B's own frame with the same sense. Rotate before building
        // post B so its local position is computed in the rotated frame.
        Vector2 dir = worldB - (Vector2)transform.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + PostAngleOffset);

        BuildPostB(worldB);
        BuildLine();
    }

    void BuildPostB(Vector2 worldB)
    {
        _postB = new GameObject("PostB");
        _postB.transform.SetParent(transform, false);
        _postB.transform.localPosition = transform.InverseTransformPoint(worldB);
        _postB.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);   // right side faces post A

        _postB.AddComponent<SpriteRenderer>();
        SyncPostVisual();

        // Mirror the root's solid body collider so other towers can't be
        // placed on top of post B (TowerPlacer checks GetComponentInParent).
        foreach (var col in GetComponents<CircleCollider2D>())
        {
            if (col.isTrigger) continue;
            var bodyCol       = _postB.AddComponent<CircleCollider2D>();
            bodyCol.radius    = col.radius;
            bodyCol.isTrigger = false;
            break;
        }
    }

    /// <summary>
    /// Copies the root post's sprite and walk animation onto post B so both posts
    /// look identical. Called on build and again by TowerInfo.RefreshSprite after
    /// upgrades swap the tier art.
    /// </summary>
    public void SyncPostVisual()
    {
        if (_postB == null) return;

        var rootSr = GetComponent<SpriteRenderer>();
        var sr     = _postB.GetComponent<SpriteRenderer>();
        if (rootSr != null && sr != null)
        {
            sr.sprite           = rootSr.sprite;
            sr.color            = rootSr.color;
            sr.sortingLayerName = rootSr.sortingLayerName;
            sr.sortingOrder     = rootSr.sortingOrder;
        }

        var rootAnim = GetComponent<SpriteAnimator>();
        if (rootAnim != null && rootAnim.WalkFrames != null && rootAnim.WalkFrames.Length > 1)
        {
            var anim = _postB.GetComponent<SpriteAnimator>() ?? _postB.AddComponent<SpriteAnimator>();
            anim.Setup(rootAnim.WalkFrames, rootAnim.WalkFps);
        }
    }

    void BuildLine()
    {
        var go = new GameObject("FenceBeam");
        go.transform.SetParent(transform, false);

        _line                  = go.AddComponent<LineRenderer>();
        _line.positionCount    = 2;
        _line.useWorldSpace    = true;
        _line.startWidth       = lineWidth;
        _line.endWidth         = lineWidth;
        _line.sortingLayerName = "Units";
        _line.sortingOrder     = 15;
        _line.material         = new Material(Shader.Find("Sprites/Default"));
        _line.startColor       = lineColor;
        _line.endColor         = lineColor;
        _line.SetPosition(0, transform.position);
        _line.SetPosition(1, new Vector3(_endB.x, _endB.y, 0f));
    }

    void Update()
    {
        if (!_hasEndpoint) return;

        // Recharge — one charge per rechargeSeconds while below cap
        if (_charges < MaxCharges)
        {
            _rechargeTimer += Time.deltaTime;
            if (_rechargeTimer >= rechargeSeconds)
            {
                _rechargeTimer -= rechargeSeconds;
                _charges++;
            }
        }
        else _rechargeTimer = 0f;

        // The fence is only visible while it has charges
        if (_line != null) _line.enabled = _charges > 0;

        // Fade the hit-flash back to the base color
        if (_flashTimer > 0f && _line != null)
        {
            _flashTimer -= Time.deltaTime;
            Color c = Color.Lerp(lineColor, Color.white, Mathf.Clamp01(_flashTimer / FLASH_TIME));
            _line.startColor = c;
            _line.endColor   = c;
        }

        Vector2 a      = transform.position;
        Vector2 b      = _endB;
        Vector2 center = (a + b) * 0.5f;
        float   radius = (b - a).magnitude * 0.5f + PAD;

        _seen.Clear();
        var hits = Physics2D.OverlapCircleAll(center, radius, _enemyMask);
        foreach (var col in hits)
        {
            var u = col.GetComponent<UnitManager>();
            if (u == null || !u.isAlive) continue;

            Vector2 cur = u.transform.position;
            _seen.Add(u);

            if (_charges > 0 &&
                _prevPos.TryGetValue(u, out Vector2 prev) &&
                SegmentsIntersect(prev, cur, a, b))
            {
                _charges--;
                ZapUnit(u);
            }

            _prevPos[u] = cur;
        }

        // Drop units that died or left the query circle
        _stale.Clear();
        foreach (var kvp in _prevPos)
            if (kvp.Key == null || !_seen.Contains(kvp.Key)) _stale.Add(kvp.Key);
        foreach (var u in _stale)
            _prevPos.Remove(u);
    }

    void ZapUnit(UnitManager unit)
    {
        _flashTimer = FLASH_TIME;

        // Preferred path: execute an effect set (behavior + damage) on the unit
        if (!string.IsNullOrEmpty(effectId) && EffectLibrary.Instance != null)
        {
            var effect = EffectLibrary.Instance.GetEffect(effectId);
            if (effect != null)
            {
                var ctx = new EffectContext
                {
                    CasterTransform = transform,
                    Target          = unit,
                    TargetPoint     = unit.transform.position,
                    AimOrigin2D     = (Vector2)transform.position,
                    OriginTower     = gameObject,
                    CustomData      = new Dictionary<string, object>(),
                    AimDirection2D  = ((Vector2)(unit.transform.position - transform.position)).normalized,
                };
                effect.Execute(ctx);
                return;
            }
            Debug.LogWarning($"[FenceLine] Unknown effectId '{effectId}'.");
        }

        // Fallback: apply a plain behavior
        if (string.IsNullOrEmpty(behaviorId)) return;
        if (BehaviorLibrary.Instance == null ||
            !BehaviorLibrary.Instance.TryGet(behaviorId, out var def))
        {
            Debug.LogWarning($"[FenceLine] Unknown behaviorId '{behaviorId}'.");
            return;
        }

        var handler = unit.GetComponent<BehaviorHandler>()
                   ?? unit.gameObject.AddComponent<BehaviorHandler>();
        handler.Apply(def);
    }

    /// <summary>Standard 2D segment intersection via orientation tests.</summary>
    static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float d1 = Cross(q2 - q1, p1 - q1);
        float d2 = Cross(q2 - q1, p2 - q1);
        float d3 = Cross(p2 - p1, q1 - p1);
        float d4 = Cross(p2 - p1, q2 - p1);

        if (((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) &&
            ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f)))
            return true;

        // Collinear endpoint touches
        if (d1 == 0f && OnSegment(q1, q2, p1)) return true;
        if (d2 == 0f && OnSegment(q1, q2, p2)) return true;
        if (d3 == 0f && OnSegment(p1, p2, q1)) return true;
        if (d4 == 0f && OnSegment(p1, p2, q2)) return true;
        return false;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static bool OnSegment(Vector2 a, Vector2 b, Vector2 p) =>
        Mathf.Min(a.x, b.x) <= p.x && p.x <= Mathf.Max(a.x, b.x) &&
        Mathf.Min(a.y, b.y) <= p.y && p.y <= Mathf.Max(a.y, b.y);
}
