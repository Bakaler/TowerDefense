using UnityEngine;

/// <summary>
/// Generic shop button. Set towerId in the inspector to any id in towers.json.
/// Replaces PurchaseBuilding1 / PurchaseBuilding2 — one component for all towers.
/// </summary>
public class TowerShopButton : MonoBehaviour
{
    [Tooltip("Must match an id in Resources/Definitions/towers.json")]
    public string towerId = "";

    public void OnButtonPress()
    {
        if (TowerPlacer.Instance == null)
        {
            Debug.LogError("[TowerShopButton] TowerPlacer not in scene.");
            return;
        }
        TowerPlacer.Instance.SelectTower(towerId);
    }
}
