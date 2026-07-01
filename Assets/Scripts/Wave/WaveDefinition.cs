using System;
using System.Collections.Generic;

/// <summary>
/// Controls when kills in a wave produce a BountyDrop.
/// mode:
///   "chance"      — balance-based probability (default)
///   "always"      — every kill drops
///   "never"       — no drops this wave
///   "alternating" — every other kill drops; rest use fallbackChance
///   "first_only"  — only the first kill drops
///   "pattern"     — follow int[] pattern: 0=never, 1=always, 2=chance
///                   repeat=true loops the pattern; repeat=false uses fallbackChance after exhaustion
/// fallbackChance: 0-1 override; -1 = use balance-based default
/// </summary>
[Serializable]
public class DropConfig
{
    public string mode           = "chance";
    public float  fallbackChance = -1f;
    public int[]  pattern        = Array.Empty<int>();
    public bool   repeat         = true;
}

/// <summary>
/// One wave: an ordered list of spawn groups.
/// </summary>
[Serializable]
public class WaveDefinition
{
    public List<WaveEntry> groups = new List<WaveEntry>();
}

/// <summary>
/// Root JSON wrapper. Matches the structure of waves.json.
/// </summary>
[Serializable]
public class WaveCollection
{
    public List<WaveDefinition> waves = new List<WaveDefinition>();
}
