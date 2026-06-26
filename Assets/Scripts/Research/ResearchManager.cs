using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks purchased tower researches and applies their effects globally
/// to the relevant shared ability/effect instances.
/// </summary>
public class ResearchManager : MonoBehaviour
{
    public static ResearchManager Instance { get; private set; }

    public string definitionsPath = "Definitions/researches";

    private List<ResearchDefinition>  _all       = new();
    private HashSet<string>           _purchased = new();

    public static event System.Action OnResearchChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Load();
    }

    void Load()
    {
        var ta = Resources.Load<TextAsset>(definitionsPath);
        if (ta == null) { Debug.LogWarning($"[ResearchManager] No file at Resources/{definitionsPath}.json"); return; }
        var wrapper = JsonUtility.FromJson<ResearchDefinitionList>(ta.text);
        if (wrapper?.researches != null)
            _all.AddRange(wrapper.researches);
    }

    public List<ResearchDefinition> GetForTower(string towerId)
    {
        var result = new List<ResearchDefinition>();
        foreach (var r in _all)
            if (r.towerId == towerId) result.Add(r);
        return result;
    }

    public bool IsPurchased(string id) => _purchased.Contains(id);

    public void ResetAll()
    {
        _purchased.Clear();
        OnResearchChanged?.Invoke();
    }

    public bool TryPurchase(ResearchDefinition def)
    {
        if (_purchased.Contains(def.id)) return false;

        var tm = TechManager.Instance;
        if (tm == null || !tm.TrySpendTech(def.techCost)) return false;
        _purchased.Add(def.id);
        ApplyEffect(def);
        OnResearchChanged?.Invoke();
        Debug.Log($"[Research] Purchased '{def.displayName}'");
        return true;
    }

    void ApplyEffect(ResearchDefinition def)
    {
        switch (def.effectType)
        {
            case "FireRateBonus":
                ApplyFireRateBonus(def);
                break;
            case "BulletCountBonus":
                ApplyBulletCountBonus(def);
                break;
            default:
                Debug.LogWarning($"[ResearchManager] Unknown effectType '{def.effectType}'");
                break;
        }
    }

    static void ApplyFireRateBonus(ResearchDefinition def)
    {
        if (AbilityLibrary.Instance == null) return;
        if (!AbilityLibrary.Instance.TryGet(def.abilityId, out var ability)) return;
        // effectValue is negative (e.g. -0.1 = -10% cooldown = faster firing)
        ability.cost.cooldownDuration *= (1f + def.effectValue);
        Debug.Log($"[Research] FireRateBonus on '{def.abilityId}': cooldown → {ability.cost.cooldownDuration:F3}s");
    }

    static void ApplyBulletCountBonus(ResearchDefinition def)
    {
        if (EffectLibrary.Instance == null) return;
        var effect = EffectLibrary.Instance.GetEffect(def.effectId);
        if (effect is Effect_Launch_Shotgun shotgun)
        {
            shotgun.pelletCount += Mathf.RoundToInt(def.effectValue);
            Debug.Log($"[Research] BulletCountBonus: pelletCount → {shotgun.pelletCount}");
        }
        else
        {
            Debug.LogWarning($"[ResearchManager] Effect '{def.effectId}' is not Effect_Launch_Shotgun.");
        }
    }
}
