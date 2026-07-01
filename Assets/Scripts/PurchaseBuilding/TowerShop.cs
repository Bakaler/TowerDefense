using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds tower shop buttons at runtime from the level's allowedTowers list.
/// Call Rebuild() whenever a level loads. Empty allowedTowers = all towers.
/// Supports SetVisibleTier() so HUDTierSelector can show one column at a time.
/// </summary>
public class TowerShop : MonoBehaviour
{
    public static TowerShop Instance { get; private set; }

    [Tooltip("Parent RectTransform that button children are placed under.")]
    public RectTransform shopPanel;

    // Layout — single-column mode (one tier visible at a time)
    const float BTN_W   = 152f;
    const float BTN_H   = 56f;
    const float STEP_Y  = -62f;
    const float START_Y = -8f;

    // Tier column containers, keyed by tier number
    readonly Dictionary<int, GameObject> _tierColumns = new();
    int _visibleTier = 1;

    public int[]  AvailableTiers  { get; private set; } = System.Array.Empty<int>();
    public int    VisibleTier     => _visibleTier;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Rebuild(string[] allowedTowers)
    {
        if (shopPanel == null) return;

        for (int i = shopPanel.childCount - 1; i >= 0; i--)
            Destroy(shopPanel.GetChild(i).gameObject);
        _tierColumns.Clear();

        if (TowerDefinitionLibrary.Instance == null) return;

        bool showAll = allowedTowers == null || allowedTowers.Length == 0;
        var  allowed = showAll ? null : new HashSet<string>(allowedTowers);

        var byTier = new SortedDictionary<int, List<TowerDefinition>>();
        foreach (var def in TowerDefinitionLibrary.Instance.All.Values)
        {
            if (!showAll && !allowed.Contains(def.id)) continue;
            int tier = Mathf.Max(1, def.towerTier);
            if (!byTier.ContainsKey(tier)) byTier[tier] = new List<TowerDefinition>();
            byTier[tier].Add(def);
        }

        foreach (var list in byTier.Values)
            list.Sort((a, b) => BalanceOrder(a.balanceType).CompareTo(BalanceOrder(b.balanceType)));

        foreach (var kv in byTier)
        {
            int  tier  = kv.Key;
            var  defs  = kv.Value;

            // Each tier gets a container GO inside shopPanel
            var colGO = new GameObject($"Tier_{tier}");
            colGO.transform.SetParent(shopPanel, false);
            var colRT    = colGO.AddComponent<RectTransform>();
            colRT.anchorMin = Vector2.zero;
            colRT.anchorMax = Vector2.one;
            colRT.offsetMin = colRT.offsetMax = Vector2.zero;
            _tierColumns[tier] = colGO;

            for (int row = 0; row < defs.Count; row++)
                CreateButton(defs[row], colGO, row);
        }

        var tiers = new List<int>(byTier.Keys);
        AvailableTiers = tiers.ToArray();

        // Default to first available tier
        _visibleTier = tiers.Count > 0 ? tiers[0] : 1;
        ApplyTierVisibility();
    }

    public void SetVisibleTier(int tier)
    {
        _visibleTier = tier;
        ApplyTierVisibility();
    }

    void ApplyTierVisibility()
    {
        foreach (var kv in _tierColumns)
            kv.Value.SetActive(kv.Key == _visibleTier);
    }

    static int BalanceOrder(string balanceType)
    {
        switch (balanceType)
        {
            case "Physical":  return 0;
            case "Elemental": return 1;
            case "Arcane":    return 2;
            default:          return 3;
        }
    }

    void CreateButton(TowerDefinition def, GameObject column, int row)
    {
        var btnGO = new GameObject($"Buy_{def.id}");
        btnGO.transform.SetParent(column.transform, false);

        var rt       = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, START_Y + STEP_Y * row);
        rt.sizeDelta = new Vector2(BTN_W, BTN_H);

        var img   = btnGO.AddComponent<Image>();
        img.color = new Color(0.18f, 0.20f, 0.28f, 1f);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        var cols = btn.colors;
        cols.highlightedColor = new Color(0.28f, 0.32f, 0.44f, 1f);
        cols.pressedColor     = new Color(0.12f, 0.14f, 0.20f, 1f);
        btn.colors = cols;

        var shopBtn     = btnGO.AddComponent<TowerShopButton>();
        shopBtn.towerId = def.id;
        btn.onClick.AddListener(shopBtn.OnButtonPress);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var labelRT       = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(8f, 0f);
        labelRT.offsetMax = Vector2.zero;
        var labelTxt      = labelGO.AddComponent<Text>();
        labelTxt.text      = $"{def.displayName}\n<size=10>({def.resourceCost}g)</size>";
        labelTxt.color     = Color.white;
        labelTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelTxt.fontSize  = 12;
        labelTxt.alignment = TextAnchor.MiddleCenter;
    }
}
