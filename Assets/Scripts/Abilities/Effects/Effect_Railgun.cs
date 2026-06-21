using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fires an instant piercing beam from the tower through all enemies in a line.
/// Damages every enemy hit. Spawns a RailgunBeam visual that fades out.
/// </summary>
[CreateAssetMenu(fileName = "NewEffect_Railgun", menuName = "Effect/Railgun")]
public class Effect_Railgun : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("railgun", typeof(Effect_Railgun));

    public float damageBase      = 80f;
    public float beamRange       = 20f;
    public float beamWidth       = 0.15f;   // capsule half-width for overlap query
    public float beamFadeDuration = 0.35f;
    public Color beamColor       = new Color(0.4f, 0.9f, 1f, 1f);
    public DamageType damageType = DamageType.Physical;

    public override void ApplyData(string dataJson, EffectLibrary library)
    {
        if (!string.IsNullOrEmpty(dataJson))
            JsonUtility.FromJsonOverwrite(dataJson, this);
    }

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;

        Transform origin = context.CasterTransform ?? context.Caster?.transform;
        if (origin == null) return;

        Vector2 start = origin.position;
        // Aim toward the primary target; fall back to tower's up direction
        Vector2 dir = context.Target != null
            ? ((Vector2)context.Target.transform.position - start).normalized
            : (Vector2)origin.up;

        Vector2 end = start + dir * beamRange;

        // Capsule cast along the beam to find all enemies
        var hits = Physics2D.CapsuleCastAll(
            start, new Vector2(beamWidth * 2f, beamWidth * 2f),
            CapsuleDirection2D.Horizontal, Vector2.SignedAngle(Vector2.right, dir),
            dir, beamRange);

        TowerInfo towerInfo = context.OriginTower != null
            ? context.OriginTower.GetComponent<TowerInfo>()
            : null;
        float towerMult = towerInfo != null ? towerInfo.StatMultiplier * towerInfo.ExtraMultiplier : 1f;
        float damage    = damageBase * towerMult;

        var alreadyHit = new HashSet<UnitParentClass>();
        foreach (var hit in hits)
        {
            var unit = hit.collider.GetComponent<UnitParentClass>();
            if (unit == null || !unit.isAlive || alreadyHit.Contains(unit)) continue;
            alreadyHit.Add(unit);

            bool wasAlive = unit.lifeCurrent > 0f;
            unit.TakeDamage(damage, 0f, 0f, damage * 10f, damageType);
            bool killed = wasAlive && (unit.lifeCurrent <= 0f || !unit.isAlive);
            if (killed)
            {
                towerInfo?.RegisterKill();
                TrySpawnBounty(unit.transform.position);
            }
        }

        // Spawn fade visual — find actual end point at last hit or full range
        SpawnBeam(start, end);
    }

    static void TrySpawnBounty(Vector3 pos)
    {
        float physical = BalanceManager.Instance != null ? BalanceManager.Instance.Physical : 0f;
        float chance   = 0.15f + physical * 0.0025f;
        if (Random.value <= chance)
            BountyDrop.Spawn(pos, 1);
    }

    void SpawnBeam(Vector2 start, Vector2 end)
    {
        var go   = new GameObject("[RailgunBeam]");
        var beam = go.AddComponent<RailgunBeam>();
        beam.Setup(start, end, beamColor, beamWidth, beamFadeDuration);
    }
}
