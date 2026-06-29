using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds tower shop buttons at runtime from the level's allowedTowers list.
/// Call Rebuild() whenever a level loads. Empty allowedTowers = all towers.
/// </summary>
public class TowerShop : MonoBehaviour
{
    public static TowerShop Instance { get; private set; }

    [Tooltip("Parent RectTransform that button children are placed under.")]
    public RectTransform shopPanel;

    // Layout
    const float BTN_W    = 85f;
    const float BTN_H    = 80f;
    const float STEP_Y   = -86f;
    const float START_Y  = -46f;
    const float COL_STEP = 95f;
    const int   ROWS_PER_COL = 10;

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

        if (TowerDefinitionLibrary.Instance == null) return;

        bool showAll = allowedTowers == null || allowedTowers.Length == 0;
        var  allowed = showAll ? null : new HashSet<string>(allowedTowers);

        // Group by towerTier → each tier gets its own column
        var byTier = new SortedDictionary<int, List<TowerDefinition>>();
        foreach (var def in TowerDefinitionLibrary.Instance.All.Values)
        {
            if (!showAll && !allowed.Contains(def.id)) continue;
            int tier = Mathf.Max(1, def.towerTier);
            if (!byTier.ContainsKey(tier)) byTier[tier] = new List<TowerDefinition>();
            byTier[tier].Add(def);
        }

        // Sort each column: Physical → Elemental → Arcane
        foreach (var list in byTier.Values)
            list.Sort((a, b) => BalanceOrder(a.balanceType).CompareTo(BalanceOrder(b.balanceType)));

        int   colCount = byTier.Count;
        float colStart = -(colCount - 1) * COL_STEP * 0.5f;
        int   colIdx   = 0;

        foreach (var kv in byTier)
        {
            float xOff = colStart + colIdx * COL_STEP;
            var   defs = kv.Value;
            for (int row = 0; row < defs.Count; row++)
                CreateButton(defs[row], xOff, row);
            colIdx++;
        }
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

    void CreateButton(TowerDefinition def, float xOff, int row)
    {
        var btnGO = new GameObject($"Buy_{def.id}");
        btnGO.transform.SetParent(shopPanel, false);

        var rt              = btnGO.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(xOff, START_Y + STEP_Y * row);
        rt.sizeDelta        = new Vector2(BTN_W, BTN_H);

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

        var labelGO       = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var labelRT       = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;
        var labelTxt      = labelGO.AddComponent<Text>();
        labelTxt.text      = $"{def.displayName}\n<size=11>({def.resourceCost}g)</size>";
        labelTxt.color     = Color.white;
        labelTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelTxt.fontSize  = 14;
        labelTxt.alignment = TextAnchor.MiddleCenter;
    }
}
