using System;
using UnityEngine;

/// <summary>
/// Defines a tower type as plain serializable data.
/// All definitions live in Resources/Definitions/towers.json.
/// TowerFactory builds a complete GameObject from this — no prefab required.
/// </summary>
[Serializable]
public class TowerDefinition
{
    // ── Identity ──────────────────────────────────────────────────────
    public string id;
    public string displayName;
    public string description;

    // ── Stats ─────────────────────────────────────────────────────────
    public float range        = 5f;
    public int   resourceCost = 100;

    // ── Balance ───────────────────────────────────────────────────────
    public string balanceType = "Physical";  // "Elemental" | "Arcane" | "Physical"

    // ── Ability ───────────────────────────────────────────────────────
    public string fireAbilityId;

    // ── Rotation / Arc ────────────────────────────────────────────────
    /// <summary>Degrees per second the tower rotates toward its target. 0 = no rotation.</summary>
    public float rotationSpeed = 0f;

    // ── Art ───────────────────────────────────────────────────────────
    /// <summary>Resources path to a single Sprite (no extension). Leave empty if using a sheet.</summary>
    public string spritePath;

    /// <summary>Resources path to a Multiple-mode sprite sheet (no extension).</summary>
    public string spriteSheet;

    /// <summary>Zero-based index into the sprite sheet.</summary>
    public int spriteIndex = -1;

    /// <summary>Uniform world-space scale. Default 1.</summary>
    public float scale = 1f;

    /// <summary>Fallback color shown when no sprite is assigned. Useful during dev.</summary>
    public Color debugColor = Color.cyan;

    /// <summary>Optional tint applied to the sprite. Default white = no tint.</summary>
    public Color tintColor = Color.white;

    // ── Upgrades ──────────────────────────────────────────────────────
    /// <summary>Number of tiers (1 = base only, 3 = base + 2 upgrades).</summary>
    public int maxTier = 1;
    /// <summary>Per-tier stat multiplier (damage × this, cooldown ÷ this).</summary>
    public float upgradeStatMultiplier = 2.25f;
    /// <summary>Balance tier of the tower (1=T1, 2=T2, 3=T3) — drives research requirements.</summary>
    public int towerTier = 1;

    // ── Components ────────────────────────────────────────────────────
    /// <summary>
    /// Optional extra components added and initialized by the factory.
    /// Keys must match entries in ComponentRegistry.
    /// </summary>
    public ComponentEntry[] components;
}

[Serializable]
public class TowerDefinitionCollection
{
    public TowerDefinition[] towers;
}
