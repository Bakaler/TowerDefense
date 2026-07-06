using System;

/// <summary>
/// One achievement as plain serializable data.
/// All definitions live in Resources/Definitions/achievements.json.
/// Conditions are evaluated by AchievementManager against SaveManager state;
/// each definition evaluates independently, so several can unlock at once.
/// </summary>
[Serializable]
public class AchievementDefinition
{
    // ── Identity ──────────────────────────────────────────────────────
    public string id;
    public string title;
    public string description;

    /// <summary>Hidden achievements show a blacked-out icon and masked text until earned.</summary>
    public bool hidden;

    // ── Art (RuntimeSprites resolution order: iconPath, then iconSheet+iconIndex) ──
    public string iconPath;
    public string iconSheet;
    public int    iconIndex = -1;

    // ── Condition ─────────────────────────────────────────────────────
    /// <summary>
    /// "LevelStars"  — GetStars(levelIndex) >= minStars
    /// "TotalStars"  — TotalStarsAllLevels() >= minStars
    /// </summary>
    public string conditionType;
    public int    levelIndex;
    public int    minStars;
}

[Serializable]
public class AchievementDefinitionList
{
    public AchievementDefinition[] achievements;
}
