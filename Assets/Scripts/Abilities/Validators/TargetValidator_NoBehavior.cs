using UnityEngine;

/// <summary>
/// Valid when the candidate does NOT have the specified behavior active.
/// JSON id format: "no_behavior:behaviorId"  e.g. "no_behavior:poisoned"
/// </summary>
public class TargetValidator_NoBehavior : TargetValidator
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() =>
        TargetValidatorRegistry.Register("no_behavior", param => new TargetValidator_NoBehavior(param));

    private readonly string _behaviorId;
    public TargetValidator_NoBehavior(string behaviorId) => _behaviorId = behaviorId;

    public override bool IsValid(GameObject candidate)
    {
        var handler = candidate.GetComponent<BehaviorHandler>();
        return handler == null || !handler.HasBehavior(_behaviorId);
    }
}
