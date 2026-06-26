using System;
using UnityEngine;

/// <summary>
/// Periodically cleanses Slowed, Rooted, and Debuff behaviors from all nearby allies.
/// Registered as "priest_aura" in ComponentRegistry.
/// </summary>
public class PriestAura : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("priest_aura", typeof(PriestAura));

    public float auraRadius = 3f;
    public float cooldown   = 3f;

    private float          _timer;
    private SpriteRenderer _sr;
    private UnitManager    _unit;
    private Color          _baseColor;
    private const float    CAST_PAUSE = 0.35f;

    [Serializable] class Data { public float auraRadius = 3f; public float cooldown = 3f; }

    public void Initialize(string dataJson)
    {
        if (!string.IsNullOrEmpty(dataJson))
        {
            var d = JsonUtility.FromJson<Data>(dataJson);
            auraRadius = d.auraRadius;
            cooldown   = d.cooldown;
        }
        _timer     = cooldown;
        _sr        = GetComponent<SpriteRenderer>();
        _unit      = GetComponent<UnitManager>();
        _baseColor = _sr != null ? _sr.color : Color.white;
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = cooldown;
        Cleanse();
    }

    void Cleanse()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, auraRadius, LayerMask.GetMask("Enemy"));
        bool cleansedAny = false;
        foreach (var col in hits)
        {
            var bh = col.GetComponent<BehaviorHandler>();
            if (bh == null) continue;
            bh.RemoveByType(BehaviorType.Slowed);
            bh.RemoveByType(BehaviorType.Rooted);
            bh.RemoveByType(BehaviorType.Debuff);
            cleansedAny = true;
        }
        if (cleansedAny) CastPulse();
    }

    void CastPulse()
    {
        // Stop movement and flash white briefly
        if (_unit != null) _unit.speedCurrent = 0f;
        if (_sr != null) _sr.color = Color.white;
        Invoke(nameof(EndCast), CAST_PAUSE);
    }

    void EndCast()
    {
        if (_sr != null) _sr.color = _baseColor;
        if (_unit != null) _unit.speedCurrent = _unit.speedMax;
    }
}
