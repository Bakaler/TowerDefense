using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zaps the nearest live tower projectile(s) within a radius of the cast origin out of
/// the air, with a lightning-bolt visual. Reports destroyed projectiles via
/// context.UnitsAffected so casters can hold their cooldown while nothing is in range.
/// JSON data fields: radius, count
/// </summary>
public class Effect_DestroyProjectile : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("destroy_projectile", typeof(Effect_DestroyProjectile));

    public float radius = 4f;
    /// <summary>Projectiles destroyed per cast.</summary>
    public int   count  = 1;

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;

        Vector2 origin;
        if      (context.AimOrigin2D.HasValue)         origin = context.AimOrigin2D.Value;
        else if (context.Caster != null)               origin = context.Caster.transform.position;
        else if (context.CasterTransform != null)      origin = context.CasterTransform.position;
        else return;

        // Destroy() is deferred, so track what this cast already claimed
        var claimed = new HashSet<Projectile>();

        for (int n = 0; n < Mathf.Max(1, count); n++)
        {
            var target = FindNearest(origin, claimed);
            if (target == null) break;

            claimed.Add(target);
            var go  = new GameObject("[ZapLine]");
            var vis = go.AddComponent<ChainLightningVisual>();
            vis.SetPath(new List<Vector3> { origin, target.transform.position });
            Destroy(target.gameObject);
            context.UnitsAffected++;
        }
    }

    Projectile FindNearest(Vector2 origin, HashSet<Projectile> claimed)
    {
        Projectile best    = null;
        float      bestSqr = radius * radius;
        foreach (var p in Projectile.Active)
        {
            if (p == null || claimed.Contains(p)) continue;
            float sqr = ((Vector2)p.transform.position - origin).sqrMagnitude;
            if (sqr <= bestSqr) { bestSqr = sqr; best = p; }
        }
        return best;
    }
}
