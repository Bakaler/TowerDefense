using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads achievement definitions and awards them by re-evaluating every
/// unearned condition against SaveManager state whenever progress changes.
/// Earned ids persist in the active profile's save file. Self-bootstraps
/// like AudioManager — no scene wiring needed.
/// </summary>
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    /// <summary>Fired once per newly earned achievement (toast UI listens).</summary>
    public static event System.Action<AchievementDefinition> OnAchievementEarned;

    const string DefinitionsPath = "Definitions/achievements";

    readonly List<AchievementDefinition> _all = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[AchievementManager]");
        DontDestroyOnLoad(go);
        go.AddComponent<AchievementManager>();
        go.AddComponent<AchievementToastUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadDefinitions();
        SaveManager.OnProgressChanged += EvaluateAll;
        EvaluateAll();   // catch anything earned before this build shipped the definition
    }

    void OnDestroy()
    {
        if (Instance == this) SaveManager.OnProgressChanged -= EvaluateAll;
    }

    void LoadDefinitions()
    {
        var ta = Resources.Load<TextAsset>(DefinitionsPath);
        if (ta == null)
        {
            Debug.LogWarning($"[AchievementManager] No file at Resources/{DefinitionsPath}.json");
            return;
        }
        var wrapper = JsonUtility.FromJson<AchievementDefinitionList>(ta.text);
        if (wrapper?.achievements != null)
            _all.AddRange(wrapper.achievements);
        Debug.Log($"[AchievementManager] Loaded {_all.Count} achievement(s).");
    }

    // ── Public API ────────────────────────────────────────────────────

    public IReadOnlyList<AchievementDefinition> All => _all;

    public bool IsEarned(string id) => SaveManager.IsAchievementEarned(id);

    // ── Evaluation ────────────────────────────────────────────────────

    void EvaluateAll()
    {
        foreach (var def in _all)
        {
            if (string.IsNullOrEmpty(def.id)) continue;
            if (SaveManager.IsAchievementEarned(def.id)) continue;
            if (!ConditionMet(def)) continue;

            if (SaveManager.MarkAchievementEarned(def.id))
            {
                Debug.Log($"[AchievementManager] Earned '{def.title}' ({def.id})");
                AudioManager.PlayEvent("achievement_earned");
                OnAchievementEarned?.Invoke(def);
            }
        }
    }

    static bool ConditionMet(AchievementDefinition def)
    {
        switch (def.conditionType)
        {
            case "LevelStars": return SaveManager.GetStars(def.levelIndex) >= def.minStars;
            case "TotalStars": return SaveManager.TotalStarsAllLevels() >= def.minStars;
            default:
                Debug.LogWarning($"[AchievementManager] Unknown conditionType '{def.conditionType}' on '{def.id}'");
                return false;
        }
    }
}
