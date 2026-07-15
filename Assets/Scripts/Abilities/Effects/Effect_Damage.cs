using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEffect_Damage", menuName = "Effect/Damage")]
public class Effect_Damage : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("damage", typeof(Effect_Damage));

    public float damageBase = 0f;

    [Tooltip("Effect executed just before damage is dealt (after chance roll)")]
    public Effect preDamageEffect;

    public float minimumDamage = 0f;
    public float maximumDamage = 524287f;
    public DamageType damageType = DamageType.Physical;
    public float shieldBonus = 0f;

    [Range(0f, 1f)]
    public float criticalChance = 0f;
    public float criticalDamageMultiplier = 2f;

    [Tooltip("Ramp: damage is multiplied by 1 + (target's stacks of this behavior × bonusPerStack)")]
    public string bonusPerStackBehaviorId = "";
    public float  bonusPerStack = 0f;

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        if (Random.Range(0f, 1f) > chance) return;

        var target = context.Target;
        if (target == null) return;

        if (preDamageEffect != null)
            EffectExecutor.ExecuteEffect(preDamageEffect, context);

        TowerInfo towerInfo = context.OriginTower != null
            ? context.OriginTower.GetComponent<TowerInfo>()
            : null;
        float towerMult = towerInfo != null ? towerInfo.EffectiveDamageMult : 1f;

        if (towerInfo != null)
        {
            // LastStand: at 1 life, towers deal 2× damage
            if (ModifierSelection.HasEffect("LastStand") && LogicManager.Instance != null && LogicManager.Instance.lives <= 1f)
                towerMult *= 2f;
        }

        float rawBase = context.DamageOverride > 0f ? context.DamageOverride : damageBase;
        float damage  = rawBase * towerMult;
        DamageType type = context.DamageTypeOverride ?? damageType;

        if (criticalChance > 0f && Random.Range(0f, 1f) <= criticalChance)
            damage *= criticalDamageMultiplier;

        // Per-stack ramp (e.g. bee frenzy): each stack of the marker behavior
        // on the target adds bonusPerStack to this effect's damage
        if (bonusPerStack != 0f && !string.IsNullOrEmpty(bonusPerStackBehaviorId))
        {
            var bh = target.GetComponent<BehaviorHandler>();
            if (bh != null)
                damage *= 1f + bh.GetStackCount(bonusPerStackBehaviorId) * bonusPerStack;
        }

        float finalDamage = Mathf.Clamp(damage, minimumDamage, maximumDamage);
        bool wasAlive = target.isAlive && target.lifeCurrent > 0f;
        // Shield bonus scales with the same tower multipliers as damage
        target.TakeDamage(finalDamage, shieldBonus * towerMult, minimumDamage, maximumDamage, type);
        bool killedIt = wasAlive && (target.lifeCurrent <= 0f || !target.isAlive);
        if (killedIt)
            KillRewards.Award(target, context.OriginTower);
    }
}
