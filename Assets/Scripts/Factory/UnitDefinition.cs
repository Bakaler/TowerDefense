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
    public float life            = 100f;
    public float speed           = 3f;
    public int   physicalDefense = 0;
    public int   bounty          = 10;
    public int   deathBlow       = 1;

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

    // ── Components ────────────────────────────────────────────────────
    /// <summary>
    /// Optional extra components added and initialized by the factory.
    /// Keys must match entries in ComponentRegistry.
    /// </summary>
    public ComponentEntry[] components;
}

[Serializable]
public class UnitDefinitionCollection
{
    public UnitDefinition[] units;
}
