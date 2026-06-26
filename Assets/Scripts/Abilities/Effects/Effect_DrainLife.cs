using UnityEngine;

/// <summary>
/// Deals minor damage to a target and deposits a small amount of gold/tech.
/// Used by the Siphon Tower. Registered as "drain_life".
/// </summary>
[CreateAssetMenu(fileName = "NewEffect_DrainLife", menuName = "Effect/Drain Life")]
public class Effect_DrainLife : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("drain_life", typeof(Effect_DrainLife));

    public float damage      = 1f;
    public int   goldPerDrain = 1;
    public int   techPerDrain = 0;

    public override void ApplyData(string dataJson, EffectLibrary library)
    {
        if (!string.IsNullOrEmpty(dataJson))
            JsonUtility.FromJsonOverwrite(dataJson, this);
    }

    public override void Execute(EffectContext context)   
    {
        if (!PassesValidators(context)) return;
        if (context.Target == null || !context.Target.isAlive) return;

        context.Target.TakeDamage(damage, 0f, 0f, damage * 10f, DamageType.Pure);

        if (goldPerDrain > 0)
        {
            var rm = Object.FindFirstObjectByType<ResourceManagerScript>();
            rm?.ChangeResourceOne(goldPerDrain);
        }
        if (techPerDrain > 0)
            TechManager.Instance?.AddTech(techPerDrain);
    }
}
