using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data entry for one spawn group within a wave.
/// unitDefinitionId must match an id in Resources/Definitions/units.json.
/// </summary>
[Serializable]
public class WaveEntry
{
    [Tooltip("Must match an id in Resources/Definitions/units.json")]
    public string unitDefinitionId = "basic_enemy";

    [Tooltip("Number of units to spawn in this group")]
    public int count = 3;

    [Tooltip("Random spread radius around spawn point (world units)")]
    public float spread = 0.5f;

    [Tooltip("Seconds between each unit in this group")]
    public float spawnInterval = 0.4f;

    [Tooltip("Extra delay after this group finishes before the next group starts")]
    public float groupDelay = 1.5f;

    [Tooltip("-1 = all spawners, 0/1/2 = specific spawner by pathIndex")]
    public int spawnerIndex = -1;
}

/// <summary>
/// Spawns enemies when told to by WaveManager.
/// Self-registers with WaveManager.Instance on Start.
/// Call BeginWave() to start a new wave's spawn sequence.
/// </summary>
public class UnitSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Head node this spawner feeds units into.")]
    public PathNode headNode;

    [Tooltip("Index used to match WaveEntry.spawnerIndex (0=main, 1=upper, 2=lower).")]
    public int pathIndex = 0;

    // ── Internal state ────────────────────────────────────────────────
    [HideInInspector] public List<WaveEntry> waves = new List<WaveEntry>();

    private int   _formationIndex;
    private int   _subIndex;          // position within current WaveEntry
    private int   _spawnTarget;
    private float _timer;
    private float _coolDown;
    private bool  _running;
    private int   _waveNumber;        // used for difficulty scaling

    // ── Public state ──────────────────────────────────────────────────
    /// <summary>True while this spawner still has units left to emit this wave.</summary>
    public bool IsSpawning => _running && _formationIndex < _spawnTarget;

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start()
    {
        WaveManager.Instance?.RegisterSpawner(this);
    }

    void Update()
    {
        if (!IsSpawning) return;

        _timer += Time.deltaTime;
        if (_timer >= _coolDown)
        {
            _timer = 0f;
            Spawn();
        }
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Called by WaveManager at the start of each wave.
    /// Replaces the current spawn queue with the new group list.
    /// </summary>
    public void BeginWave(List<WaveEntry> groups, int waveNumber = 1)
    {
        waves           = groups ?? new List<WaveEntry>();
        _formationIndex = 0;
        _subIndex       = 0;
        _spawnTarget    = waves.Count;
        _timer          = 0f;
        _coolDown       = 0f;   // first unit spawns immediately
        _running        = waves.Count > 0;
        _waveNumber     = waveNumber;
    }

    // ── Spawn ─────────────────────────────────────────────────────────

    void Spawn()
    {
        if (_formationIndex >= waves.Count) { _running = false; return; }

        WaveEntry entry = waves[_formationIndex];
        _coolDown = entry.spawnInterval > 0f ? entry.spawnInterval : 0.4f;

        if (UnitFactory.Instance == null)
        {
            Debug.LogError("[UnitSpawner] UnitFactory not in scene.");
            _formationIndex++;
            return;
        }

        // Spawn ONE unit this tick
        SpawnUnit(entry);
        _subIndex++;

        // Advance to next WaveEntry when this one is exhausted
        if (_subIndex >= entry.count)
        {
            _formationIndex++;
            _subIndex = 0;
            // pause between groups
            if (_formationIndex < _spawnTarget)
                _coolDown = entry.groupDelay > 0f ? entry.groupDelay : 1.5f;
        }

        if (_formationIndex >= _spawnTarget)
            _running = false;
    }

    void SpawnUnit(WaveEntry entry)
    {
        Vector3 offset = new Vector3(
            UnityEngine.Random.Range(-entry.spread, entry.spread),
            UnityEngine.Random.Range(-entry.spread, entry.spread),
            0f);

        Vector3 spawnPos = transform.position + offset;
        spawnPos.z = 0f;

        GameObject unitGO = UnitFactory.Instance.Build(entry.unitDefinitionId, spawnPos);
        if (unitGO == null) return;

        // Scale stats with wave number — +8% health and +3% speed compounding per wave
        var upc = unitGO.GetComponent<UnitParentClass>();
        if (upc != null && _waveNumber > 1)
        {
            float healthMult = Mathf.Pow(1.08f, _waveNumber - 1);
            upc.lifeMax     *= healthMult;
            upc.lifeCurrent  = upc.lifeMax;
        }

        UnitManager unit = unitGO.GetComponent<UnitManager>();
        if (unit != null && PathGraph.Instance != null)
        {
            PathNode head = headNode != null ? headNode : GetNearestHead();
            if (head != null)
                unit.AssignRoute(PathGraph.Instance.BuildRoute(head));
            else
                Debug.LogWarning("[UnitSpawner] No head PathNode found — unit will not move.");
        }

        if (unit != null)
            WaveManager.Instance?.RegisterUnit(unit);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    PathNode GetNearestHead()
    {
        if (PathGraph.Instance == null) return null;
        var heads = PathGraph.Instance.GetHeads();
        if (heads.Count == 0) return null;
        if (heads.Count == 1) return heads[0];

        PathNode best     = null;
        float    bestDist = float.MaxValue;
        foreach (var h in heads)
        {
            float d = ((Vector2)transform.position - h.Position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = h; }
        }
        return best;
    }
}
