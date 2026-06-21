using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds a continuous damage beam on the nearest enemy.
/// Damage ramps up the longer the lock is held; resets when target dies or leaves range.
/// Bypasses the normal ability cooldown — runs entirely in Update.
/// </summary>
public class LaserTower : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("laser_tower", typeof(LaserTower));

    // ── Tuning (set via JSON or defaults) ────────────────────────────
    public float baseDamagePerSecond = 8f;
    public float maxRampMultiplier   = 5f;
    public float rampDuration        = 4f;   // seconds to reach max ramp
    public float beamWidth           = 0.06f;
    public Color beamColor           = new Color(1f, 0.25f, 0.1f, 1f);

    // ── Runtime ───────────────────────────────────────────────────────
    private float          _lockTime;
    private UnitParentClass _lockedTarget;
    private LineRenderer   _beam;
    private TowerInfo      _info;
    private Turrent        _turrent;

    [System.Serializable]
    class Data
    {
        public float baseDamagePerSecond = 8f;
        public float maxRampMultiplier   = 5f;
        public float rampDuration        = 4f;
        public float beamWidth           = 0.06f;
        public float beamColorR          = 1f;
        public float beamColorG          = 0.25f;
        public float beamColorB          = 0.1f;
    }

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<Data>(dataJson);
        if (d == null) return;
        baseDamagePerSecond = d.baseDamagePerSecond;
        maxRampMultiplier   = d.maxRampMultiplier;
        rampDuration        = d.rampDuration;
        beamWidth           = d.beamWidth;
        beamColor           = new Color(d.beamColorR, d.beamColorG, d.beamColorB, 1f);
    }

    void Awake()
    {
        _info    = GetComponent<TowerInfo>();
        _turrent = GetComponent<Turrent>();
        BuildBeam();
    }

    void BuildBeam()
    {
        var go               = new GameObject("[LaserBeam]");
        go.transform.SetParent(transform, false);

        _beam                = go.AddComponent<LineRenderer>();
        _beam.positionCount  = 2;
        _beam.useWorldSpace  = true;
        _beam.startWidth     = beamWidth;
        _beam.endWidth       = beamWidth * 0.4f;
        _beam.sortingLayerName = "Units";
        _beam.sortingOrder   = 25;
        _beam.material       = new Material(Shader.Find("Sprites/Default"));
        _beam.enabled        = false;
    }

    void Update()
    {
        // Find current target from Turrent (reuse its range/lead logic)
        UnitParentClass target = null;
        if (_turrent != null && _turrent.target != null)
            target = _turrent.target.GetComponent<UnitParentClass>();

        if (target == null || !target.isAlive)
        {
            BreakLock();
            return;
        }

        // Switch target → reset ramp
        if (target != _lockedTarget)
        {
            _lockedTarget = target;
            _lockTime     = 0f;
        }

        _lockTime += Time.deltaTime;
        float ramp   = Mathf.Lerp(1f, maxRampMultiplier, Mathf.Clamp01(_lockTime / rampDuration));
        float mult   = _info != null ? _info.StatMultiplier * _info.ExtraMultiplier : 1f;
        float dps    = baseDamagePerSecond * ramp * mult;

        bool wasAlive = target.lifeCurrent > 0f;
        target.TakeDamage(dps * Time.deltaTime, 0f, 0f, dps * 10f, DamageType.Arcane);
        bool killed = wasAlive && (target.lifeCurrent <= 0f || !target.isAlive);
        if (killed)
        {
            _info?.RegisterKill();
            float physical = BalanceManager.Instance != null ? BalanceManager.Instance.Physical : 0f;
            if (Random.value <= 0.15f + physical * 0.0025f)
                BountyDrop.Spawn(target.transform.position, 1);
            BreakLock();
            return;
        }

        // Draw beam
        UpdateBeam((Vector2)transform.position, (Vector2)target.transform.position, ramp);
    }

    void BreakLock()
    {
        _lockedTarget = null;
        _lockTime     = 0f;
        if (_beam != null) _beam.enabled = false;
    }

    void UpdateBeam(Vector2 from, Vector2 to, float ramp)
    {
        if (_beam == null) return;
        _beam.enabled = true;

        // Flicker width with ramp intensity
        float w = beamWidth * Mathf.Lerp(1f, 2.5f, (_lockTime / rampDuration));
        _beam.startWidth = w;
        _beam.endWidth   = w * 0.3f;

        // Pulse alpha
        float pulse = 0.75f + Mathf.Sin(Time.time * 18f) * 0.25f;
        Color c     = new Color(beamColor.r, beamColor.g, beamColor.b, pulse);
        _beam.startColor = c;
        _beam.endColor   = new Color(c.r, c.g, c.b, 0f);

        _beam.SetPosition(0, from);
        _beam.SetPosition(1, to);
    }

    void OnDisable() => BreakLock();
}
