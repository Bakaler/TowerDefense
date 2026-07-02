using UnityEngine;

/// <summary>Per-minion overrides passed to MinionFactory.Spawn. 0/null fields fall back to the definition.</summary>
public struct MinionSpawnArgs
{
    /// <summary>Engage search radius — usually the owning tower's range. Required (>0).</summary>
    public float range;
    /// <summary>Replaces def.attackCooldown when >0. Hosts stagger this per minion.</summary>
    public float cooldownOverride;
    /// <summary>Replaces def.maxAwayTime when >0. Hosts stagger this so minions don't all leave at once.</summary>
    public float maxAwayTimeOverride;
    /// <summary>Replaces def.restDuration when >0.</summary>
    public float restDurationOverride;
    /// <summary>Replaces the effect resolved from def.impactEffectId when set.</summary>
    public Effect impactEffect;
    /// <summary>Animation phase offset so swarms don't flap in sync.</summary>
    public float animTimeOffset;
}

/// <summary>
/// Builds minion GameObjects entirely in code from MinionDefinitions (minions.json) —
/// no prefabs. Mirrors the Tower/Unit/Projectile factory pattern.
/// </summary>
public static class MinionFactory
{
    public static Minion Spawn(string minionId, IMinionHost host, Vector3 position, MinionSpawnArgs args)
    {
        var def = MinionLibrary.Get(minionId);
        if (def == null)
        {
            Debug.LogWarning($"[MinionFactory] No minion definition for id '{minionId}'.");
            return null;
        }
        return Spawn(def, host, position, args);
    }

    public static Minion Spawn(MinionDefinition def, IMinionHost host, Vector3 position, MinionSpawnArgs args)
    {
        if (def == null) return null;

        var go = new GameObject(string.IsNullOrEmpty(def.displayName) ? (def.id ?? "Minion") : def.displayName);
        go.transform.position   = position;
        go.transform.localScale = Vector3.one * (def.scale > 0f ? def.scale : 1f);

        // ── Visuals ───────────────────────────────────────────────
        var sr              = go.AddComponent<SpriteRenderer>();
        var frames          = RuntimeSprites.LoadSheet(def.spritePath);
        sr.sprite           = frames.Length > 0 ? frames[0] : RuntimeSprites.Circle(10);
        sr.color            = def.tintColor.a > 0f ? def.tintColor : Color.white;
        sr.sortingLayerName = string.IsNullOrEmpty(def.sortingLayer) ? "Towers" : def.sortingLayer;
        sr.sortingOrder     = def.sortingOrder;

        if (frames.Length > 1)
        {
            var anim = go.AddComponent<SpriteAnimator>();
            anim.Setup(frames, def.animFps > 0f ? def.animFps : 12f);
            anim.OffsetTime(args.animTimeOffset);
        }

        // ── Brain ─────────────────────────────────────────────────
        var minion            = go.AddComponent<Minion>();
        minion.def            = def;
        minion.host           = host;
        minion.range          = args.range > 0f ? args.range : def.wanderRadius;
        minion.noticeRange    = minion.range;
        minion.attackCooldown = args.cooldownOverride     > 0f ? args.cooldownOverride     : def.attackCooldown;
        minion.maxAwayTime    = args.maxAwayTimeOverride  > 0f ? args.maxAwayTimeOverride  : def.maxAwayTime;
        minion.restDuration   = args.restDurationOverride > 0f ? args.restDurationOverride : def.restDuration;
        minion.impactEffect   = args.impactEffect != null ? args.impactEffect : ResolveImpactEffect(def);

        // Parent to the host so minions follow it; world scale is preserved.
        if (host?.HomeTransform != null)
            go.transform.SetParent(host.HomeTransform, true);

        return minion;
    }

    static Effect ResolveImpactEffect(MinionDefinition def)
    {
        if (string.IsNullOrEmpty(def.impactEffectId) || EffectLibrary.Instance == null) return null;
        return EffectLibrary.Instance.GetEffect(def.impactEffectId);
    }
}
