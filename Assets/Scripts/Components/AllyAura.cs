using System;
using UnityEngine;

/// <summary>
/// Generic ally-support aura: on spawn or on an interval, applies a behavior to
/// and/or cleanses behavior types from all nearby allies (self included).
/// Replaces the bespoke ShielderAura and PriestAura components.
/// JSON keys: radius, interval (0 = once on spawn), applyBehaviorId,
///            cleanseTypes (e.g. ["Slowed","Rooted","Debuff"]), castPause
/// </summary>
public class AllyAura : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("ally_aura", typeof(AllyAura));

    public float    radius          = 3.5f;
    /// <summary>Seconds between pulses. 0 or less = pulse once on spawn.</summary>
    public float    interval        = 0f;
    /// <summary>Behavior granted to allies each pulse (behaviors.json), e.g. "shielded".</summary>
    public string   applyBehaviorId = "";
    /// <summary>BehaviorType names removed from allies each pulse.</summary>
    public string[] cleanseTypes    = Array.Empty<string>();
    /// <summary>Brief stop + white flash after a pulse that did something. 0 = no visual.</summary>
    public float    castPause       = 0f;

    private float          _timer;
    private SpriteRenderer _sr;
    private UnitManager    _unit;
    private Color          _baseColor;

    [Serializable]
    class Data
    {
        public float    radius          = 3.5f;
        public float    interval        = 0f;
        public string   applyBehaviorId = "";
        public string[] cleanseTypes    = Array.Empty<string>();
        public float    castPause       = 0f;
    }

    public void Initialize(string dataJson)
    {
        if (!string.IsNullOrEmpty(dataJson))
        {
            var d = JsonUtility.FromJson<Data>(dataJson);
            if (d != null)
            {
                radius          = d.radius;
                interval        = d.interval;
                applyBehaviorId = d.applyBehaviorId;
                cleanseTypes    = d.cleanseTypes ?? Array.Empty<string>();
                castPause       = d.castPause;
            }
        }

        _timer     = interval;
        _sr        = GetComponent<SpriteRenderer>();
        _unit      = GetComponent<UnitManager>();
        _baseColor = _sr != null ? _sr.color : Color.white;

        if (interval <= 0f) Pulse();   // one-time grant on spawn
    }

    void Update()
    {
        if (interval <= 0f) return;
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = interval;
        Pulse();
    }

    void Pulse()
    {
        BehaviorDefinition applyDef = null;
        if (!string.IsNullOrEmpty(applyBehaviorId) && BehaviorLibrary.Instance != null &&
            !BehaviorLibrary.Instance.TryGet(applyBehaviorId, out applyDef))
            Debug.LogWarning($"[AllyAura] Unknown applyBehaviorId '{applyBehaviorId}'.");

        bool didSomething = false;
        var hits = Physics2D.OverlapCircleAll(transform.position, radius, LayerMask.GetMask("Enemy"));
        foreach (var col in hits)
        {
            var unit = col.GetComponent<UnitManager>();
            if (unit == null || !unit.isAlive) continue;

            if (applyDef != null)
            {
                var bh = unit.GetComponent<BehaviorHandler>() ?? unit.gameObject.AddComponent<BehaviorHandler>();
                bh.Apply(applyDef);
                didSomething = true;
            }

            if (cleanseTypes.Length > 0)
            {
                var bh = unit.GetComponent<BehaviorHandler>();
                if (bh != null)
                    foreach (var typeName in cleanseTypes)
                        if (Enum.TryParse<BehaviorType>(typeName, out var bt) && bh.HasBehaviorType(bt))
                        {
                            bh.RemoveByType(bt);
                            didSomething = true;
                        }
            }
        }

        if (didSomething && castPause > 0f) CastPulse();
    }

    // Brief stop + white flash so the pulse reads visually
    void CastPulse()
    {
        if (_unit != null) _unit.speedCurrent = 0f;
        if (_sr != null) _sr.color = Color.white;
        Invoke(nameof(EndCast), castPause);
    }

    void EndCast()
    {
        if (_sr != null) _sr.color = _baseColor;
        if (_unit != null) _unit.speedCurrent = _unit.speedMax;
    }
}
