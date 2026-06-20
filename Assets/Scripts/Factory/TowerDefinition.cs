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
    /// <summary>
    /// ID of the Ability_Effect to load from abilities.json via AbilityLibrary.
    /// E.g. "basic_tower_shot"
    /// </summary>
    public string fireAbilityId;

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
