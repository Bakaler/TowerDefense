using System;
using UnityEngine;

/// <summary>
/// Defines an enemy unit type as plain serializable data.
/// All definitions live in Resources/Definitions/units.json.
/// UnitFactory builds a complete GameObject from this — no prefab required.
/// </summary>
[Serializable]
public class UnitDefinition
{
    // ── Identity ──────────────────────────────────────────────────────
    public string id;
    public string displayName;
    public string description;

    // ── Stats ─────────────────────────────────────────────────────────
    public float life              = 100f;
    /// <summary>Secondary HP pool drained before life. 0 = no shield. Damage effects'
    /// shieldBonus adds/subtracts damage while this pool is up.</summary>
    public float shield            = 0f;
    public float speed             = 3f;
    public int   physicalDefense   = 0;
    public int   elementalDefense  = 0;
    public int   arcanaDefense     = 0;
    public int   deathBlow         = 1;

    // ── Movement ──────────────────────────────────────────────────────
    /// <summary>
    /// Rotate the unit toward its movement direction (missile-style). Off by
    /// default — left/right sprite flipping happens regardless of this flag.
    /// </summary>
    public bool rotateToMovement = false;

    // ── Physics ───────────────────────────────────────────────────────
    /// <summary>Radius of the CircleCollider2D on the unit. Default 0.3.</summary>
    public float colliderRadius  = 0.3f;

    /// <summary>
    /// Unity layer index for this unit. Leave 0 to auto-resolve the "Enemy" named layer.
    /// Enemies are detected by turrets via layer checks (default layer 10).
    /// </summary>
    public int layer = 0;

    // ── Art ───────────────────────────────────────────────────────────
    /// <summary>
    /// Resources path to a single Sprite asset (no extension).
    /// Leave empty if using a sprite sheet instead.
    /// </summary>
    public string spritePath;

    /// <summary>
    /// Resources path to a Multiple-mode sprite sheet (no extension).
    /// E.g. "Art/Enemies/Enemies"
    /// Pair with spriteIndex to pick a specific cell.
    /// </summary>
    public string spriteSheet;

    /// <summary>Zero-based index into the sprite sheet (left-to-right, top-to-bottom).</summary>
    public int spriteIndex = -1;

    /// <summary>Uniform scale applied to the unit's GameObject. Default 1.</summary>
    public float scale = 1f;

    /// <summary>Fallback color shown when no sprite is assigned. Useful during dev.</summary>
    public Color debugColor = Color.red;

    /// <summary>Optional tint applied to the sprite (white = no tint).</summary>
    public Color tintColor = Color.white;

    // ── Animation ─────────────────────────────────────────────────────
    /// <summary>Resources path to the sliced sprite sheet for walk animation (no extension).</summary>
    public string animSheet;
    /// <summary>Frames per second for the walk animation. Default 8.</summary>
    public float  animFps = 8f;
    /// <summary>Resources path to the sliced sprite sheet for death animation (no extension).</summary>
    public string animDeathSheet;
    /// <summary>Frames per second for the death animation. Default 8.</summary>
    public float  animDeathFps = 8f;
    /// <summary>Play the sheets in reverse frame order (for art authored backwards).</summary>
    public bool   animReverse = false;
    /// <summary>Extra degrees added when rotating toward movement (rotateToMovement).
    /// Use 180 when the art was authored facing the opposite direction.</summary>
    public float  spriteAngleOffset = 0f;

    // ── Audio ─────────────────────────────────────────────────────────
    /// <summary>Sound played on death (sounds.json id). Empty = the "enemy_death" event sound.</summary>
    public string deathSoundId = "";

    // ── Components ────────────────────────────────────────────────────
    /// <summary>
    /// Optional extra components added and initialized by the factory.
    /// Keys must match entries in ComponentRegistry.
    /// </summary>
    public ComponentEntry[] components;

    /// <summary>
    /// Behavior ids applied permanently at spawn (e.g. immunities, passive auras).
    /// These are never timed out — they last the unit's lifetime.
    /// </summary>
    public string[] startingBehaviors = System.Array.Empty<string>();

    /// <summary>
    /// Ability ids (abilities.json) this unit casts on cooldown — support powers like
    /// cleansing allies, weaving barriers, or zapping projectiles. Preferred over
    /// bespoke components: the whole cast is data (ability → effect → behavior).
    /// </summary>
    public string[] abilities = System.Array.Empty<string>();

    /// <summary>
    /// Free-form tags tower targeting modes key off — "high_prio" (shielders,
    /// priests, barrier weavers) and "boss" are the ones currently read.
    /// </summary>
    public string[] tags = System.Array.Empty<string>();
}

[Serializable]
public class UnitDefinitionCollection
{
    public UnitDefinition[] units;
}
