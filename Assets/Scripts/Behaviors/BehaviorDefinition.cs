using UnityEngine;

[System.Serializable]
public class BehaviorDefinition
{
    public string id            = "";
    public string displayName   = "";
    public float  duration      = 2f;
    /// <summary>refresh | stack | none</summary>
    public string stackRule     = "refresh";

    // Stat modifiers
    public float moveSpeedMultiplier = 1f;

    // DoT tick — tickInterval 0 means no ticks
    public float tickInterval  = 0f;
    public float tickDamage    = 0f;
    /// <summary>DamageType enum value: 0=Elemental 1=Arcane 2=Physical 3=Piercing 4=Poison 5=Pure</summary>
    public int   tickDamageType = 4;   // Poison

    public Color tintColor = Color.white;
}
