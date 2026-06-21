using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic aura component: buffs attack speed and/or damage of all towers within range.
/// JSON keys: range, damageMultiplier, attackSpeedMultiplier, auraColorR/G/B
/// Bonus scales with this tower's StatMultiplier when upgraded.
/// </summary>
public class TowerAura : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("tower_aura", typeof(TowerAura));

    public float range                 = 5f;
    public float damageMultiplier      = 1f;
    public float attackSpeedMultiplier = 1f;
    public Color auraColor             = new Color(0.3f, 0.8f, 1f, 0.45f);

    private TowerInfo        _info;
    private const float      PULSE  = 0.5f;
    private float            _timer;
    private readonly HashSet<TowerInfo> _inRange = new();

    [System.Serializable]
    class Data
    {
        public float range                 = 5f;
        public float damageMultiplier      = 1f;
        public float attackSpeedMultiplier = 1f;
        public float auraColorR = 0.3f, auraColorG = 0.8f, auraColorB = 1f;
    }

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<Data>(dataJson);
        if (d == null) return;
        range                 = d.range;
        damageMultiplier      = d.damageMultiplier;
        attackSpeedMultiplier = d.attackSpeedMultiplier;
        auraColor             = new Color(d.auraColorR, d.auraColorG, d.auraColorB, 0.45f);
    }

    void Awake() => _info = GetComponent<TowerInfo>();

    void Start()
    {
        BuildAuraRing();
        Pulse();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= PULSE) { _timer = 0f; Pulse(); }
    }

    void OnDestroy() => ClearAura();
    void OnDisable() => ClearAura();
    void OnEnable()  { foreach (var t in _inRange) if (t) ApplyTo(t); }

    void Pulse()
    {
        float statMult    = _info != null ? _info.StatMultiplier : 1f;
        float effectiveDmg = 1f + (damageMultiplier      - 1f) * statMult;
        float effectiveSpd = 1f + (attackSpeedMultiplier - 1f) * statMult;

        var hits    = Physics2D.OverlapCircleAll(transform.position, range);
        var nowSet  = new HashSet<TowerInfo>();

        foreach (var col in hits)
        {
            var t = col.GetComponent<TowerInfo>();
            if (t == null || t == _info || t.isGhost) continue;
            nowSet.Add(t);
        }

        foreach (var t in _inRange)
            if (!nowSet.Contains(t) && t) t.RemoveAura(this);

        foreach (var t in nowSet)
            if (t) t.ApplyAura(this, effectiveDmg, effectiveSpd);

        _inRange.Clear();
        foreach (var t in nowSet) _inRange.Add(t);
    }

    void ApplyTo(TowerInfo t)
    {
        float statMult     = _info != null ? _info.StatMultiplier : 1f;
        float effectiveDmg = 1f + (damageMultiplier      - 1f) * statMult;
        float effectiveSpd = 1f + (attackSpeedMultiplier - 1f) * statMult;
        t.ApplyAura(this, effectiveDmg, effectiveSpd);
    }

    void ClearAura()
    {
        foreach (var t in _inRange)
            if (t) t.RemoveAura(this);
        _inRange.Clear();
    }

    void BuildAuraRing()
    {
        const int SEG = 64;
        var go = new GameObject("AuraRing");
        go.transform.SetParent(transform, false);

        float scale       = Mathf.Max(0.01f, transform.localScale.x);
        float localRadius = range / scale;

        var lr              = go.AddComponent<LineRenderer>();
        lr.loop             = true;
        lr.positionCount    = SEG;
        lr.startWidth       = 0.06f;
        lr.endWidth         = 0.06f;
        lr.useWorldSpace    = false;
        lr.sortingLayerName = "Units";
        lr.sortingOrder     = 18;
        lr.material         = new Material(Shader.Find("Sprites/Default"));
        lr.startColor       = auraColor;
        lr.endColor         = auraColor;

        for (int i = 0; i < SEG; i++)
        {
            float a = i / (float)SEG * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * localRadius, Mathf.Sin(a) * localRadius, 0f));
        }
    }
}
