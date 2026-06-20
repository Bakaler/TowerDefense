using System;
using System.Collections.Generic;

/// <summary>
/// One wave: an ordered list of spawn groups.
/// Each group maps to one UnitSpawner pass (unit type + count + timing).
/// WaveEntry is reused from UnitSpawner.cs — same serialized fields.
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
