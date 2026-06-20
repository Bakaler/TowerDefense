using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEffect_Damage", menuName = "Effect/Damage")]
public class Effect_Damage : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("damage", typeof(Effect_Damage));

    public float damageBase = 0f;
    public List<Accumulators_Constant> damageAccumulators;

    [Tooltip("Effect executed just before damage is dealt (after chance roll)")]
    public Effect preDamageEffect;

    public float minimumDamage = 0f;
    public float maximumDamage = 524287f;
    public DamageType damageType = DamageType.Physical;
    public float shieldBonus = 0f;

    [Range(0f, 1f)]
    public float criticalChance = 0f;
    public float criticalDamageMultiplier = 2f;

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        if (Random.Range(0f, 1f) > chance) return;

        var target = context.Target;
        if (target == null) return;

        if (preDamageEffect != null)
            EffectExecutor.ExecuteEffect(preDamageEffect, context);

        float damage = damageBase;

        if (criticalChance > 0f && Random.Range(0f, 1f) <= criticalChance)
            damage *= criticalDamageMultiplier;

        float finalDamage = Mathf.Clamp(damage, minimumDamage, maximumDamage);
        bool wasAlive = target.isAlive && target.lifeCurrent > 0f;
        target.TakeDamage(finalDamage, shieldBonus, minimumDamage, maximumDamage, damageType);
        bool killedIt = wasAlive && (target.lifeCurrent <= 0f || !target.isAlive);
        if (killedIt && context.OriginTower != null)
            context.OriginTower.GetComponent<TowerInfo>()?.RegisterKill();
    }
}
