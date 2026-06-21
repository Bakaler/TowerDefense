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

    // ── Payout table (index = orb count) ─────────────────────────────
    static readonly int[] Payout = { 0, 1, 3, 5, 8 };

    // ── Orb slot offsets in world space (relative to tower center) ───
    // Arranged as a 2x2 grid sitting just above the tower sprite
    static readonly Vector2[] SlotOffsets =
    {
        new Vector2(-0.22f, 0.40f),
        new Vector2( 0.22f, 0.40f),
        new Vector2(-0.22f, 0.72f),
        new Vector2( 0.22f, 0.72f),
    };

    // ── Data (set from towers.json via Initialize) ────────────────────
    public float  orbInterval     = 8f;
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
        if (!string.IsNullOrEmpty(d.orbSpriteSheet))   orbSpriteSheet = d.orbSpriteSheet;
        if (d.orbSpriteIndex >= 0)                     orbSpriteIndex = d.orbSpriteIndex;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start()
    {
        _rm = FindFirstObjectByType<ResourceManagerScript>();
        if (!string.IsNullOrEmpty(orbSpriteSheet))
            _orbSheet = Resources.LoadAll<Sprite>(orbSpriteSheet);

        StartCoroutine(OrbSpawnLoop());
    }

    IEnumerator OrbSpawnLoop()
    {
        // Small random stagger so multiple income towers don't all tick together
        yield return new WaitForSeconds(Random.Range(0f, orbInterval * 0.5f));

        while (true)
        {
            yield return new WaitForSeconds(orbInterval);
            if (_orbs.Count < SlotOffsets.Length)
                SpawnOrb();
        }
    }

    // ── Orb spawning ──────────────────────────────────────────────────

    void SpawnOrb()
    {
        int slot       = _orbs.Count;
        Vector3 worldPos = transform.position + (Vector3)SlotOffsets[slot];

        var go = new GameObject($"IncomeOrb_{slot}");
        go.transform.position   = worldPos;
        go.transform.localScale = Vector3.one * 0.65f;

        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Towers";
        sr.sortingOrder     = 10;  // above the tower sprite (which is order 0)
        sr.color            = Color.white;

        if (_orbSheet != null && orbSpriteIndex < _orbSheet.Length)
            sr.sprite = _orbSheet[orbSpriteIndex];

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
        int count = Mathf.Clamp(_orbs.Count, 0, Payout.Length - 1);
        if (count == 0) return;

        int gold = Payout[count];

        if (_rm != null)
            _rm.ChangeResourceOne(gold);

        // Trigger pop animation on each orb — they self-destruct when done
        foreach (var orb in _orbs)
        {
            if (orb == null) continue;
            var orbComp = orb.GetComponent<IncomeOrb>();
            if (orbComp != null) orbComp.Pop();
            else Destroy(orb);
        }
        _orbs.Clear();

        Debug.Log($"[IncomeTower] Collected {count} orb(s) → +{gold} gold");
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
    public string orbSpriteSheet = "";
    public int    orbSpriteIndex = 1;
}
