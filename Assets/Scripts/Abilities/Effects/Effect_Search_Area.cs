using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds all alive units within a radius and executes an effect on each.
/// Configurable from the inspector (areas list) or from effects.json via ApplyData.
///
/// JSON data fields (creates one area):
///   effectId        — effect to run on each found unit
///   radius          — search radius in world units (default 4)
///   maxTargets      — cap on targets hit; -1 = unlimited (default -1)
///   horizontalArc   — arc in degrees, 360 = full circle (default 360)
///   startingDepth   — minimum distance from origin to count (default 0)
///   searchFromTarget — if true, search originates from Target position
///                      instead of Caster position (use for chain/bounce effects)
/// </summary>
public class Effect_Search_Area : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("search_area", typeof(Effect_Search_Area));

    // ── Inspector-configured areas (used when not driven from JSON) ───
    public List<Area> areas = new List<Area>();
    public SearchFilters searchFilters;

    // ── JSON-driven single area (populated by ApplyData) ─────────────
    private bool _searchFromTarget;

    public override void ApplyData(string dataJson, EffectLibrary library)
    {
        if (string.IsNullOrEmpty(dataJson)) return;

        var d = JsonUtility.FromJson<SearchAreaData>(dataJson);
        if (d == null) return;

        _searchFromTarget = d.searchFromTarget;

        var effect = library.GetEffect(d.effectId);
        if (effect == null)
        {
            Debug.LogWarning($"[Effect_Search_Area] Could not resolve effectId '{d.effectId}'.");
            return;
        }

        // Build the single area from JSON fields
        areas.Clear();
        areas.Add(new Area
        {
            radius       = d.radius,
            maxTargets   = d.maxTargets,
            horizontalArc = d.horizontalArc,
            startingDepth = d.startingDepth,
            effect       = effect,
        });
    }

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        if (areas == null || areas.Count == 0) return;

        // Origin: target position for bounce/chain, caster position otherwise
        Vector2 origin2D;
        if (_searchFromTarget && context.Target != null)
            origin2D = context.Target.transform.position;
        else if (context.AimOrigin2D.HasValue)
            origin2D = context.AimOrigin2D.Value;
        else if (context.Caster != null)
            origin2D = context.Caster.transform.position;
        else
            return;

        Vector2 forward2D = context.AimDirection2D ?? Vector2.up;

        foreach (var area in areas)
        {
            if (area?.effect == null) continue;

            Vector2 offsetOrigin = origin2D + forward2D.normalized * area.castOffset;
            float   finalRadius  = area.radius + area.radiusBonus;

            Collider2D[] hits      = Physics2D.OverlapCircleAll(offsetOrigin, finalRadius);
            var          alreadyHit = new HashSet<UnitParentClass>();

            // Exclude the current context target so the search doesn't re-hit them
            if (context.Target != null)
                alreadyHit.Add(context.Target);

            int targetsHit = 0;

            foreach (var hit in hits)
            {
                if (area.maxTargets >= 0 && targetsHit >= area.maxTargets) break;

                var candidate = hit.GetComponentInParent<UnitParentClass>();
                if (candidate == null || alreadyHit.Contains(candidate)) continue;
                if (searchFilters != null && !SearchFilterUtility.PassesFilters(context.Caster, candidate, searchFilters.Flags)) continue;

                Vector2 toTarget = (Vector2)candidate.transform.position - offsetOrigin;
                float   distance = toTarget.magnitude;

                if (distance < area.startingDepth || distance > finalRadius) continue;

                if (area.horizontalArc < 360f)
                {
                    float angle = Vector2.SignedAngle(forward2D, toTarget) - area.facingAdjustment;
                    if (Mathf.Abs(angle) > area.horizontalArc * 0.5f) continue;
                }

                alreadyHit.Add(candidate);
                var jumpCtx = context.CloneForNewTarget(candidate);
                // When searching from the hit unit, subsequent missiles should
                // spawn from that unit's position, not the original tower.
                if (_searchFromTarget && context.Target != null)
                    jumpCtx.CasterTransform = context.Target.transform;
                area.effect.Execute(jumpCtx);
                targetsHit++;
            }
        }
    }

    [Serializable]
    class SearchAreaData
    {
        public string effectId       = "";
        public float  radius         = 4f;
        public int    maxTargets     = -1;
        public float  horizontalArc  = 360f;
        public float  startingDepth  = 0f;
        public bool   searchFromTarget = false;
    }
}
