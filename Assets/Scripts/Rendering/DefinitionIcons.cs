using UnityEngine;

/// <summary>
/// Icon-sprite resolution for definitions, matching the tower/ability editor's
/// tooltip previews. Shared by the Journal almanac and the wave preview so a
/// unit always shows the same face everywhere.
/// </summary>
public static class DefinitionIcons
{
    /// <summary>spritePath first (frame 0 of animated sheets via Load's fallback), then spriteSheet cell.</summary>
    public static Sprite TowerBase(TowerDefinition def)
    {
        if (def == null) return null;
        if (!string.IsNullOrEmpty(def.spritePath))
        {
            var s = RuntimeSprites.Load(def.spritePath);
            if (s != null) return s;
        }
        if (!string.IsNullOrEmpty(def.spriteSheet))
            return RuntimeSprites.FromSheet(def.spriteSheet, Mathf.Max(def.spriteIndex, 0));
        return null;
    }

    public static Sprite TowerTurret(TowerDefinition def) =>
        def == null || string.IsNullOrEmpty(def.turretSpritePath) ? null : RuntimeSprites.Load(def.turretSpritePath);

    /// <summary>Walk-animation frame 0, then spriteSheet cell, then single sprite.</summary>
    public static Sprite Unit(UnitDefinition def)
    {
        if (def == null) return null;
        if (!string.IsNullOrEmpty(def.animSheet))
        {
            var s = RuntimeSprites.FromSheet(def.animSheet, 0);
            if (s != null) return s;
        }
        if (!string.IsNullOrEmpty(def.spriteSheet) && def.spriteIndex >= 0)
        {
            var s = RuntimeSprites.FromSheet(def.spriteSheet, def.spriteIndex);
            if (s != null) return s;
        }
        return RuntimeSprites.Load(def.spritePath);
    }
}
