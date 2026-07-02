using System;
using UnityEngine;

/// <summary>
/// Defines a tower-owned minion (drone-like sub-unit) as plain serializable data.
/// All definitions live in Resources/Definitions/minions.json.
/// MinionFactory builds a complete GameObject from this — no prefab required.
/// </summary>
[Serializable]
public class MinionDefinition
{
    // ── Identity ──────────────────────────────────────────────────────
    public string id;
    public string displayName;
    public string description;

    // ── Brain (Wander → Engage → Return → Rest state machine) ────────
    public float moveSpeed    = 2.8f;   // wander speed
    public float engageSpeed  = 4.2f;   // speed while attacking
    public float returnSpeed  = 5.5f;   // speed flying back to the hive
    public float orbitDist    = 0.6f;   // preferred distance while circling a target
    public float wanderRadius = 1.1f;   // wander goal radius around the hive
    public float maxAwayTime  = 6f;     // seconds away from the hive before forced return
    public float restDuration = 1f;     // seconds resting at the hive before redeploying

    // ── Attack ────────────────────────────────────────────────────────
    public float  attackCooldown = 0.8f;
    /// <summary>Projectile fired at engaged targets (projectiles.json).</summary>
    public string projectileId = "";
    /// <summary>Default impact effect (effects.json). The owner can override it.</summary>
    public string impactEffectId = "";

    // ── Visuals ───────────────────────────────────────────────────────
    public float  scale = 1f;
    /// <summary>Resources path to a single sprite or sliced animation sheet (no extension).</summary>
    public string spritePath;
    public float  animFps = 12f;
    public Color  tintColor = Color.white;
    public string sortingLayer = "Towers";
    public int    sortingOrder = 100;
}

[Serializable]
public class MinionDefinitionCollection
{
    public MinionDefinition[] minions;
}
