using UnityEngine;

public abstract class BaseValidator : ScriptableObject
{
    public string validatorName = "New Validator";

    public abstract bool Validate(EffectContext context);
}
