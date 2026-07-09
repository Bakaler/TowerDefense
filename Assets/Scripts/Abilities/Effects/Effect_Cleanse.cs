using System;
using UnityEngine;

/// <summary>
/// Removes behavior types (Slowed, Rooted, Debuff, …) from all alive units within a
/// radius of the cast origin. Reports how many units were actually cleansed via
/// context.UnitsAffected so casters can hold their cooldown when nothing needed it.
/// JSON data fields: radius, cleanseTypes (BehaviorType names)
/// </summary>
public class Effect_Cleanse : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("cleanse", typeof(Effect_Cleanse));

    public float    radius       = 3.5f;
    public string[] cleanseTypes = Array.Empty<string>();

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        if (cleanseTypes == null || cleanseTypes.Length == 0) return;

        Vector2 origin;
        if      (context.AimOrigin2D.HasValue)         origin = context.AimOrigin2D.Value;
        else if (context.Caster != null)               origin = context.Caster.transform.position;
        else if (context.CasterTransform != null)      origin = context.CasterTransform.position;
        else return;

        foreach (var col in Physics2D.OverlapCircleAll(origin, radius))
        {
            var unit = col.GetComponentInParent<UnitParentClass>();
            if (unit == null || !unit.isAlive) continue;

            var bh = unit.GetComponent<BehaviorHandler>();
            if (bh == null) continue;

            bool cleansed = false;
            foreach (var typeName in cleanseTypes)
                if (Enum.TryParse<BehaviorType>(typeName, out var bt) && bh.HasBehaviorType(bt))
                {
                    bh.RemoveByType(bt);
                    cleansed = true;
                }

            if (cleansed) context.UnitsAffected++;
        }
    }
}
