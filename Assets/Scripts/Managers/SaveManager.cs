using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Persists per-profile progress to JSON files in Application.persistentDataPath.
/// Three fixed profile slots (profile_0.json … profile_2.json); all progress
/// queries (levels, stars, achievements) operate on the active slot.
/// starsPerLevel stores the highest difficulty beaten per level (0=none, 1=Easy, 2=Medium, 3=Hard).
/// </summary>
public static class SaveManager
{
    public const int MaxProfiles  = 3;
    const int CurrentSaveVersion  = 2;
    const string LastSlotPrefsKey = "profile.lastSlot";

    /// <summary>Fired after any progress write (victory, unlock, reset) and after profile switches.</summary>
    public static event Action OnProgressChanged;

    [Serializable]
    class SaveData
    {
        public int    version = CurrentSaveVersion;
        public string profileName;
        public string createdUtc;
        public string lastPlayedUtc;
        public List<int> unlockedLevels = new List<int> { 1 };
        public List<LevelStarEntry> starsPerLevel = new List<LevelStarEntry>();
        public List<string> earnedAchievements = new List<string>();
    }

    [Serializable]
    class LevelStarEntry
    {
        public int levelIndex;
        public int stars;   // 0=not beaten, 1=Easy, 2=Medium, 3=Hard
    }

    /// <summary>Read-only view of a slot for the profile-select UI.</summary>
    public class ProfileSummary
    {
        public int    slot;
        public bool   exists;
        public string name;
        public int    totalStars;
        public int    levelsBeaten;
        public string lastPlayedUtc;
    }

    static SaveData _data;
    static int      _activeSlot = -1;

    // ── Paths ─────────────────────────────────────────────────────────

    static string SlotPath(int slot)  => Path.Combine(Application.persistentDataPath, $"profile_{slot}.json");
    static string LegacyPath          => Path.Combine(Application.persistentDataPath, "save.json");

    // ── Profile management ────────────────────────────────────────────

    /// <summary>Slot the game is currently reading/writing. Defaults to the last-used slot.</summary>
    public static int ActiveSlot
    {
        get
        {
            if (_activeSlot < 0)
                _activeSlot = Mathf.Clamp(PlayerPrefs.GetInt(LastSlotPrefsKey, 0), 0, MaxProfiles - 1);
            return _activeSlot;
        }
    }

    /// <summary>Switches the active profile. Subsequent queries read/write that slot's file.</summary>
    public static void SetActiveProfile(int slot)
    {
        slot = Mathf.Clamp(slot, 0, MaxProfiles - 1);
        if (slot == _activeSlot && _data != null) return;
        _activeSlot = slot;
        _data       = null;   // lazy-reload from the new slot's file
        PlayerPrefs.SetInt(LastSlotPrefsKey, slot);
        StarManager.Instance?.Refresh();
        OnProgressChanged?.Invoke();
    }

    public static bool ProfileExists(int slot) => File.Exists(SlotPath(slot));

    /// <summary>Creates a fresh profile in the slot (overwrites nothing — caller checks ProfileExists).</summary>
    public static void CreateProfile(int slot, string name)
    {
        slot = Mathf.Clamp(slot, 0, MaxProfiles - 1);
        var data = new SaveData
        {
            profileName   = string.IsNullOrWhiteSpace(name) ? $"Profile {slot + 1}" : name.Trim(),
            createdUtc    = DateTime.UtcNow.ToString("o"),
            lastPlayedUtc = DateTime.UtcNow.ToString("o"),
        };
        WriteTo(SlotPath(slot), data);
        if (slot == _activeSlot) _data = data;
    }

    public static void DeleteProfile(int slot)
    {
        var path = SlotPath(slot);
        if (File.Exists(path)) File.Delete(path);
        if (slot == _activeSlot) _data = null;
    }

    public static ProfileSummary GetProfileSummary(int slot)
    {
        MigrateLegacySave();   // pre-profile saves must surface in the picker
        var summary = new ProfileSummary { slot = slot };
        var data    = ReadFrom(SlotPath(slot));
        if (data == null) return summary;

        summary.exists        = true;
        summary.name          = string.IsNullOrEmpty(data.profileName) ? $"Profile {slot + 1}" : data.profileName;
        summary.lastPlayedUtc = data.lastPlayedUtc;
        foreach (var e in data.starsPerLevel)
        {
            summary.totalStars += e.stars;
            if (e.stars > 0) summary.levelsBeaten++;
        }
        return summary;
    }

    public static string ActiveProfileName()
    {
        EnsureLoaded();
        return string.IsNullOrEmpty(_data.profileName) ? $"Profile {ActiveSlot + 1}" : _data.profileName;
    }

    // ── Load / write ──────────────────────────────────────────────────

    static void EnsureLoaded()
    {
        if (_data != null) return;
        MigrateLegacySave();
        _data = ReadFrom(SlotPath(ActiveSlot)) ?? new SaveData
        {
            profileName = $"Profile {ActiveSlot + 1}",
            createdUtc  = DateTime.UtcNow.ToString("o"),
        };
    }

    static SaveData ReadFrom(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data != null && data.version < CurrentSaveVersion) data.version = CurrentSaveVersion;
            return data;
        }
        catch (Exception e)
        {
            // Preserve the corrupt file for inspection instead of silently losing it
            Debug.LogError($"[SaveManager] Failed to parse '{path}': {e.Message} — backing up and starting fresh.");
            try { File.Copy(path, path + ".corrupt", true); } catch { }
            return null;
        }
    }

    static void Write() => WriteTo(SlotPath(ActiveSlot), _data);

    static void WriteTo(string path, SaveData data)
    {
        // Atomic-ish: write a temp file first so a crash mid-write can't corrupt the profile
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
        File.Copy(tmp, path, true);
        File.Delete(tmp);
    }

    /// <summary>One-time migration: pre-profile save.json becomes profile slot 0.</summary>
    static void MigrateLegacySave()
    {
        if (!File.Exists(LegacyPath)) return;
        for (int i = 0; i < MaxProfiles; i++)
            if (ProfileExists(i)) { File.Delete(LegacyPath); return; }   // profiles already in use

        var legacy = ReadFrom(LegacyPath);
        if (legacy != null)
        {
            legacy.version       = CurrentSaveVersion;
            legacy.profileName   = "Profile 1";
            legacy.createdUtc    = DateTime.UtcNow.ToString("o");
            legacy.lastPlayedUtc = DateTime.UtcNow.ToString("o");
            WriteTo(SlotPath(0), legacy);
            Debug.Log("[SaveManager] Migrated legacy save.json into profile slot 0.");
        }
        File.Delete(LegacyPath);
    }

    // ── Level progress API ────────────────────────────────────────────

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

        _data.lastPlayedUtc = DateTime.UtcNow.ToString("o");
        Write();
        StarManager.Instance?.Refresh();
        OnProgressChanged?.Invoke();
    }

    /// <summary>Sum of highest stars earned across every level.</summary>
    public static int TotalStarsAllLevels()
    {
        EnsureLoaded();
        int total = 0;
        foreach (var e in _data.starsPerLevel) total += e.stars;
        return total;
    }

    public static void UnlockAllLevels(int maxLevel = 20)
    {
        EnsureLoaded();
        for (int i = 1; i <= maxLevel; i++)
            if (!_data.unlockedLevels.Contains(i))
                _data.unlockedLevels.Add(i);
        _data.unlockedLevels.Sort();
        Write();
        OnProgressChanged?.Invoke();
    }

    public static void ResetAll()
    {
        EnsureLoaded();
        _data = new SaveData
        {
            profileName = _data.profileName,
            createdUtc  = _data.createdUtc,
        };
        Write();
        StarManager.Instance?.Refresh();
        OnProgressChanged?.Invoke();
    }

    // ── Achievements API ──────────────────────────────────────────────

    public static bool IsAchievementEarned(string id)
    {
        EnsureLoaded();
        return _data.earnedAchievements.Contains(id);
    }

    /// <summary>Records an earned achievement. Returns false if it was already earned.</summary>
    public static bool MarkAchievementEarned(string id)
    {
        EnsureLoaded();
        if (_data.earnedAchievements.Contains(id)) return false;
        _data.earnedAchievements.Add(id);
        Write();
        return true;
    }
}
