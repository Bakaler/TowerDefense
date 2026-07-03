using UnityEngine;

[CreateAssetMenu(fileName = "NewAbility_Effect", menuName = "Ability/Ability_Effect")]
public class Ability_Effect : Ability
{
    // ── Data fields (set from abilities.json via AbilityLibrary) ─────

    /// <summary>ID of the Effect to execute. Resolved from EffectLibrary at load time.</summary>
    public string effectId = "";

    /// <summary>Detection radius in world units. Turrent sizes its trigger collider to this.</summary>
    public float range = 5f;

    /// <summary>Total fire cone in degrees. 360 = omnidirectional (no arc restriction).</summary>
    public float fireArc = 360f;

    /// <summary>Sound played when the ability triggers (sounds.json id).</summary>
    public string fireSoundId = "";

    [Tooltip("Time before cast officially starts (wind-up)")]
    public float prepare_time    = 0f;
    [Tooltip("Time at which the ability fires")]
    public float cast_start_time = 0f;
    [Tooltip("Time when cast ends")]
    public float cast_finish_time = 0f;
    [Tooltip("Total duration until fully recovered")]
    public float finish_time     = 0f;

    [Header("Resource & Cooldown")]
    public AbilityCost cost;

    // ── Attack animation (played on the tower's own SpriteRenderer) ──
    public string attackSheetPath  = "";
    public int    attackFrameCount = 0;
    public float  attackFps        = 12f;
    public float  attackScale      = 1f;

    // ── Resolved at runtime (set by AbilityLibrary) ──────────────────

    /// <summary>The actual Effect instance, resolved from effectId by AbilityLibrary.</summary>
    [System.NonSerialized]
    public Effect effect;

    /// <summary>
    /// Optional target-selection filters. The tower prefers targets that pass all validators,
    /// falling back to normal lead-enemy selection if no valid target exists.
    /// </summary>
    [System.NonSerialized]
    public TargetValidator[] targetValidators;
}
