using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight singleton that exposes live balance scores so game systems
/// (drop chance, income rate, etc.) can read them without coupling to GameHUD.
/// GameHUD still owns the display; it calls Recalculate() each frame.
/// </summary>
public class BalanceManager : MonoBehaviour
{
    public static BalanceManager Instance { get; private set; }

    public float Elemental  { get; private set; }
    public float Arcane     { get; private set; }
    public float Physical   { get; private set; }
    public int   MaxTowers  { get; private set; } = 10;
    public int   TowerCount { get; private set; }

    private int _levelCap   = -1;
    private int _bonusSlots = 0;
    public void SetLevelCap(int cap) { _levelCap = cap; _bonusSlots = 0; MarkDirty(); }
    public void AddBonusSlots(int n) { _bonusSlots += n; MarkDirty(); }

    // Recalculate() is a no-op until something changes the tower set — TowerInfo
    // marks dirty on enable/disable/upgrade, so the per-frame call in GameHUD is free.
    static bool _dirty = true;
    public static void MarkDirty() => _dirty = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _dirty = true;

    public static readonly int[] Thresholds = { 12, 36, 80 };

    /// <summary>Copies of the same tower id that earn full balance credit before decay kicks in.</summary>
    public const int FullCreditCount = 4;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Recalculate()
    {
        if (!_dirty) return;
        _dirty = false;

        var towers = TowerInfo.All;

        // Count how many of each tower ID exist, grouped by balance type
        var idCounts   = new Dictionary<string, int>();
        var typeMaxCount = new Dictionary<BalanceType, int>
        {
            { BalanceType.Elemental, 0 },
            { BalanceType.Arcane,    0 },
            { BalanceType.Physical,  0 },
            { BalanceType.All,       0 },
        };

        // Registry only contains non-ghost towers
        foreach (var t in towers)
        {
            if (!idCounts.ContainsKey(t.definitionId)) idCounts[t.definitionId] = 0;
            idCounts[t.definitionId]++;
        }

        // Worst decay within a type = highest single-ID count of that type
        foreach (var t in towers)
        {
            int cnt = idCounts[t.definitionId];
            if (cnt > typeMaxCount[t.balanceType])
                typeMaxCount[t.balanceType] = cnt;
        }

        // Each tower contributes BalanceRatio(worstDecayForItsType)
        float e = 0f, a = 0f, p = 0f;
        foreach (var t in towers)
        {
            float ratio = BalanceRatio(typeMaxCount[t.balanceType]) * t.balanceMultiplier;
            switch (t.balanceType)
            {
                case BalanceType.Elemental: e += ratio; break;
                case BalanceType.Arcane:    a += ratio; break;
                case BalanceType.Physical:  p += ratio; break;
                case BalanceType.All:
                    // Counts toward every type, contribution split evenly
                    e += ratio / 3f; a += ratio / 3f; p += ratio / 3f;
                    break;
            }
        }

        Elemental  = e;
        Arcane     = a;
        Physical   = p;
        int total  = Mathf.FloorToInt(e + a + p);
        int slots  = 0;
        foreach (int t in Thresholds) if (total >= t) slots++;
        int balanced = 10 + slots * 4;
        int cap = _levelCap > 0 ? Mathf.Min(balanced, _levelCap) : balanced;
        MaxTowers  = cap + _bonusSlots;
        TowerCount = towers.Count;
    }

    static float BalanceRatio(int count)
    {
        if (count <= 0) return 0f;
        if (count <= FullCreditCount) return 1f;
        float r = (float)FullCreditCount / count;
        return r * r;
    }
}
