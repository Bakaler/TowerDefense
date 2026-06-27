using UnityEngine;

public enum BehaviorType
{
    None     = 0,
    Slowed   = 1,
    Rooted   = 2,
    Stunned  = 3,
    Silenced = 4,
    Debuff   = 5,   // generic catch-all for DoTs, etc.
}

[System.Serializable]
public class BehaviorDefinition
{
    public string       id            = "";
    public string       displayName   = "";
    public float        duration      = 2f;
    public BehaviorType behaviorType  = BehaviorType.None;
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

    /// <summary>
    /// BehaviorType names this behavior blocks from being applied (e.g. ["Slowed","Rooted"]).
    /// Used to give units immunity to specific CC types via a permanent behavior.
    /// </summary>
    public string[] immunities = System.Array.Empty<string>();

    /// <summary>
    /// Effect id to execute when the unit carrying this behavior dies.
    /// The effect fires with the dying unit as the caster origin.
    /// </summary>
    public string onDeathEffectId = "";

    // ── VFX ───────────────────────────────────────────────────────────
    // Impact animation: plays once at the hit location when behavior is applied.
    public string impactSheetPath  = "";
    public int    impactFrameCount = 0;
    public float  impactFps        = 12f;
    public float  impactScale      = 1f;

    // Duration animation: loops on the unit for the full behavior duration.
    public string durationSheetPath  = "";
    public int    durationFrameCount = 0;
    public float  durationFps        = 8f;
    public float  durationScale      = 1f;
}
