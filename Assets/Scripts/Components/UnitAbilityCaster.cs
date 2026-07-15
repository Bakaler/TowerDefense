using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Casts data-driven abilities (abilities.json) on cooldown for an enemy unit — the
/// unit-side counterpart of the towers' AbilityManager. Added by UnitFactory when a
/// UnitDefinition lists ability ids; replaces bespoke unit components like ally_aura.
///
/// A cast only consumes its cooldown when its effect reports it affected something
/// (EffectContext.UnitsAffected) — otherwise the ability stays charged and retries
/// shortly, so e.g. a projectile zapper fires the moment something enters range.
/// An effective cast with finish_time > 0 briefly stops the unit and flashes it
/// white so the cast reads visually (the old ally_aura castPause).
/// </summary>
public class UnitAbilityCaster : MonoBehaviour
{
    const float RetryInterval = 0.25f;

    readonly List<AbilityInstance> _instances = new();
    float          _retryTimer;
    bool           _casting;
    UnitManager    _unit;
    SpriteRenderer _sr;
    Color          _baseColor;

    public void Setup(IEnumerable<string> abilityIds)
    {
        _unit      = GetComponent<UnitManager>();
        _sr        = GetComponent<SpriteRenderer>();
        _baseColor = _sr != null ? _sr.color : Color.white;

        foreach (var id in abilityIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (AbilityLibrary.Instance != null && AbilityLibrary.Instance.TryGet(id, out var ability))
                _instances.Add(new AbilityInstance(ability));
            else
                Debug.LogWarning($"[UnitAbilityCaster] Unknown ability id '{id}' on '{name}'.");
        }
    }

    void Update()
    {
        if (_unit != null && !_unit.isAlive) return;

        float dt = Time.deltaTime;
        foreach (var inst in _instances) inst.Tick(dt);

        if (_casting) return;
        _retryTimer -= dt;
        if (_retryTimer > 0f) return;
        _retryTimer = RetryInterval;

        foreach (var inst in _instances)
            if (inst.IsReady) TryCast(inst);
    }

    void TryCast(AbilityInstance inst)
    {
        var ability = inst.Definition;
        if (ability.effect == null) { inst.Trigger(); return; }   // never spin on a broken ability

        var ctx = new EffectContext
        {
            Caster          = _unit,
            CasterTransform = transform,
            AimOrigin2D     = (Vector2)transform.position,
            OriginAbility   = ability,
            CustomData      = new Dictionary<string, object>(),
        };
        EffectExecutor.ExecuteEffect(ability.effect, ctx);

        if (ctx.UnitsAffected <= 0) return;   // whiff — stay charged, retry shortly

        inst.Trigger();
        if (!string.IsNullOrEmpty(ability.fireSoundId))
            AudioManager.Play(ability.fireSoundId);
        if (ability.finish_time > 0f)
            StartCoroutine(CastPause(ability.finish_time));
    }

    // Brief stop + white flash so the cast reads visually
    IEnumerator CastPause(float duration)
    {
        _casting = true;
        if (_unit != null) _unit.SetExternalSpeedMult(0f);
        if (_sr != null) _sr.color = Color.white;

        yield return new WaitForSeconds(duration);

        _casting = false;
        if (_sr != null) _sr.color = _baseColor;
        // Behavior slows persist through the pause — only the external freeze lifts
        if (_unit != null) _unit.SetExternalSpeedMult(1f);
    }
}
