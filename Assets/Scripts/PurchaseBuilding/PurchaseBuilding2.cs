using UnityEngine;

/// <summary>
/// Legacy button script — now delegates to TowerPlacer so towers are built
/// entirely from data (no prefab instantiation).
/// Set towerId to "income_tower" (or any id from towers.json) in the Inspector.
/// </summary>
public class PurchaseBuilding2 : MonoBehaviour
{
    [Tooltip("Must match an id in Resources/Definitions/towers.json")]
    public string towerId = "income_tower";

    public void OnButtonPress()
    {
        if (TowerPlacer.Instance == null)
        {
            Debug.LogError("[PurchaseBuilding2] TowerPlacer not in scene.");
            return;
        }
        TowerPlacer.Instance.SelectTower(towerId);
    }
}
