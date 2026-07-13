using UnityEngine;

/// <summary>
/// Shared check for "is this click grabbing a collectible?" — income-tower
/// orbs or research orbs. Selection paths (GameHUD click loop,
/// UnitManager.OnMouseDown) consult this so collecting never also opens
/// a tower/enemy info panel. Bounty drops collect on hover, so they never
/// swallow clicks.
/// </summary>
public static class CollectClickGuard
{
    public static bool IsOverCollectible(Vector2 worldPos)
    {
        // Research towers only swallow the click while holding an orb;
        // an empty one should still be selectable for upgrades/info.
        foreach (var rt in Object.FindObjectsByType<ResearchTower>(FindObjectsSortMode.None))
            if (rt.HasOrb &&
                Vector2.Distance(worldPos, (Vector2)rt.transform.position) <= ResearchTower.CollectRadius)
                return true;

        // Income towers only swallow the click while they have orbs to collect;
        // an empty one should still be selectable for upgrades/info.
        foreach (var it in Object.FindObjectsByType<IncomeTower>(FindObjectsSortMode.None))
            if (it.OrbCount > 0 &&
                Vector2.Distance(worldPos, (Vector2)it.transform.position) <= IncomeTower.CollectRadius)
                return true;

        return false;
    }
}
