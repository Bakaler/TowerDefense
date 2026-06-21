using UnityEngine;

/// <summary>
/// Filters candidate targets during tower target selection.
/// Unlike Effect validators (checked at execution time), these run during
/// GetLeadEnemy() so the tower picks the right target before firing.
///
/// ID format in JSON: "type:param"  e.g. "no_behavior:poisoned"
/// </summary>
public abstract class TargetValidator
{
    public abstract bool IsValid(GameObject candidate);
}
