using UnityEngine;

/// <summary>
/// Unified projectile launcher — spawns one or more projectiles defined in projectiles.json.
/// Replaces Effect_Launch_Missile / Effect_Launch_Shotgun / Effect_Launch_Boomerang.
///
/// The projectile's movement mode decides how the target is used:
///   straight — fired toward the target's position, with optional spread
///   homing   — locks onto the target unit
///   arc      — flies to the target point and detonates there (mortar)
///   orbit    — sweeps around a point ahead of the caster (boomerang)
/// </summary>
[CreateAssetMenu(fileName = "NewEffect_Launch", menuName = "Effect/Launch")]
public class Effect_Launch : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("launch", typeof(Effect_Launch));

    // ── Data fields (populated from effects.json via ApplyData) ──────
    /// <summary>ID of the projectile definition in projectiles.json.</summary>
    public string projectileId = "";
    /// <summary>ID of the Effect applied on projectile impact. Resolved from EffectLibrary.</summary>
    public string impactEffectId = "";

    /// <summary>Projectiles spawned per cast.</summary>
    public int count = 1;
    /// <summary>Total cone width in degrees for straight projectiles (center-weighted random).</summary>
    public float spreadAngle = 0f;
    /// <summary>Per-shot random multipliers: value j gives a range of (1-j)..(1+j).</summary>
    public float speedJitter    = 0f;
    public float lifetimeJitter = 0f;
    public float scaleJitter    = 0f;
    /// <summary>ModifierSelection key whose value is added to count (e.g. "BonusShotgunBullets").</summary>
    public string bonusCountKey = "";

    // ── Resolved at runtime ───────────────────────────────────────────
    private Effect _impactEffect;

    public override void ApplyData(string dataJson, EffectLibrary library)
    {
        base.ApplyData(dataJson, library);

        if (!string.IsNullOrEmpty(impactEffectId))
            _impactEffect = library.GetEffect(impactEffectId);

        if (_impactEffect == null)
            Debug.LogWarning($"[Effect_Launch] '{effectID}' could not resolve impactEffectId '{impactEffectId}'.");
    }

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        if (_impactEffect == null) { Debug.LogWarning($"[Effect_Launch] '{effectID}' impactEffect is null."); return; }

        var def = ProjectileLibrary.Get(projectileId);
        if (def == null) { Debug.LogWarning($"[Effect_Launch] '{effectID}' unknown projectileId '{projectileId}'."); return; }

        bool homing = def.movement == "homing";
        if (homing && context.Target == null)
        {
            Debug.LogWarning($"[Effect_Launch] '{effectID}' homing projectile needs a target.");
            return;
        }

        // Spawn origin: explicit caster transform → caster unit → target position (chain/bounce fallback)
        Transform spawnTransform = context.CasterTransform
            ?? context.Caster?.transform
            ?? context.Target?.transform;
        if (spawnTransform == null) { Debug.LogWarning($"[Effect_Launch] '{effectID}' has no spawn origin."); return; }

        Vector2 toTarget = context.Target != null
            ? ((Vector2)(context.Target.transform.position - spawnTransform.position)).normalized
            : Vector2.up;

        int total = Mathf.Max(1, count + BonusCount());
        float half = spreadAngle * 0.5f;

        for (int i = 0; i < total; i++)
        {
            // Two samples averaged — spread weighted toward the cone center
            float offset = half > 0f
                ? (Random.Range(-half, half) + Random.Range(-half, half)) * 0.5f
                : 0f;

            ProjectileFactory.Spawn(def, new ProjectileSpawnArgs
            {
                origin             = spawnTransform.position,
                direction          = Rotate(toTarget, offset),
                targetUnit         = homing ? context.Target : null,
                targetPoint        = context.TargetPoint,
                impactEffect       = _impactEffect,
                caster             = context.Caster,
                casterTransform    = context.CasterTransform ?? context.Caster?.transform,
                originAbility      = context.OriginAbility,
                originTower        = context.OriginTower,
                speedMultiplier    = JitterMultiplier(speedJitter),
                lifetimeMultiplier = JitterMultiplier(lifetimeJitter),
                scaleMultiplier    = JitterMultiplier(scaleJitter),
            });
        }
    }

    int BonusCount() =>
        string.IsNullOrEmpty(bonusCountKey) ? 0 : (int)ModifierSelection.GetFloat(bonusCountKey);

    static float JitterMultiplier(float jitter) =>
        jitter > 0f ? Random.Range(1f - jitter, 1f + jitter) : 1f;

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        if (degrees == 0f) return v;
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}
