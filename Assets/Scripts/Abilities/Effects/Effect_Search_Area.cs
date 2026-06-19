using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEffect_Search_Area", menuName = "Effect/Search Area")]
public class Effect_Search_Area : Effect
{
    public List<Area> areas;
    public SearchFilters searchFilters;
    public int minimumCount = 0;
    public bool useUnitForward = false;

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        if (context.Caster == null) return;

        // Origin: prefer explicit aim origin, fall back to caster position
        Vector2 origin2D = context.AimOrigin2D ?? (Vector2)context.Caster.transform.position;
        Vector2 forward2D = context.AimDirection2D ?? Vector2.up;

        if (useUnitForward)
            forward2D = context.Caster.transform.up;

        foreach (var area in areas)
        {
            if (area.effect == null) continue;

            Vector2 offsetOrigin = origin2D + forward2D.normalized * area.castOffset;
            float finalRadius = area.radius + area.radiusBonus;

            Collider2D[] hits = Physics2D.OverlapCircleAll(offsetOrigin, finalRadius);
            HashSet<UnitParentClass> alreadyHit = new HashSet<UnitParentClass>();
            int targetsHit = 0;

            foreach (var hit in hits)
            {
                if (area.maxTargets >= 0 && targetsHit >= area.maxTargets) break;

                UnitParentClass candidate = hit.GetComponentInParent<UnitParentClass>();
                if (candidate == null || alreadyHit.Contains(candidate)) continue;
                if (!SearchFilterUtility.PassesFilters(context.Caster, candidate, searchFilters.Flags)) continue;

                Vector2 toTarget = (Vector2)candidate.transform.position - offsetOrigin;
                float distance = toTarget.magnitude;

                if (distance < area.startingDepth || distance > finalRadius) continue;

                // Arc check
                if (area.horizontalArc < 360f)
                {
                    float angle = Vector2.SignedAngle(forward2D, toTarget);
                    // Apply facing adjustment
                    angle -= area.facingAdjustment;
                    if (Mathf.Abs(angle) > area.horizontalArc * 0.5f) continue;
                }

                alreadyHit.Add(candidate);
                var clonedContext = context.CloneForNewTarget(candidate);
                area.effect.Execute(clonedContext);
                targetsHit++;
            }
        }
    }
}
