using UnityEngine;

/// <summary>
/// Valid when the candidate can still be affected by the specified behavior:
/// it does NOT currently have it AND is not immune to its behavior type.
/// Lets CC towers skip immune units (e.g. a boss with boss_immunity) and
/// already-affected units, falling back to them only when no better target exists.
/// JSON id format: "affectable:behaviorId"  e.g. "affectable:slowed"
/// </summary>
public class TargetValidator_Affectable : TargetValidator
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() =>
        TargetValidatorRegistry.Register("affectable", param => new TargetValidator_Affectable(param));

    private readonly string _behaviorId;
    public TargetValidator_Affectable(string behaviorId) => _behaviorId = behaviorId;

    public override bool IsValid(GameObject candidate)
    {
        var handler = candidate.GetComponent<BehaviorHandler>();
        if (handler == null) return true;   // no behaviors at all — fully affectable
        if (handler.HasBehavior(_behaviorId)) return false;

        // Immunity is declared against the behavior's type, so resolve the definition
        if (BehaviorLibrary.Instance != null &&
            BehaviorLibrary.Instance.TryGet(_behaviorId, out var def) &&
            handler.IsImmuneTo(def.behaviorType))
            return false;

        return true;
    }
}
