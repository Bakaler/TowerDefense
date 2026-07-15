using UnityEngine;

/// <summary>
/// Searches for nearby enemies whose definitionId matches targetDefinitionId and
/// permanently adds speedBonus to their speedMax/speedCurrent. Bypasses BehaviorHandler
/// so it cannot be cleansed.
/// </summary>
[CreateAssetMenu(fileName = "NewEffect_ApplyPermanentSpeedBuff", menuName = "Effect/Apply Permanent Speed Buff")]
public class Effect_ApplyPermanentSpeedBuff : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("apply_permanent_speed_buff", typeof(Effect_ApplyPermanentSpeedBuff));

    public float  speedBonus          = 0.3f;
    public float  radius              = 4f;
    public string targetDefinitionId  = "";   // only buff units with this id; empty = all enemies

    public override void ApplyData(string dataJson, EffectLibrary library)
    {
        if (!string.IsNullOrEmpty(dataJson))
            JsonUtility.FromJsonOverwrite(dataJson, this);
    }

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;

        Vector2 origin = context.AimOrigin2D ?? (context.CasterTransform != null ? (Vector2)context.CasterTransform.position : Vector2.zero);
        var hits = Physics2D.OverlapCircleAll(origin, radius, GameLayers.EnemyMask);

        foreach (var col in hits)
        {
            var unit = col.GetComponent<UnitManager>();
            if (unit == null || !unit.isAlive) continue;
            if (!string.IsNullOrEmpty(targetDefinitionId) && unit.definitionId != targetDefinitionId) continue;

            unit.speedMax += speedBonus;
            unit.RefreshSpeed();   // re-derive speedCurrent with active multipliers intact
        }
    }
}
