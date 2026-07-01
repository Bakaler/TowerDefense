using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Persists level progress to a JSON file in Application.persistentDataPath.
/// starsPerLevel stores the highest difficulty beaten per level (0=none, 1=Easy, 2=Medium, 3=Hard).
/// </summary>
public static class SaveManager
{
    static readonly string SavePath = Path.Combine(Application.persistentDataPath, "save.json");

    [Serializable]
    class SaveData
    {
        public List<int> unlockedLevels = new List<int> { 1 };
        public List<LevelStarEntry> starsPerLevel = new List<LevelStarEntry>();
    }

    [Serializable]
    class LevelStarEntry
    {
        public int levelIndex;
        public int stars;   // 0=not beaten, 1=Easy, 2=Medium, 3=Hard
    }

    static SaveData _data;

    static void EnsureLoaded()
    {
        if (_data != null) return;
        if (File.Exists(SavePath))
        {
            try { _data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)); }
            catch { _data = null; }
        }
        if (_data == null) _data = new SaveData();
    }

    static void Write() => File.WriteAllText(SavePath, JsonUtility.ToJson(_data, true));

    // ── Public API ────────────────────────────────────────────────────────

    public static bool IsLevelUnlocked(int levelIndex)
    {
        EnsureLoaded();
        return _data.unlockedLevels.Contains(levelIndex);
    }

    /// <summary>Returns highest stars earned on this level (0-3).</summary>
    public static int GetStars(int levelIndex)
    {
        EnsureLoaded();
        var entry = _data.starsPerLevel.Find(e => e.levelIndex == levelIndex);
        return entry?.stars ?? 0;
    }

    /// <summary>
    /// Call on victory. difficulty = 0/1/2 (Easy/Medium/Hard = 1/2/3 stars).
    /// Unlocks the next level if not already unlocked.
    /// </summary>
    public static void RecordVictory(int levelIndex, int difficulty)
    {
        EnsureLoaded();

        int starsEarned = difficulty + 1;
        var entry = _data.starsPerLevel.Find(e => e.levelIndex == levelIndex);
        if (entry == null)
        {
            entry = new LevelStarEntry { levelIndex = levelIndex };
            _data.starsPerLevel.Add(entry);
        }
        if (starsEarned > entry.stars)
            entry.stars = starsEarned;

        int nextLevel = levelIndex + 1;
        if (!_data.unlockedLevels.Contains(nextLevel))
        {
            _data.unlockedLevels.Add(nextLevel);
            _data.unlockedLevels.Sort();
        }

        Write();
    }

    public static void UnlockAllLevels(int maxLevel = 20)
    {
        EnsureLoaded();
        for (int i = 1; i <= maxLevel; i++)
            if (!_data.unlockedLevels.Contains(i))
                _data.unlockedLevels.Add(i);
        _data.unlockedLevels.Sort();
        Write();
    }

    public static void ResetAll()
    {
        _data = new SaveData();
        Write();
    }
}
