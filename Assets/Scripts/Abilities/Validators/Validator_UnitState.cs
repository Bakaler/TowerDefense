using UnityEngine;

[CreateAssetMenu(fileName = "NewValidator_UnitState", menuName = "Effect/Validators/UnitState")]
public class Validator_UnitState : BaseValidator
{
    [Tooltip("Require target to be alive (Required), dead (Excluded), or either (Allowed)")]
    public FilterRule aliveRule = FilterRule.Required;

    public override bool Validate(EffectContext context)
    {
        if (context.Target == null) return false;

        bool isAlive = context.Target.isAlive;

        switch (aliveRule)
        {
            case FilterRule.Required:
                if (!isAlive) return false;
                break;
            case FilterRule.Excluded:
                if (isAlive) return false;
                break;
        }
        return true;
    }
}
