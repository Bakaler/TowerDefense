using System.Collections;
using UnityEngine;

/// <summary>
/// Elemental support tower. Scans its range each tick for bounty drops,
/// income towers with full orbs (4), and fully-saturated research clusters.
/// Grabs one target at a time, draws a light-tether LineRenderer, then
/// collects after a short reach delay.
/// </summary>
public class CollectorTower : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("collector_tower", typeof(CollectorTower));

    // ── Config (overridable from towers.json) ─────────────────────────
    public float range       = 3.5f;
    public float reachTime   = 0.35f;   // seconds to animate the tether before collecting
    public float cooldown    = 1.2f;    // seconds between grabs

    // ── Runtime ───────────────────────────────────────────────────────
    private LineRenderer _line;
    private bool         _busy;

    // ── IFactoryInitializable ─────────────────────────────────────────
    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<CollectorTowerData>(dataJson);
        if (d == null) return;
        if (d.range     > 0f) range     = d.range;
        if (d.reachTime > 0f) reachTime = d.reachTime;
        if (d.cooldown  > 0f) cooldown  = d.cooldown;
    }

    void Start()
    {
        BuildTether();
        StartCoroutine(CollectLoop());
    }

    // ── Main loop ─────────────────────────────────────────────────────

    IEnumerator CollectLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(cooldown);
            if (_busy) continue;

            // Priority 1: full income towers (4 orbs)
            var incomeTower = FindFullIncomeTower();
            if (incomeTower != null)
            {
                yield return StartCoroutine(GrabRoutine(incomeTower.transform.position, () =>
                {
                    incomeTower.Collect();
                }));
                continue;
            }

            // Priority 2: nearest bounty drop in range
            var drop = FindNearestBountyDrop();
            if (drop != null)
            {
                yield return StartCoroutine(GrabRoutine(drop.transform.position, () =>
                {
                    if (drop != null) drop.Collect();
                }));
                continue;
            }

            // Priority 3: research towers — only once every one in range holds
            // an orb, and then highest payout first, so the collector never
            // spoils the adjacency bonus by grabbing orbs early
            var research = FindBestSaturatedResearchTower();
            if (research != null)
            {
                yield return StartCoroutine(GrabRoutine(research.transform.position, () =>
                {
                    if (research != null) research.Collect();
                }));
            }
        }
    }

    IEnumerator GrabRoutine(Vector3 target, System.Action onCollect)
    {
        _busy = true;
        SetTether(transform.position, target, true);

        float elapsed = 0f;
        while (elapsed < reachTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / reachTime;
            // Animate tether tip traveling toward target
            Vector3 tip = Vector3.Lerp(transform.position, target, t);
            SetTether(transform.position, tip, true);
            yield return null;
        }

        onCollect?.Invoke();
        SetTether(transform.position, transform.position, false);
        _busy = false;
    }

    // ── Scanning ──────────────────────────────────────────────────────

    IncomeTower FindFullIncomeTower()
    {
        var towers = FindObjectsByType<IncomeTower>(FindObjectsSortMode.None);
        IncomeTower best = null;
        float bestDist   = float.MaxValue;
        foreach (var t in towers)
        {
            float d = Vector2.Distance(transform.position, t.transform.position);
            if (d <= range && t.OrbCount >= 4 && d < bestDist)
            { best = t; bestDist = d; }
        }
        return best;
    }

    ResearchTower FindBestSaturatedResearchTower()
    {
        var towers = FindObjectsByType<ResearchTower>(FindObjectsSortMode.None);
        ResearchTower best = null;
        int bestPayout     = 0;
        foreach (var t in towers)
        {
            if (Vector2.Distance(transform.position, t.transform.position) > range) continue;
            if (!t.HasOrb) return null;   // wait until every research tower in range is full
            int payout = t.CurrentPayout();
            if (best == null || payout > bestPayout)
            { best = t; bestPayout = payout; }
        }
        return best;
    }

    BountyDrop FindNearestBountyDrop()
    {
        var drops = FindObjectsByType<BountyDrop>(FindObjectsSortMode.None);
        BountyDrop best = null;
        float bestDist  = float.MaxValue;
        foreach (var drop in drops)
        {
            float d = Vector2.Distance(transform.position, drop.transform.position);
            if (d <= range && d < bestDist)
            { best = drop; bestDist = d; }
        }
        return best;
    }

    // ── Tether visual ─────────────────────────────────────────────────

    void BuildTether()
    {
        _line = gameObject.AddComponent<LineRenderer>();
        _line.positionCount  = 2;
        _line.startWidth     = 0.04f;
        _line.endWidth       = 0.02f;
        _line.useWorldSpace  = true;
        _line.sortingLayerName = "Units";
        _line.sortingOrder   = 8;

        var mat = RuntimeMaterials.SpriteDefault;
        _line.sharedMaterial = mat;
        _line.startColor = new Color(0.5f, 0.9f, 1f, 0.9f);
        _line.endColor   = new Color(1f,   1f,  1f, 0.3f);
        _line.enabled    = false;
    }

    void SetTether(Vector3 from, Vector3 to, bool visible)
    {
        if (_line == null) return;
        _line.enabled = visible;
        if (!visible) return;
        _line.SetPosition(0, from);
        _line.SetPosition(1, to);
    }
}

[System.Serializable]
public class CollectorTowerData
{
    public float range     = 3.5f;
    public float reachTime = 0.35f;
    public float cooldown  = 1.2f;
}
