using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Research tower with cluster synergy.
///
/// — A single global spawn timer gives one orb to one empty research tower
///   per interval. Towers fill in player-set priority order (1 first, 4 last;
///   ties picked at random). Max 1 orb each, so 3 towers take 3 intervals
///   to all fill.
/// — Click a tower to collect its orb. Payout scales with how many adjacent
///   research towers currently hold an orb:
///     0 neighbors → 1 tech, 1 → 3, 2 → 7, 3 → 15.
/// — adjacencyRadius is sized so three towers placed side by side are all
///   within range of one another.
/// </summary>
public class ResearchTower : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register()
    {
        ComponentRegistry.Register("research_tower", typeof(ResearchTower));
        Instances.Clear();
        _nextSpawnTime = -1f;
    }

    /// <summary>World-space click radius for collecting the orb (also read by CollectClickGuard).</summary>
    public const float CollectRadius = 0.9f;

    public const int MaxPriority = 4;

    /// <summary>Fill priority (1 = filled first, 4 = last). Set by the player via the info panel.</summary>
    public int priority = 1;

    static readonly Vector2 OrbSlot = new Vector2(0f, 0.60f);

    // ── Data (set from towers.json via Initialize) ────────────────────
    public float  orbInterval     = 12f;
    public float  minInterval     = 4f;
    public float  arcaneScale     = 0.12f;
    public float  adjacencyRadius = 1.6f;
    public int[]  neighborValues  = { 1, 3, 7, 15 };
    public string orbSpritePath   = "";
    public string orbSpriteSheet  = "";
    public int    orbSpriteIndex  = 1;

    // ── Global spawn coordination ─────────────────────────────────────
    static readonly List<ResearchTower> Instances = new();
    static float _nextSpawnTime = -1f;

    // ── Runtime ───────────────────────────────────────────────────────
    private GameObject _orb;
    private Sprite[]   _orbSheet;

    public bool HasOrb => _orb != null;

    // ── IFactoryInitializable ─────────────────────────────────────────

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<ResearchTowerData>(dataJson);
        if (d == null) return;
        if (d.orbInterval     > 0f)                  orbInterval     = d.orbInterval;
        if (d.minInterval     > 0f)                  minInterval     = d.minInterval;
        if (d.arcaneScale     > 0f)                  arcaneScale     = d.arcaneScale;
        if (d.adjacencyRadius > 0f)                  adjacencyRadius = d.adjacencyRadius;
        if (d.neighborValues != null && d.neighborValues.Length > 0)
                                                     neighborValues  = d.neighborValues;
        if (!string.IsNullOrEmpty(d.orbSpritePath))  orbSpritePath   = d.orbSpritePath;
        if (!string.IsNullOrEmpty(d.orbSpriteSheet)) orbSpriteSheet  = d.orbSpriteSheet;
        if (d.orbSpriteIndex >= 0)                   orbSpriteIndex  = d.orbSpriteIndex;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start()
    {
        if (!string.IsNullOrEmpty(orbSpritePath))
            _orbSheet = new[] { Resources.Load<Sprite>(orbSpritePath) };
        else if (!string.IsNullOrEmpty(orbSpriteSheet))
            _orbSheet = Resources.LoadAll<Sprite>(orbSpriteSheet);
    }

    void OnEnable()
    {
        Instances.Add(this);
        if (_nextSpawnTime < 0f)
            _nextSpawnTime = Time.time + Random.Range(1f, orbInterval * 0.4f);
    }

    void OnDisable()
    {
        Instances.Remove(this);
        if (Instances.Count == 0) _nextSpawnTime = -1f;
    }

    void OnDestroy()
    {
        if (_orb != null) Destroy(_orb);
    }

    void Update()
    {
        // Only the leader instance advances the shared spawn timer
        if (Instances.Count > 0 && Instances[0] == this && Time.time >= _nextSpawnTime)
        {
            float arcane   = BalanceManager.Instance != null ? BalanceManager.Instance.Arcane : 0f;
            float interval = Mathf.Max(minInterval, orbInterval * Mathf.Pow(0.99f, arcane));
            _nextSpawnTime = Time.time + interval;

            NextTowerToFill()?.SpawnOrb();
        }

        HandleCollectClick();
    }

    /// <summary>Empty tower with the lowest priority number; ties picked at random.</summary>
    static ResearchTower NextTowerToFill()
    {
        int bestPrio = int.MaxValue;
        var candidates = new List<ResearchTower>();
        foreach (var t in Instances)
        {
            if (t.HasOrb) continue;
            if (t.priority < bestPrio) { bestPrio = t.priority; candidates.Clear(); }
            if (t.priority == bestPrio) candidates.Add(t);
        }
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    // ── Orb spawning ──────────────────────────────────────────────────

    void SpawnOrb()
    {
        if (_orb != null) return;

        _orb = new GameObject("ResearchOrb");
        _orb.transform.position   = transform.position + (Vector3)OrbSlot;
        _orb.transform.localScale = Vector3.one * .85f;

        var sr              = _orb.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Towers";
        sr.sortingOrder     = 10;  // above the tower sprite (which is order 0)
        sr.color            = new Color(0.35f, 1f, 0.45f, 1f);

        int idx = string.IsNullOrEmpty(orbSpritePath) ? orbSpriteIndex : 0;
        if (_orbSheet != null && idx < _orbSheet.Length)
            sr.sprite = _orbSheet[idx];

        _orb.AddComponent<IncomeOrb>();
    }

    // ── Collection ────────────────────────────────────────────────────

    void HandleCollectClick()
    {
        if (!HasOrb || !Input.GetMouseButtonDown(0)) return;

        if (TowerPlacer.Instance != null && TowerPlacer.Instance.IsPlacing) return;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        // Direct world-position check — bypasses Physics2D raycast so nearby
        // tower range colliders can't intercept the click
        Vector2 clickWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float myDist = Vector2.Distance(clickWorld, (Vector2)transform.position);
        if (myDist > CollectRadius) return;

        // Clustered towers can sit closer together than CollectRadius, so a
        // click may land in range of several — only the nearest one collects
        foreach (var t in Instances)
            if (t != this && t.HasOrb &&
                Vector2.Distance(clickWorld, (Vector2)t.transform.position) < myDist)
                return;

        Collect();
    }

    /// <summary>Adjacent research towers (within adjacencyRadius) that currently hold an orb.</summary>
    public int OrbNeighborCount()
    {
        int count = 0;
        foreach (var t in Instances)
            if (t != this && t.HasOrb &&
                Vector2.Distance(transform.position, t.transform.position) <= adjacencyRadius)
                count++;
        return count;
    }

    public int CurrentPayout()
    {
        int idx = Mathf.Clamp(OrbNeighborCount(), 0, neighborValues.Length - 1);
        return neighborValues[idx];
    }

    public void Collect()
    {
        if (!HasOrb) return;

        int tech = CurrentPayout();
        TechManager.Instance?.AddTech(tech);
        FloatingText.Spawn($"+{tech} sci", transform.position + Vector3.up * 0.9f, new Color(0.35f, 1f, 0.5f));

        var orbComp = _orb.GetComponent<IncomeOrb>();
        if (orbComp != null) orbComp.Pop();
        else Destroy(_orb);

        _orb = null;
    }
}

[System.Serializable]
public class ResearchTowerData
{
    public float  orbInterval     = 12f;
    public float  minInterval     = 4f;
    public float  arcaneScale     = 0.12f;
    public float  adjacencyRadius = 1.6f;
    public int[]  neighborValues  = { 1, 3, 7, 15 };
    public string orbSpritePath   = "";
    public string orbSpriteSheet  = "";
    public int    orbSpriteIndex  = 1;
}
