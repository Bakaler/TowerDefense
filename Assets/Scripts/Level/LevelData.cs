using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ModifierDef
{
    public string id          = "";
    public string displayName = "";
    public string description = "";
    public string effectType  = "";   // StartingGold, StartingLives, StartingTech, TowerSpeedMult, TowerRangeMult, TowerDamageMult
    public float  value       = 0f;
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
    public static void Add(ModifierDef mod) { if (mod != null) Chosen.Add(mod); }
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
    /// <summary>Tower definition IDs available to buy in this level. Empty = all towers allowed.</summary>
    public string[]          allowedTowers  = Array.Empty<string>();
    /// <summary>Modifier columns shown before the level starts. Empty = skip modifier screen.</summary>
    public ModifierColumn[]  modifierColumns = Array.Empty<ModifierColumn>();
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
