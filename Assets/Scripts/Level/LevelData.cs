using System;
using UnityEngine;

/// <summary>Data classes that map to the level JSON files under Resources/Definitions/Levels/.</summary>

[Serializable]
public class LevelData
{
    public string    id             = "level_1";
    public string    displayName    = "Level 1";
    public int       startGold      = 25;
    public int       startLives     = 20;
    public string    backgroundSprite = "";   // Resources path, e.g. "Art/Backgrounds/bg_1"
    public float     backgroundX      = 0f;
    public float     backgroundY      = 0f;
    /// <summary>Tower definition IDs available to buy in this level. Empty = all towers allowed.</summary>
    public string[]          allowedTowers  = Array.Empty<string>();
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
