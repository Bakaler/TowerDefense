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

        var handler = context.Target.GetComponent<BehaviorHandler>()
                   ?? context.Target.gameObject.AddComponent<BehaviorHandler>();

        handler.Apply(def);
    }
}
