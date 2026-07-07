using UnityEngine;

/// <summary>
/// When this unit dies it spawns 4 smaller "splitter_mini" units that continue along the path.
/// Attach via units.json components entry "splitter_on_death".
/// </summary>
public class SplitterOnDeath : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("splitter_on_death", typeof(SplitterOnDeath));

    public string miniUnitId = "splitter_mini";
    public int    miniCount  = 4;

    private UnitManager _unit;
    private bool        _split;

    public void Initialize(string dataJson)
    {
        Debug.Log($"[SplitterDebug] Initialize on '{name}' — raw data: {(string.IsNullOrEmpty(dataJson) ? "<empty>" : dataJson)}");
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<SplitterData>(dataJson);
        if (d == null) return;
        if (!string.IsNullOrEmpty(d.miniUnitId)) miniUnitId = d.miniUnitId;
        if (d.miniCount > 0) miniCount = d.miniCount;
        Debug.Log($"[SplitterDebug] Initialized: miniUnitId='{miniUnitId}', miniCount={miniCount}");
    }

    void Start()
    {
        _unit = GetComponent<UnitManager>();
        // Die() destroys no-death-anim units the same frame, so Update polling
        // never sees the death — the synchronous event is the reliable hook.
        if (_unit != null) _unit.OnDied += HandleDeath;
    }

    void OnDestroy()
    {
        if (_unit != null) _unit.OnDied -= HandleDeath;
    }

    void HandleDeath()
    {
        if (_split) return;
        _split = true;
        Debug.Log($"[SplitterDebug] '{name}' died at {transform.position} — spawning {miniCount}x '{miniUnitId}'");
        SpawnMinis();
    }

    // Fallback for deaths that skip Die() — harmless double-check, _split guards
    void Update()
    {
        if (_split || _unit == null || _unit.isAlive) return;
        HandleDeath();
    }

    void SpawnMinis()
    {
        if (UnitFactory.Instance == null)
        {
            Debug.LogWarning("[SplitterOnDeath] UnitFactory missing — minis not spawned.");
            return;
        }

        // Route progress comes from this unit's own follower — no PathGraph needed
        var myFollower = GetComponent<RouteFollower>();
        if (myFollower == null || !myFollower.HasRoute)
            Debug.LogWarning("[SplitterOnDeath] Dying splitter has no route — minis will spawn but not move.");

        int spawned = 0;
        for (int i = 0; i < miniCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-0.3f, 0.3f),
                Random.Range(-0.3f, 0.3f), 0f);

            var go = UnitFactory.Instance.Build(miniUnitId, transform.position + offset);
            if (go == null)
            {
                Debug.LogWarning($"[SplitterOnDeath] Factory failed to build '{miniUnitId}' — check units.json.");
                continue;
            }

            var unit = go.GetComponent<UnitManager>();
            if (unit == null) continue;

            // Same per-wave HP scaling the spawner applies to normal units
            int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : 1;
            if (wave > 1)
            {
                float healthMult = Mathf.Pow(1.08f, wave - 1);
                unit.lifeMax    *= healthMult;
                unit.lifeCurrent = unit.lifeMax;
            }

            // Slight speed spread so the pack never marches in perfect lockstep
            // (identical route + progress + speed renders 4 minis as one unit)
            unit.speedMax    *= Random.Range(0.90f, 1.10f);
            unit.speedCurrent = unit.speedMax;

            // Resume from the same point on the path, each mini trailing a bit further back
            if (myFollower != null && myFollower.HasRoute)
            {
                var follower = go.GetComponent<RouteFollower>();
                if (follower == null) follower = go.AddComponent<RouteFollower>();
                float progress = Mathf.Max(0f, myFollower.Progress - i * 0.02f);
                follower.StartRoute(myFollower.CurrentRoute, unit.speedMax, progress);
            }

            WaveManager.Instance?.RegisterUnit(unit);
            spawned++;
            Debug.Log($"[SplitterDebug] Mini {i + 1}/{miniCount}: '{go.name}' scale={go.transform.localScale.x:0.##} " +
                      $"hp={unit.lifeMax:0.#} speed={unit.speedMax:0.##} pos={go.transform.position}");
        }
        Debug.Log($"[SplitterDebug] SpawnMinis done — {spawned}/{miniCount} spawned.");
    }

    [System.Serializable]
    class SplitterData
    {
        public string miniUnitId;
        public int    miniCount;
    }
}
