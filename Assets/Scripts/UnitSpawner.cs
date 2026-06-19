using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data entry for one wave in this spawner's sequence.
/// Each wave spawns 'count' units of 'unitDefinitionId' with random spread.
/// </summary>
[Serializable]
public class WaveEntry
{
    [Tooltip("Must match an id in Resources/Definitions/units.json")]
    public string unitDefinitionId = "basic_enemy";

    [Tooltip("Number of units to spawn in this wave")]
    public int count = 3;

    [Tooltip("Random spread radius around spawn point (world units)")]
    public float spread = 0.5f;

    [Tooltip("Seconds between each unit in this wave")]
    public float spawnInterval = 0.4f;
}

public class UnitSpawner : MonoBehaviour
{
    [Header("References")]
    public RoundManager roundManager;

    [Tooltip("Head node this spawner feeds units into. Must be a PathGraph node with no incoming connections.")]
    public PathNode headNode;

    [Header("Waves")]
    [Tooltip("One entry per wave — spawned in order each round")]
    public List<WaveEntry> waves = new List<WaveEntry>();

    // ── Internal state ────────────────────────────────────────────────
    public int  spawnCount         = 0;
    public int  formationArrayIndex = -1;
    public bool isActive           = false;

    private float _timer;
    private float _coolDown;

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start()
    {
        roundManager = GameObject.FindGameObjectWithTag("RoundManager").GetComponent<RoundManager>();
        roundManager.spawners.Add(gameObject);
    }

    void Update()
    {
        if (formationArrayIndex == -1) return;

        isActive = formationArrayIndex < spawnCount;
        if (!isActive) return;

        if (_timer < _coolDown)
        {
            _timer += Time.deltaTime;
        }
        else if (formationArrayIndex < spawnCount)
        {
            Spawn();
            _timer = 0f;
        }
    }

    // ── Spawn ─────────────────────────────────────────────────────────

    void Spawn()
    {
        if (waves == null || waves.Count == 0) { formationArrayIndex++; return; }

        WaveEntry entry = waves[formationArrayIndex % waves.Count];
        _coolDown = entry.spawnInterval > 0f ? entry.spawnInterval : 0.4f;

        if (UnitFactory.Instance == null)
        {
            Debug.LogError("[UnitSpawner] UnitFactory not found in scene.");
            formationArrayIndex++;
            return;
        }

        if (PathGraph.Instance == null)
            Debug.LogWarning("[UnitSpawner] PathGraph not in scene — units will have no route.");

        for (int i = 0; i < entry.count; i++)
        {
            Vector3 offset = new Vector3(
                UnityEngine.Random.Range(-entry.spread, entry.spread),
                UnityEngine.Random.Range(-entry.spread, entry.spread),
                0f);

            Vector3 spawnPos = new Vector3(transform.position.x, transform.position.y, 0f) + offset;

            GameObject unitGO = UnitFactory.Instance.Build(entry.unitDefinitionId, spawnPos);
            if (unitGO == null) continue;

            // Build and assign route
            UnitManager unit = unitGO.GetComponent<UnitManager>();
            if (unit != null && PathGraph.Instance != null)
            {
                PathNode head = headNode != null ? headNode : GetNearestHead();
                if (head != null)
                    unit.AssignRoute(PathGraph.Instance.BuildRoute(head));
                else
                    Debug.LogWarning("[UnitSpawner] No head node found — unit will not move.");
            }

            roundManager.aliveEnemies.Add(unitGO);
        }

        formationArrayIndex++;
    }

    PathNode GetNearestHead()
    {
        var heads = PathGraph.Instance.GetHeads();
        if (heads.Count == 0) return null;
        if (heads.Count == 1) return heads[0];

        // Pick the head closest to this spawner
        PathNode best = null;
        float bestDist = float.MaxValue;
        foreach (var h in heads)
        {
            float d = ((Vector2)transform.position - h.Position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = h; }
        }
        return best;
    }
}
