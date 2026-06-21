using UnityEngine;

/// <summary>
/// Generic component: holds a continuous damage beam on the tower's current target.
/// Damage ramps up the longer the lock is held; resets when target changes or dies.
/// JSON keys: damagePerSecond, maxRampMultiplier, rampDuration, beamWidth,
///            beamColorR, beamColorG, beamColorB, damageType (int)
/// </summary>
public class ContinuousBeam : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("continuous_beam", typeof(ContinuousBeam));

    public float     damagePerSecond   = 8f;
    public float     maxRampMultiplier = 5f;
    public float     rampDuration      = 4f;
    public float     beamWidth         = 0.06f;
    public DamageType damageType       = DamageType.Arcane;
    public Color     beamColor         = new Color(1f, 0.25f, 0.1f, 1f);

    private float           _lockTime;
    private UnitParentClass _locked;
    private LineRenderer    _beam;
    private TowerInfo       _info;
    private Turrent         _turrent;

    [System.Serializable]
    class Data
    {
        public float damagePerSecond   = 8f;
        public float maxRampMultiplier = 5f;
        public float rampDuration      = 4f;
        public float beamWidth         = 0.06f;
        public int   damageType        = 1;   // Arcane
        public float beamColorR        = 1f;
        public float beamColorG        = 0.25f;
        public float beamColorB        = 0.1f;
    }

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<Data>(dataJson);
        if (d == null) return;
        damagePerSecond   = d.damagePerSecond;
        maxRampMultiplier = d.maxRampMultiplier;
        rampDuration      = d.rampDuration;
        beamWidth         = d.beamWidth;
        damageType        = (DamageType)d.damageType;
        beamColor         = new Color(d.beamColorR, d.beamColorG, d.beamColorB, 1f);
    }

    void Awake()
    {
        _info    = GetComponent<TowerInfo>();
        _turrent = GetComponent<Turrent>();
        BuildBeam();
    }

    void BuildBeam()
    {
        var go             = new GameObject("[Beam]");
        go.transform.SetParent(transform, false);
        _beam              = go.AddComponent<LineRenderer>();
        _beam.positionCount = 2;
        _beam.useWorldSpace = true;
        _beam.sortingLayerName = "Units";
        _beam.sortingOrder  = 25;
        _beam.material      = new Material(Shader.Find("Sprites/Default"));
        _beam.enabled       = false;
    }

    void Update()
    {
        UnitParentClass target = _turrent != null && _turrent.target != null
            ? _turrent.target.GetComponent<UnitParentClass>() : null;

        if (target == null || !target.isAlive) { BreakLock(); return; }

        if (target != _locked) { _locked = target; _lockTime = 0f; }

        _lockTime += Time.deltaTime;
        float ramp  = Mathf.Lerp(1f, maxRampMultiplier, Mathf.Clamp01(_lockTime / rampDuration));
        float mult  = _info != null ? _info.StatMultiplier * _info.ExtraMultiplier : 1f;
        float dps   = damagePerSecond * ramp * mult;

        bool wasAlive = target.lifeCurrent > 0f;
        target.TakeDamage(dps * Time.deltaTime, 0f, 0f, dps * 10f, damageType);

        if (wasAlive && (target.lifeCurrent <= 0f || !target.isAlive))
        {
            _info?.RegisterKill();
            float physical = BalanceManager.Instance != null ? BalanceManager.Instance.Physical : 0f;
            if (Random.value <= 0.15f + physical * 0.0025f)
                BountyDrop.Spawn(target.transform.position, 1);
            BreakLock();
            return;
        }

        DrawBeam((Vector2)transform.position, (Vector2)target.transform.position);
    }

    void BreakLock()
    {
        _locked   = null;
        _lockTime = 0f;
        if (_beam != null) _beam.enabled = false;
    }

    void DrawBeam(Vector2 from, Vector2 to)
    {
        if (_beam == null) return;
        _beam.enabled = true;
        float w = beamWidth * Mathf.Lerp(1f, 2.5f, Mathf.Clamp01(_lockTime / rampDuration));
        _beam.startWidth = w;
        _beam.endWidth   = w * 0.3f;
        float pulse      = 0.75f + Mathf.Sin(Time.time * 18f) * 0.25f;
        Color c          = new Color(beamColor.r, beamColor.g, beamColor.b, pulse);
        _beam.startColor = c;
        _beam.endColor   = new Color(c.r, c.g, c.b, 0f);
        _beam.SetPosition(0, from);
        _beam.SetPosition(1, to);
    }

    void OnDisable() => BreakLock();
}
