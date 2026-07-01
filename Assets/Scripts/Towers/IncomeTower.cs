using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PvZ-style income tower.
///
/// — Every orbInterval seconds a new orb appears above one of 4 slots.
/// — Max 4 orbs. No more spawn until the player collects.
/// — Click the tower body to collect all orbs at once.
/// — Payout scales with how many you've saved up (Fibonacci-style):
///     1 orb  →  1 gold
///     2 orbs →  3 gold
///     3 orbs →  5 gold
///     4 orbs →  8 gold
/// </summary>
public class IncomeTower : MonoBehaviour, IFactoryInitializable
{
    // ── Self-registration ─────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("income_tower", typeof(IncomeTower));

    // ── Full payout per tier when all orb slots are filled ───────────
    static readonly int[] FullPayout = { 0, 1, 3, 6, 10, 15 };  // index = tier

    // ── Orb slot layouts per tier (tier 1 = index 0) ─────────────────
    static readonly Vector2[][] TierSlots =
    {
        new[] { new Vector2( 0.00f,  0.60f) },                                                                                                                          // T1: 1 orb center
        new[] { new Vector2(-0.25f,  0.60f), new Vector2( 0.25f,  0.60f) },                                                                                            // T2: 2 orbs side by side
        new[] { new Vector2(-0.25f,  0.70f), new Vector2( 0.25f,  0.70f), new Vector2( 0.00f,  0.48f) },                                                               // T3: triangle
        new[] { new Vector2(-0.25f,  0.72f), new Vector2( 0.25f,  0.72f), new Vector2(-0.25f,  0.48f), new Vector2( 0.25f,  0.48f) },                                  // T4: 2×2 grid
        new[] { new Vector2(-0.25f,  0.72f), new Vector2( 0.25f,  0.72f), new Vector2(-0.25f,  0.48f), new Vector2( 0.25f,  0.48f), new Vector2( 0.00f,  0.60f) },    // T5: 4 corners + center
    };

    // ── Data (set from towers.json via Initialize) ────────────────────
    public float  orbInterval     = 8f;
    public float  minInterval     = 2f;
    public float  elementalScale  = 0.15f;
    public string orbSpritePath   = "";
    public string orbSpriteSheet  = "";
    public int    orbSpriteIndex  = 1;

    // ── Runtime ───────────────────────────────────────────────────────
    private ResourceManagerScript _rm;
    private readonly List<GameObject> _orbs = new();
    private Sprite[] _orbSheet;

    public int OrbCount => _orbs.Count;

    // ── IFactoryInitializable ─────────────────────────────────────────

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<IncomeTowerData>(dataJson);
        if (d == null) return;
        if (d.orbInterval    > 0f)                     orbInterval    = d.orbInterval;
        if (d.minInterval    > 0f)                     minInterval    = d.minInterval;
        if (d.elementalScale > 0f)                     elementalScale = d.elementalScale;
        if (!string.IsNullOrEmpty(d.orbSpritePath))   orbSpritePath  = d.orbSpritePath;
        if (!string.IsNullOrEmpty(d.orbSpriteSheet)) orbSpriteSheet = d.orbSpriteSheet;
        if (d.orbSpriteIndex >= 0)                   orbSpriteIndex = d.orbSpriteIndex;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start()
    {
        _rm = ResourceManagerScript.Instance;
        if (!string.IsNullOrEmpty(orbSpritePath))
            _orbSheet = new[] { Resources.Load<Sprite>(orbSpritePath) };
        else if (!string.IsNullOrEmpty(orbSpriteSheet))
            _orbSheet = Resources.LoadAll<Sprite>(orbSpriteSheet);

        StartCoroutine(OrbSpawnLoop());
    }

    int CurrentTier => GetComponent<TowerInfo>()?.Tier ?? 1;
    Vector2[] CurrentSlots => TierSlots[Mathf.Clamp(CurrentTier, 1, TierSlots.Length) - 1];

    IEnumerator OrbSpawnLoop()
    {
        yield return new WaitForSeconds(Random.Range(0f, orbInterval * 0.5f));

        while (true)
        {
            float elemental = BalanceManager.Instance != null ? BalanceManager.Instance.Elemental : 0f;
            float interval  = Mathf.Max(minInterval, orbInterval * Mathf.Pow(0.99f, elemental));
            yield return new WaitForSeconds(interval);
            if (_orbs.Count < CurrentSlots.Length)
                SpawnOrb();
        }
    }

    // ── Orb spawning ──────────────────────────────────────────────────

    void SpawnOrb()
    {
        int slot         = _orbs.Count;
        var slots        = CurrentSlots;
        if (slot >= slots.Length) return;
        Vector3 worldPos = transform.position + (Vector3)slots[slot];

        var go = new GameObject($"IncomeOrb_{slot}");
        go.transform.position   = worldPos;
        go.transform.localScale = Vector3.one * .85f;

        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Towers";
        sr.sortingOrder     = 10;  // above the tower sprite (which is order 0)
        sr.color            = Color.white;

        int idx = string.IsNullOrEmpty(orbSpritePath) ? orbSpriteIndex : 0;
        if (_orbSheet != null && idx < _orbSheet.Length)
            sr.sprite = _orbSheet[idx];

        go.AddComponent<IncomeOrb>();

        _orbs.Add(go);
    }

    // ── Collection (player clicks the tower) ─────────────────────────

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        // Don't collect while in tower-placement mode
        if (TowerPlacer.Instance != null && TowerPlacer.Instance.IsPlacing) return;

        // Don't collect if the click landed on a UI element
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        // Direct world-position check — bypasses Physics2D raycast so nearby
        // tower range colliders can't intercept the click
        Vector2 clickWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (Vector2.Distance(clickWorld, (Vector2)transform.position) > 0.9f) return;

        Collect();
    }

    public void Collect()
    {
        int orbCount = _orbs.Count;
        if (orbCount == 0) return;

        int tier      = CurrentTier;
        int maxOrbs   = CurrentSlots.Length;
        int fullGold  = tier < FullPayout.Length ? FullPayout[tier] : FullPayout[FullPayout.Length - 1];
        int gold      = Mathf.Max(1, Mathf.RoundToInt((float)orbCount / maxOrbs * fullGold));

        if (_rm != null) _rm.ChangeResourceOne(gold);
        FloatingText.Spawn($"+{gold}g", transform.position + Vector3.up * 0.9f, new Color(1f, 0.85f, 0.25f));

        foreach (var orb in _orbs)
        {
            if (orb == null) continue;
            var orbComp = orb.GetComponent<IncomeOrb>();
            if (orbComp != null) orbComp.Pop();
            else Destroy(orb);
        }
        _orbs.Clear();
    }

    void OnDestroy()
    {
        foreach (var orb in _orbs)
            if (orb != null) Destroy(orb);
        _orbs.Clear();
    }
}

[System.Serializable]
public class IncomeTowerData
{
    public float  orbInterval    = 8f;
    public float  minInterval    = 2f;
    public float  elementalScale = 0.15f;
    public string orbSpritePath  = "";
    public string orbSpriteSheet = "";
    public int    orbSpriteIndex = 1;
}
