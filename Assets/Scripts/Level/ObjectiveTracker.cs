using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime tracker for in-level objectives defined in the level JSON.
/// Systems call the static Notify* methods; the tracker updates progress
/// and fires OnObjectiveUpdated for the HUD to refresh.
/// </summary>
public static class ObjectiveTracker
{
    public static event Action OnObjectiveUpdated;

    static ObjectiveDef[] _defs   = Array.Empty<ObjectiveDef>();
    static int[]          _progress;

    // ── Setup ─────────────────────────────────────────────────────────

    public static void Load(ObjectiveDef[] objectives)
    {
        _defs     = objectives ?? Array.Empty<ObjectiveDef>();
        _progress = new int[_defs.Length];
        OnObjectiveUpdated?.Invoke();
    }

    public static void Reset()
    {
        _defs     = Array.Empty<ObjectiveDef>();
        _progress = Array.Empty<int>();
    }

    // ── Read API (for HUD) ────────────────────────────────────────────

    public static IReadOnlyList<ObjectiveDef> Objectives => _defs;
    public static int GetProgress(int i) => _progress != null && i < _progress.Length ? _progress[i] : 0;
    public static bool IsComplete(int i)  => GetProgress(i) >= _defs[i].count;
    public static bool AllRequiredComplete()
    {
        for (int i = 0; i < _defs.Length; i++)
            if (_defs[i].required && !IsComplete(i)) return false;
        return true;
    }

    // ── Notify hooks ──────────────────────────────────────────────────

    public static void NotifyBuild(string definitionId)   => Increment("BuildTower",   definitionId);
    public static void NotifyUpgrade(string definitionId) => Increment("UpgradeTower", definitionId);
    public static void NotifyKill(string definitionId)    => Increment("KillEnemy",    definitionId);
    public static void NotifyWaveReached(int wave)
    {
        for (int i = 0; i < _defs.Length; i++)
        {
            if (_defs[i].type != "ReachWave") continue;
            if (wave >= _defs[i].count && _progress[i] == 0)
            {
                _progress[i] = _defs[i].count;
                OnObjectiveUpdated?.Invoke();
            }
        }
    }
    public static void NotifyLivesRemaining(float lives)
    {
        for (int i = 0; i < _defs.Length; i++)
        {
            if (_defs[i].type != "SurviveWithLives") continue;
            // Marked complete only at victory — tracked differently; just record current value
            _progress[i] = (int)lives;
            OnObjectiveUpdated?.Invoke();
        }
    }

    // ── Internal ──────────────────────────────────────────────────────

    static void Increment(string type, string targetId)
    {
        bool changed = false;
        for (int i = 0; i < _defs.Length; i++)
        {
            var def = _defs[i];
            if (def.type != type) continue;
            if (def.targetId != "any" && def.targetId != targetId) continue;
            if (_progress[i] >= def.count) continue;

            _progress[i]++;
            changed = true;
        }
        if (changed) OnObjectiveUpdated?.Invoke();
    }
}
