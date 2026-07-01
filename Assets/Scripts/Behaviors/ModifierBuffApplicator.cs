using UnityEngine;

/// <summary>
/// Reads ModifierSelection at level load and applies TowerBuffDefinitions to all
/// existing towers. Also listens for new towers placed mid-game so they receive
/// the same buffs immediately.
/// </summary>
public class ModifierBuffApplicator : MonoBehaviour
{
    public static ModifierBuffApplicator Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Call once after ModifierSelection is finalised (end of LevelManager.LoadLevel).</summary>
    public void ApplyAll()
    {
        foreach (var tower in FindObjectsByType<TowerInfo>(FindObjectsSortMode.None))
            ApplyToTower(tower);
    }

    /// <summary>Call from TowerFactory after a new tower is placed.</summary>
    public void ApplyToTower(TowerInfo tower)
    {
        if (tower == null || tower.isGhost) return;

        // ── Balance-type multipliers ──────────────────────────────────
        float physMult = ModifierSelection.GetFloat("PhysicalDamageMult");
        if (physMult > 0f && tower.balanceType == BalanceType.Physical)
            ApplyBuff(tower, "mod_physical_dmg", "+Phys Dmg", damageMult: physMult);

        float elMult = ModifierSelection.GetFloat("ElementalDamageMult");
        if (elMult > 0f && tower.balanceType == BalanceType.Elemental)
            ApplyBuff(tower, "mod_elemental_dmg", "+Elem Dmg", damageMult: elMult);

        float elSpdMult = ModifierSelection.GetFloat("ElementalSpdMult");
        if (elSpdMult > 0f && tower.balanceType == BalanceType.Elemental)
            ApplyBuff(tower, "mod_elemental_spd", "+Elem Spd", fireRateMult: elSpdMult);

        float arcaneSpdMult = ModifierSelection.GetFloat("ArcaneSpdMult");
        if (arcaneSpdMult > 0f && tower.balanceType == BalanceType.Arcane)
            ApplyBuff(tower, "mod_arcane_spd", "+Arcane Spd", fireRateMult: arcaneSpdMult);

        // ── Tower-specific multipliers ────────────────────────────────
        float basicDmgMult = ModifierSelection.GetFloat("BasicTowerDamageMult");
        if (basicDmgMult > 0f && tower.definitionId == "basic_tower")
            ApplyBuff(tower, "mod_basic_dmg", "+Basic Dmg", damageMult: basicDmgMult);

        float basicSpdMult = ModifierSelection.GetFloat("BasicTowerFireRateMult");
        if (basicSpdMult > 0f && tower.definitionId == "basic_tower")
            ApplyBuff(tower, "mod_basic_spd", "+Basic Spd", fireRateMult: basicSpdMult);

        // ── Full refund ───────────────────────────────────────────────
        if (ModifierSelection.HasEffect("FullRefund"))
            ApplyBuff(tower, "mod_full_refund", "Full Refund");

        // ── Global multipliers ────────────────────────────────────────
        float globalDmgMult = ModifierSelection.GetFloat("TowerDamageMult");
        if (globalDmgMult > 0f)
            ApplyBuff(tower, "mod_global_dmg", "+All Dmg", damageMult: globalDmgMult);

        float globalSpdMult = ModifierSelection.GetFloat("TowerSpeedMult");
        if (globalSpdMult > 0f)
            ApplyBuff(tower, "mod_global_spd", "+All Spd", fireRateMult: globalSpdMult);

        float globalRngMult = ModifierSelection.GetFloat("TowerRangeMult");
        if (globalRngMult > 0f)
            ApplyBuff(tower, "mod_global_rng", "+All Rng", rangeMult: globalRngMult);
    }

    static void ApplyBuff(TowerInfo tower, string id, string displayName,
        float damageMult = 0f, float fireRateMult = 0f, float rangeMult = 0f)
    {
        var handler = TowerBuffHandler.GetOrAdd(tower.gameObject);
        if (handler.HasBuff(id)) return;
        handler.ApplyBuff(new TowerBuffDefinition
        {
            id           = id,
            displayName  = displayName,
            damageMult   = damageMult,
            fireRateMult = fireRateMult,
            rangeMult    = rangeMult,
        });
    }
}
