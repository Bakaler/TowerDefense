using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attached to every placed tower. Tracks runtime stats and fires an event
/// when the tower is clicked so GameHUD can show the info panel.
/// </summary>
public class TowerInfo : MonoBehaviour
{
    // ── Populated by TowerFactory ─────────────────────────────────────
    public string      definitionId = "";
    public string      displayName  = "";
    public string      description  = "";
    public BalanceType balanceType  = BalanceType.Physical;
    public float       damage       = 0f;
    public float       cooldown     = 0f;
    public int         resourceCost = 0;

    /// <summary>True on the ghost preview — excluded from balance counts.</summary>
    public bool isGhost = false;

    // ── Runtime ───────────────────────────────────────────────────────
    public int KillCount { get; private set; }

    public float FireRate => cooldown > 0f ? 1f / cooldown : 0f;

    // ── Event ─────────────────────────────────────────────────────────
    public static event Action<TowerInfo> OnTowerClicked;
    public static event Action<TowerInfo> OnTowerKill;

    public void RegisterKill()
    {
        KillCount++;
        OnTowerKill?.Invoke(this);
    }

    void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
        OnTowerClicked?.Invoke(this);
    }
}
