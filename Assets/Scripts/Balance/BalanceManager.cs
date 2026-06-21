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

    public float Elemental { get; private set; }
    public float Arcane    { get; private set; }
    public float Physical  { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Recalculate()
    {
        var towers = FindObjectsByType<TowerInfo>(FindObjectsSortMode.None);

        var idCounts = new Dictionary<string, int>();
        foreach (var t in towers)
        {
            if (t.isGhost) continue;
            if (!idCounts.ContainsKey(t.definitionId)) idCounts[t.definitionId] = 0;
            idCounts[t.definitionId]++;
        }

        float e = 0f, a = 0f, p = 0f;
        foreach (var t in towers)
        {
            if (t.isGhost) continue;
            float ratio = BalanceRatio(idCounts[t.definitionId]);
            switch (t.balanceType)
            {
                case BalanceType.Elemental: e += ratio; break;
                case BalanceType.Arcane:    a += ratio; break;
                case BalanceType.Physical:  p += ratio; break;
            }
        }

        Elemental = e;
        Arcane    = a;
        Physical  = p;
    }

    static float BalanceRatio(int count)
    {
        if (count <= 0) return 0f;
        if (count <= 4) return 1f;
        float r = 4f / count;
        return r * r;
    }
}
