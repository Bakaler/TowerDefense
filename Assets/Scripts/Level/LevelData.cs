using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Leaf-level modifier — same fields as ModifierDef but no subEffects, breaking the cycle.</summary>
[Serializable]
public class SubEffectDef
{
    public string id          = "";
    public string displayName = "";
    public string description = "";
    public string effectType  = "";
    public float  value       = 0f;
}

[Serializable]
public class ModifierDef
{
    public string         id          = "";
    public string         displayName = "";
    public string         description = "";
    public string         effectType  = "";
    public float          value       = 0f;
    /// <summary>Child effects applied instead of this modifier's own effectType.</summary>
    public SubEffectDef[] subEffects  = System.Array.Empty<SubEffectDef>();
}

[Serializable]
public class ModifierColumn
{
    public ModifierDef[] options = Array.Empty<ModifierDef>();
}

/// <summary>Carries chosen modifiers from the modifier select scene into the game scene.</summary>
public static class ModifierSelection
{
    public static readonly List<ModifierDef> Chosen = new List<ModifierDef>();
    public static void Clear() => Chosen.Clear();

    public static void Add(ModifierDef mod)
    {
        if (mod == null) return;
        if (mod.subEffects != null && mod.subEffects.Length > 0)
        {
            foreach (var sub in mod.subEffects)
            {
                // Wrap SubEffectDef as a ModifierDef leaf so the rest of the pipeline is unchanged
                Chosen.Add(new ModifierDef {
                    id = sub.id, displayName = sub.displayName,
                    description = sub.description, effectType = sub.effectType,
                    value = sub.value
                });
            }
        }
        else
            Chosen.Add(mod);
    }

    /// <summary>Sum of all values matching effectType.</summary>
    public static float GetFloat(string effectType)
    {
        float total = 0f;
        foreach (var m in Chosen)
            if (m.effectType == effectType) total += m.value;
        return total;
    }

    public static bool HasEffect(string effectType)
    {
        foreach (var m in Chosen)
            if (m.effectType == effectType) return true;
        return false;
    }
}

/// <summary>
/// One in-level objective. type values:
///   "BuildTower"      — build count towers matching targetId (or "any")
///   "UpgradeTower"    — upgrade count towers matching targetId
///   "KillEnemy"       — kill count enemies matching targetId
///   "ReachWave"       — reach wave number stored in count
///   "SurviveWithLives"— finish with at least count lives (checked at victory)
/// required=true objectives are tracked prominently; optional ones are bonus.
/// </summary>
[Serializable]
public class ObjectiveDef
{
    public string id          = "";
    public string description = "";
    public string type        = "";
    public string targetId    = "any";
    public int    count       = 1;
    public bool   required    = true;
}

[Serializable]
public class DifficultyDef
{
    public string label          = "";
    // Enemy multipliers
    public float  enemyHpMult    = 1f;
    public float  enemySpeedMult = 1f;
    // Economy multipliers
    public float  goldMult       = 1f;   // applied to startGold
    public float  bountyMult     = 1f;   // applied to per-kill bounty value
    // Per-level overrides (-1 = inherit base level value)
    public int    startGold      = -1;
    public int    startLives     = -1;
    public int    startTech      = -1;
    public int    startTier      = -1;
    public int    maxTowers      = -1;
    // Array overrides (empty = inherit base level value)
    public string[]       allowedTowers  = System.Array.Empty<string>();
    /// <summary>"wi,gi" pairs — wave groups disabled at this difficulty.</summary>
    public string[]       disabledGroups = System.Array.Empty<string>();
    public ObjectiveDef[] objectives     = System.Array.Empty<ObjectiveDef>();
}

/// <summary>Data classes that map to the level JSON files under Resources/Definitions/Levels/.</summary>

[Serializable]
public class LevelData
{
    public string    id             = "level_1";
    public string    displayName    = "Level 1";
    public int       startGold      = 25;
    public int       startLives     = 20;
    public int       startTech      = 0;
    public int       startTier      = 1;
    public string    backgroundSprite = "";   // Resources path, e.g. "Art/Backgrounds/bg_1"
    public float     backgroundX      = 0f;
    public float     backgroundY      = 0f;
    /// <summary>Hard cap on placed towers for this level. -1 = use balance system default.</summary>
    public int               maxTowers      = -1;
    /// <summary>Tower definition IDs available to buy in this level. Empty = all towers allowed.</summary>
    public string[]          allowedTowers  = Array.Empty<string>();
    /// <summary>Modifier columns shown before the level starts. Empty = skip modifier screen.</summary>
    public ModifierColumn[]  modifierColumns = Array.Empty<ModifierColumn>();
    /// <summary>Difficulty tiers. Index 0=Easy, 1=Medium, 2=Hard.</summary>
    public DifficultyDef[]   difficulties    = Array.Empty<DifficultyDef>();
    /// <summary>In-level objectives shown to the player. Empty = no objective panel.</summary>
    public ObjectiveDef[]    objectives      = Array.Empty<ObjectiveDef>();
    public PathData[]        paths         = Array.Empty<PathData>();
    public ZoneData[]        placementZones = Array.Empty<ZoneData>();
    public WaveDefinition[]  waves         = Array.Empty<WaveDefinition>();
}

[Serializable]
public class PathData
{
    public int        spawnerIndex = 0;
    public NodeData[] nodes        = Array.Empty<NodeData>();
}

[Serializable]
public class NodeData
{
    public string   id   = "";
    public float    x    = 0f;
    public float    y    = 0f;
    /// <summary>Units reaching this node fade out and reappear at the next node.</summary>
    public bool     teleporter = false;
    /// <summary>Seconds a unit stays vanished between fading out and fading back in.</summary>
    public float    teleportDelay = 0f;
    public string[] next = Array.Empty<string>();
}

[Serializable]
public class ZoneData
{
    public string      type   = "circle";   // "circle" or "lane"
    // circle
    public float       x      = 0f;
    public float       y      = 0f;
    public float       radius = 2f;
    // lane
    public float       width  = 3f;
    public VertexData[] points = Array.Empty<VertexData>();
}

[Serializable]
public class VertexData
{
    public float x     = 0f;
    public float y     = 0f;
    public float width = 3f;
}
