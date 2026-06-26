using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic shop button. Set towerId in the inspector to any id in towers.json.
/// Replaces PurchaseBuilding1 / PurchaseBuilding2 — one component for all towers.
/// </summary>
public class TowerShopButton : MonoBehaviour
{
    [Tooltip("Must match an id in Resources/Definitions/towers.json")]
    public string towerId = "";

    void Start()
    {
        if (TowerDefinitionLibrary.Instance == null) return;
        if (!TowerDefinitionLibrary.Instance.TryGet(towerId, out var def)) return;

        // Refresh label from live definition
        var label = GetComponentInChildren<Text>();
        if (label != null)
            label.text = $"{def.displayName}\n<size=13>({def.resourceCost}g)</size>";

        // Colored border matching balance type
        var img = GetComponent<Image>();
        if (img != null)
        {
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor     = BalanceColor(def.balanceType);
            outline.effectDistance  = new Vector2(2.5f, -2.5f);
            outline.useGraphicAlpha = false;
        }
    }

    static Color BalanceColor(string balanceType)
    {
        switch (balanceType)
        {
            case "Physical":  return new Color(0.85f, 0.15f, 0.15f, 1f);
            case "Arcane":    return new Color(0.25f, 0.45f, 1.00f, 1f);
            case "Elemental": return new Color(0.55f, 0.30f, 0.08f, 1f);
            default:          return new Color(0.50f, 0.50f, 0.50f, 1f);
        }
    }

    public void OnButtonPress()
    {
        if (TowerPlacer.Instance == null)
        {
            Debug.LogError("[TowerShopButton] TowerPlacer not in scene.");
            return;
        }
        GameHUD.Instance?.CloseAllPanels();
        TowerPlacer.Instance.SelectTower(towerId);
    }
}
