using UnityEngine;

/// <summary>
/// Applies a behavior (slow, stun, burn, etc.) from behaviors.json to the target unit.
/// Lazily adds BehaviorHandler if the target doesn't already have one.
/// </summary>
public class Effect_ApplyBehavior : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("apply_behavior", typeof(Effect_ApplyBehavior));

    public string behaviorId = "";

    public override void Execute(EffectContext context) 
    {
        if (!PassesValidators(context)) return;
        if (context.Target == null) return;

        if (BehaviorLibrary.Instance == null || !BehaviorLibrary.Instance.TryGet(behaviorId, out var def))
        {
            Debug.LogWarning($"[Effect_ApplyBehavior] Unknown behaviorId '{behaviorId}'.");
            return;
        }

        SpawnImpactVFX(def, context.Target.transform.position, context.CasterTransform);

        var handler = context.Target.GetComponent<BehaviorHandler>()
                   ?? context.Target.gameObject.AddComponent<BehaviorHandler>();

        // Origin tower rides along so DoT tick kills credit the right tower
        handler.Apply(def, context.OriginTower);
    }

    static void SpawnImpactVFX(BehaviorDefinition def, Vector3 position, Transform caster)
    {
        if (string.IsNullOrEmpty(def.impactSheetPath) || def.impactFrameCount <= 0) return;

        var go              = new GameObject("[BehaviorImpact]");
        go.transform.position = position;

        if (caster != null)
        {
            Vector2 dir   = (Vector2)(position - caster.position);
            float   angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Units";
        sr.sortingOrder     = 30;

        var anim               = go.AddComponent<SpriteSheetAnimator>();
        anim.texturePath       = def.impactSheetPath;
        anim.frameCount        = def.impactFrameCount;
        anim.fps               = def.impactFps;
        anim.scale             = def.impactScale;
        anim.loop              = false;
        anim.destroyOnComplete = true;
    }
}
