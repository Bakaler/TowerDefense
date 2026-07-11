using UnityEngine;

/// <summary>
/// Resolves display damage / cooldown / shield bonus for a TowerDefinition:
/// explicit display overrides first, otherwise walked out of the tower's
/// fire-ability effect tree. Shared by TowerFactory (runtime TowerInfo) and
/// the Journal (definition-only cards) so the two can't drift apart.
/// Requires AbilityLibrary + EffectLibrary in the scene; returns 0 without them.
/// </summary>
public static class TowerStatResolver
{
    public static float Damage(TowerDefinition def) =>
        def.displayDamage > 0f ? def.displayDamage : (FindDamageEffectFor(def)?.damageBase ?? 0f);

    public static float Cooldown(TowerDefinition def) =>
        def.displayCooldown > 0f ? def.displayCooldown : ResolveCooldown(def.fireAbilityId);

    public static float ShieldBonus(TowerDefinition def) => FindDamageEffectFor(def)?.shieldBonus ?? 0f;

    /// <summary>Damage type of the tower's resolved damage effect; null when the tower deals no direct damage.</summary>
    public static DamageType? DamageTypeFor(TowerDefinition def) => FindDamageEffectFor(def)?.damageType;

    /// <summary>
    /// Damage effect for a tower: fire-ability tree first, otherwise any effectId
    /// declared in component data (e.g. drone_swarm) — so component-driven towers
    /// resolve their stats from effects.json like everything else.
    /// </summary>
    static Effect_Damage FindDamageEffectFor(TowerDefinition def)
    {
        var fromAbility = ResolveDamageEffect(def.fireAbilityId);
        if (fromAbility != null) return fromAbility;

        if (def.components == null || EffectLibrary.Instance == null) return null;
        foreach (var c in def.components)
        {
            if (c == null || string.IsNullOrEmpty(c.data)) continue;
            var probe = JsonUtility.FromJson<ComponentEffectProbe>(c.data);
            if (probe == null || string.IsNullOrEmpty(probe.effectId)) continue;
            var found = FindFirstDamageEffect(probe.effectId, 0);
            if (found != null) return found;
        }
        return null;
    }

    [System.Serializable]
    class ComponentEffectProbe { public string effectId = ""; }

    // ── Ability/effect-tree resolution ────────────────────────────────

    public static float ResolveCooldown(string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId)) return 0f;
        if (AbilityLibrary.Instance == null) return 0f;
        var ab = AbilityLibrary.Instance.GetAbility(abilityId);
        return ab != null ? ab.cost.cooldownDuration : 0f;
    }

    public static float ResolveDamage(string abilityId) =>
        ResolveDamageEffect(abilityId)?.damageBase ?? 0f;

    public static float ResolveShieldBonus(string abilityId) =>
        ResolveDamageEffect(abilityId)?.shieldBonus ?? 0f;

    static Effect_Damage ResolveDamageEffect(string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId)) return null;
        if (AbilityLibrary.Instance == null || EffectLibrary.Instance == null) return null;
        var ab = AbilityLibrary.Instance.GetAbility(abilityId);
        if (ab == null) return null;
        return FindFirstDamageEffect(ab.effectId, 0);
    }

    // Walk the effect tree up to 4 levels deep looking for the first damage effect
    static Effect_Damage FindFirstDamageEffect(string effectId, int depth)
    {
        if (depth > 4 || string.IsNullOrEmpty(effectId)) return null;
        var effect = EffectLibrary.Instance?.GetEffect(effectId);
        return effect != null ? FindFirstDamageEffect(effect, depth) : null;
    }

    static Effect_Damage FindFirstDamageEffect(Effect effect, int depth)
    {
        if (depth > 4 || effect == null) return null;
        if (effect is Effect_Damage dmg) return dmg.damageBase > 0f ? dmg : null;
        if (effect is Effect_Launch launch) return FindFirstDamageEffect(launch.impactEffectId, depth + 1);
        if (effect is Effect_Search_Area area && area.areas.Count > 0 && area.areas[0].effect != null)
            return FindFirstDamageEffect(area.areas[0].effect, depth + 1);
        if (effect is Effect_Set set)
            foreach (var id in set.EffectIds)
            {
                var found = FindFirstDamageEffect(id, depth + 1);
                if (found != null) return found;
            }
        return null;
    }
}
