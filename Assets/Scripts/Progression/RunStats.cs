using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-run counters recorded during a level attempt. Game systems ping the
/// Notify* hooks; the victory recap reads the live values, lifetime profile
/// stats absorb them via FlushToProfile, and run-condition achievements get
/// a snapshot via BuildReport. Reset on every level load.
/// </summary>
public static class RunStats
{
    public static int   Kills        { get; private set; }
    public static int   WavesCleared { get; private set; }
    public static int   TowersBuilt  { get; private set; }
    public static int   GoldEarned   { get; private set; }
    public static float LivesLost    { get; private set; }
    public static float Playtime     { get; private set; }

    static readonly HashSet<string> _balanceTypesUsed = new();

    /// <summary>Snapshot handed to AchievementManager at victory.</summary>
    public class Report
    {
        public int   levelIndex;
        public int   difficulty;
        public float livesLost;
        public HashSet<string> balanceTypesUsed;

        // Live BalanceManager scores at the moment of victory
        public float balancePhysical;
        public float balanceElemental;
        public float balanceArcane;

        public float BalanceOf(string balanceType)
        {
            switch (balanceType)
            {
                case "Physical":  return balancePhysical;
                case "Elemental": return balanceElemental;
                case "Arcane":    return balanceArcane;
                default:          return 0f;
            }
        }
    }

    public static void ResetForLevel()
    {
        Kills = WavesCleared = TowersBuilt = GoldEarned = 0;
        LivesLost = 0f;
        Playtime  = 0f;
        _balanceTypesUsed.Clear();
    }

    // ── Hooks (called by game systems) ────────────────────────────────

    public static void NotifyKill()                   => Kills++;
    public static void NotifyWaveCleared()            => WavesCleared++;
    public static void NotifyGoldEarned(int amount)   { if (amount > 0) GoldEarned += amount; }
    public static void NotifyLivesLost(float amount)  { if (amount > 0f) LivesLost += amount; }
    public static void NotifyTowerBuilt(string balanceType)
    {
        TowersBuilt++;
        // "All" counts as every type, so it can never violate a mono-type run
        if (!string.IsNullOrEmpty(balanceType) && balanceType != "All")
            _balanceTypesUsed.Add(balanceType);
    }
    public static void TickPlaytime(float unscaledDelta) => Playtime += unscaledDelta;

    // ── Consumption ───────────────────────────────────────────────────

    public static Report BuildReport(int levelIndex, int difficulty)
    {
        var bm = BalanceManager.Instance;
        return new Report
        {
            levelIndex       = levelIndex,
            difficulty       = difficulty,
            livesLost        = LivesLost,
            balanceTypesUsed = new HashSet<string>(_balanceTypesUsed),
            balancePhysical  = bm != null ? bm.Physical  : 0f,
            balanceElemental = bm != null ? bm.Elemental : 0f,
            balanceArcane    = bm != null ? bm.Arcane    : 0f,
        };
    }

    /// <summary>
    /// Adds this run's counters to the active profile's lifetime stats and
    /// zeroes them so partial flushes (game over, quit) never double-count.
    /// </summary>
    public static void FlushToProfile()
    {
        if (Kills == 0 && WavesCleared == 0 && TowersBuilt == 0 && GoldEarned == 0 && Playtime < 1f)
            return;
        SaveManager.AccumulateLifetimeStats(Kills, WavesCleared, TowersBuilt, GoldEarned, Playtime);
        Kills = WavesCleared = TowersBuilt = GoldEarned = 0;
        Playtime = 0f;
        // LivesLost and type usage stay — they describe the run, not a counter.
    }
}
