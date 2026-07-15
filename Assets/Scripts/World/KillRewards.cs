using UnityEngine;

/// <summary>
/// Shared kill payout: tower kill counter (which feeds RunStats and achievements),
/// bounty drop, and the BountyPerKill modifier. Called by Effect_Damage for direct
/// hits and by BehaviorHandler for damage-over-time ticks, so DoT kills pay out
/// exactly like direct-hit kills.
/// </summary>
public static class KillRewards
{
    public static void Award(UnitParentClass target, GameObject originTower)
    {
        if (target == null) return;

        if (originTower != null)
            originTower.GetComponent<TowerInfo>()?.RegisterKill();

        BountyDrop.TrySpawn(target.transform.position, target as UnitManager);

        float bonusBounty = ModifierSelection.GetFloat("BountyPerKill");
        if (bonusBounty >= 1f)
            ResourceManagerScript.Instance?.ChangeResourceOne((int)bonusBounty);
    }
}
