using System.Collections.Generic;
using UnityEngine;

public abstract class Effect : ScriptableObject
{
    public string effectName = "Unknown Name";
    public string effectID = "UnknownName";

    public string editorPrefix = "";
    public string editorSuffix = "";

    [Range(0f, 1f)]
    public float chance = 1f;
    public bool canBeBlocked = false;

    [Header("Validation")]
    public List<BaseValidator> validators = new List<BaseValidator>();

    public abstract void Execute(EffectContext context);

    /// <summary>
    /// Called by EffectLibrary after instantiation.
    /// Override in each subclass to read type-specific fields from the JSON data blob.
    /// Base implementation uses JsonUtility.FromJsonOverwrite for simple flat fields.
    /// </summary>
    public virtual void ApplyData(string dataJson, EffectLibrary library)
    {
        if (!string.IsNullOrEmpty(dataJson))
            JsonUtility.FromJsonOverwrite(dataJson, this);
    }

    protected bool PassesValidators(EffectContext context)
    {
        foreach (var validator in validators)
        {
            if (validator == null) continue;
            if (!validator.Validate(context))
                return false;
        }
        return true;
    }
}
