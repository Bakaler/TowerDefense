using System;
using UnityEngine;

/// <summary>
/// Defines a projectile type as plain serializable data.
/// All definitions live in Resources/Definitions/projectiles.json.
/// ProjectileFactory builds a complete GameObject from this — no prefab required.
/// </summary>
[Serializable]
public class ProjectileDefinition
{
    // ── Identity ──────────────────────────────────────────────────────
    public string id;
    public string displayName;
    public string description;

    // ── Movement ──────────────────────────────────────────────────────
    /// <summary>
    /// straight — flies in a fixed direction, hits the first enemy touched
    /// homing   — tracks a target unit, hits only that target
    /// arc      — lobs toward a fixed point and detonates on arrival (mortar)
    /// orbit    — sweeps a full circle around a point ahead of the caster (boomerang)
    /// </summary>
    public string movement = "straight";
    public float  speed    = 10f;
    /// <summary>Seconds until self-destruct. 0 = no timer (orbit ends after a full circle).</summary>
    public float  lifetime = 5f;
    /// <summary>Rotate the sprite to face the travel direction.</summary>
    public bool   faceDirection = false;

    // ── Orbit (boomerang) ─────────────────────────────────────────────
    /// <summary>Distance of the sweep circle's center from the caster.</summary>
    public float arcRadius  = 4f;
    /// <summary>Sweep speed in degrees per second.</summary>
    public float sweepSpeed = 180f;
    /// <summary>Extra self-rotation in degrees per second.</summary>
    public float spinSpeed  = 0f;

    // ── Hit behavior ──────────────────────────────────────────────────
    /// <summary>Collision radius. Trigger collider for straight/homing/arc, overlap scan for orbit.</summary>
    public float hitRadius = 0.15f;
    /// <summary>If true the projectile keeps flying after a hit and can hit further targets.</summary>
    public bool  pierce = false;
    /// <summary>If true, a ShieldBubble intercepts this projectile.</summary>
    public bool  blockedByShields = true;
    /// <summary>Damage dealt to an intercepting shield when blocked.</summary>
    public float shieldAbsorb = 10f;
    /// <summary>Draw a fading line from spawn point to impact (chain lightning look).</summary>
    public bool  drawImpactLine = false;
    /// <summary>Sound played on each impact (sounds.json id). Rate-limited by the sound's own minInterval.</summary>
    public string impactSoundId = "";

    // ── Visuals ───────────────────────────────────────────────────────
    public float  scale = 1f;
    /// <summary>Resources path to a single sprite (no extension). Takes priority over the sheet.</summary>
    public string spritePath;
    /// <summary>Resources path to a sliced sprite sheet (no extension). Pair with spriteIndex.</summary>
    public string spriteSheet;
    public int    spriteIndex = -1;
    public Color  color = Color.white;
    public string sortingLayer = "Units";
    public int    sortingOrder = 10;
}

[Serializable]
public class ProjectileDefinitionCollection
{
    public ProjectileDefinition[] projectiles;
}
