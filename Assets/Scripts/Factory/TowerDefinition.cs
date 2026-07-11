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
    public float range           = 5f;
    public float placementRadius = 0.4f;   // body footprint used for overlap/spacing check
    public int   resourceCost    = 100;

    // ── Placement ─────────────────────────────────────────────────────
    /// <summary>"" = normal single-click placement. "pair" = two clicks (post A, then post B).</summary>
    public string placementMode = "";
    /// <summary>Max world-space distance between the two posts when placementMode is "pair".</summary>
    public float pairMaxSpan = 4f;

    // ── Detection ─────────────────────────────────────────────────────
    /// <summary>Tower tier at which this tower can target invisible units.
    /// 0 = never, 1 = from placement, 2 = once upgraded to tier 2, etc.</summary>
    public int detectorTier = 0;

    // ── Balance ───────────────────────────────────────────────────────
    public string balanceType   = "Physical";  // "Elemental" | "Arcane" | "Physical"
    /// <summary>Tower cost in the per-level balance budget. T1=1, T2=2, T3=4.</summary>
    public int    balanceWeight = 1;

    // ── Targeting ─────────────────────────────────────────────────────
    /// <summary>TargetingMode name the tower starts with (e.g. "Closest").
    /// Empty = Furthest. The player can still change it per-tower in the HUD.</summary>
    public string defaultTargeting = "";

    // ── Ability ───────────────────────────────────────────────────────
    public string fireAbilityId;

    // ── Audio ─────────────────────────────────────────────────────────
    /// <summary>Placement sound (sounds.json id). Empty = the generic "tower_place" event sound.</summary>
    public string placeSoundId = "";
    /// <summary>Sell sound. Empty = the generic "tower_sell" event sound.</summary>
    public string sellSoundId = "";
    /// <summary>Upgrade sound. Empty = the generic "tower_upgrade" event sound.</summary>
    public string upgradeSoundId = "";

    // ── HUD display overrides (for component-driven towers with no fireAbilityId) ──
    /// <summary>If > 0 overrides the auto-resolved damage shown in the tower panel.</summary>
    public float displayDamage   = 0f;
    /// <summary>If > 0 overrides the auto-resolved cooldown (seconds) shown in the tower panel.</summary>
    public float displayCooldown = 0f;

    // ── Rotation / Arc ────────────────────────────────────────────────
    /// <summary>Degrees per second the tower rotates toward its target. 0 = no rotation.</summary>
    public float rotationSpeed = 0f;

    /// <summary>If > 0, the base sprite sheet is animated at this FPS instead of shown as a static image.</summary>
    public float animFps = 0f;

    // ── Art ───────────────────────────────────────────────────────────
    /// <summary>Resources path to a single Sprite (no extension). Leave empty if using a sheet.</summary>
    public string spritePath;

    /// <summary>
    /// Optional rotating turret sprite rendered as a child GO on top of the base sprite.
    /// When set, the base sprite stays static and only this child rotates toward targets.
    /// </summary>
    public string turretSpritePath;

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
    /// <summary>Extra world-space range added to the collider each time this tower upgrades (0 = no range growth).</summary>
    public float rangePerTier = 0f;

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
