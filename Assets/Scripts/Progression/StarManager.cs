using UnityEngine;

/// <summary>
/// Computes and caches total stars earned across all levels from SaveManager.
/// Stars unlock shop columns globally — earned once, available forever.
/// Column thresholds: C1=0, C2=3, C3=10, C4=20, C5=45, C6=55
/// </summary>
public class StarManager : MonoBehaviour
{
    public static StarManager Instance { get; private set; }

    // Column star thresholds (1-indexed: index 0 = C1, etc.)
    public static readonly int[] ColumnThresholds = { 3, 10, 20, 45, 55 };

    public int TotalStars { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Refresh();
    }

    /// <summary>Re-sum stars from SaveManager. Call after any victory is recorded.</summary>
    public void Refresh()
    {
        TotalStars = SaveManager.TotalStarsAllLevels();
        Debug.Log($"[StarManager] TotalStars = {TotalStars}");
    }

    public bool IsColumnUnlocked(int column)
    {
        int idx = Mathf.Clamp(column - 1, 0, ColumnThresholds.Length - 1);
        return TotalStars >= ColumnThresholds[idx];
    }

    public static int ThresholdFor(int column)
    {
        int idx = Mathf.Clamp(column - 1, 0, ColumnThresholds.Length - 1);
        return ColumnThresholds[idx];
    }

    [ContextMenu("Debug: Refresh Stars")]
    void DebugRefresh() { Refresh(); Debug.Log($"[StarManager] Total stars: {TotalStars}"); }
}
