using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A clickable world pickup spawned on enemy kill.
/// Uses the same orb art as IncomeTower. Player clicks to collect;
/// CollectorTower can call Collect() directly.
/// </summary>
public class BountyDrop : MonoBehaviour
{
    public int   goldValue   = 1;
    public float clickRadius = 0.4f;   // world-space click radius

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (TowerPlacer.Instance != null && TowerPlacer.Instance.IsPlacing) return;

        Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (Vector2.Distance(mouse, transform.position) <= clickRadius)
            Collect();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, clickRadius);
    }

    public void Collect()
    {
        var orb = GetComponent<IncomeOrb>();
        if (orb != null) orb.Pop();

        var rm = FindFirstObjectByType<ResourceManagerScript>();
        if (rm != null) rm.ChangeResourceOne(goldValue);
        FloatingText.Spawn($"+{goldValue}g", transform.position, new Color(1f, 0.85f, 0.25f));

        // IncomeOrb.Pop() destroys itself; destroy GO if no orb component
        if (orb == null) Destroy(gameObject);
    }

    // ── Factory ───────────────────────────────────────────────────────

    /// <summary>
    /// Evaluate the unit's spawn group DropConfig and spawn a bounty if warranted.
    /// </summary>
    public static void TrySpawn(Vector3 worldPos, UnitManager unit = null)
    {
        var cfg        = unit?.SpawnDropConfig;
        int spawnIndex = unit?.SpawnIndex ?? 0;

        if (EvaluateDrop(cfg, spawnIndex))
            Spawn(worldPos, 1);
    }

    static bool EvaluateDrop(DropConfig cfg, int killIndex)
    {
        if (cfg == null || cfg.mode == "chance")
            return RollBalanceChance();

        switch (cfg.mode)
        {
            case "always":
                return true;

            case "never":
                return false;

            case "first_only":
                return killIndex == 0;

            case "alternating":
                return killIndex % 2 == 0 ? true : RollFallback(cfg);

            case "pattern":
                if (cfg.pattern == null || cfg.pattern.Length == 0)
                    return RollBalanceChance();

                int idx = cfg.repeat
                    ? killIndex % cfg.pattern.Length
                    : killIndex;

                if (idx >= cfg.pattern.Length)
                    return RollFallback(cfg);

                return cfg.pattern[idx] switch
                {
                    1 => true,
                    0 => false,
                    _ => RollBalanceChance()   // 2 or any other value = chance
                };

            default:
                return RollBalanceChance();
        }
    }

    static bool RollFallback(DropConfig cfg) =>
        cfg.fallbackChance >= 0f ? Random.value <= cfg.fallbackChance : RollBalanceChance();

    static bool RollBalanceChance()
    {
        float physical = BalanceManager.Instance != null ? BalanceManager.Instance.Physical : 0f;
        return Random.value <= 0.15f + physical * 0.0025f;
    }

    public static BountyDrop Spawn(Vector3 worldPos, int value)
    {
        var go = new GameObject("BountyDrop");
        go.transform.position   = worldPos;
        go.transform.localScale = Vector3.one * .85f;

        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Units";
        sr.sortingOrder     = 5;
        sr.color            = Color.white;

        var orb = Resources.Load<Sprite>("Art/resource_1");
        if (orb != null) sr.sprite = orb;

        go.AddComponent<IncomeOrb>();   // handles bob + pop animation

        var drop       = go.AddComponent<BountyDrop>();
        drop.goldValue = value;
        return drop;
    }
}
