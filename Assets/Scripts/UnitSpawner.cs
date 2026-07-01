using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One spawn group within a wave.
/// startTime is seconds after the wave button is pressed.
/// </summary>
[Serializable]
public class WaveEntry
{
    public string     unitDefinitionId = "basic_enemy";
    public int        count            = 5;
    public float      spawnInterval    = 1.0f;
    public float      startTime        = 0f;

    [Tooltip("-1 = all spawners, 0/1/2 = specific spawner by pathIndex")]
    public int        spawnerIndex = 0;

    /// <summary>Controls when kills from this spawn group produce a BountyDrop.</summary>
    public DropConfig dropConfig = new DropConfig();

    // ── Computed helpers ──────────────────────────────────────────────
    public float EndTime  => count <= 1 ? startTime : startTime + (count - 1) * spawnInterval;
    public float Duration => EndTime - startTime;
}

/// <summary>
/// Spawn point that executes a single WaveEntry group on demand.
/// WaveManager drives timing; UnitSpawner just fires units.
/// </summary>
public class UnitSpawner : MonoBehaviour
{
    [Header("References")]
    public PathNode headNode;

    [Tooltip("Index used to match WaveEntry.spawnerIndex")]
    public int pathIndex = 0;

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start() => WaveManager.Instance?.RegisterSpawner(this);

    // ── Public API ────────────────────────────────────────────────────

    public IEnumerator SpawnGroup(WaveEntry entry, int waveNumber)
    {
        for (int i = 0; i < entry.count; i++)
        {
            SpawnUnit(entry, waveNumber, i);
            if (i < entry.count - 1)
                yield return new WaitForSeconds(entry.spawnInterval);
        }
    }

    // ── Internal ──────────────────────────────────────────────────────

    void SpawnUnit(WaveEntry entry, int waveNumber, int spawnIndex)
    {
        if (UnitFactory.Instance == null) return;

        var unitGO = UnitFactory.Instance.Build(entry.unitDefinitionId, transform.position);
        if (unitGO == null) return;

        var upc = unitGO.GetComponent<UnitParentClass>();
        if (upc != null && waveNumber > 1)
        {
            float healthMult = Mathf.Pow(1.08f, waveNumber - 1);
            upc.lifeMax     *= healthMult;
            upc.lifeCurrent  = upc.lifeMax;
        }

        var unit = unitGO.GetComponent<UnitManager>();
        if (unit != null)
        {
            unit.SpawnDropConfig = entry.dropConfig ?? new DropConfig();
            unit.SpawnIndex      = spawnIndex;

            if (PathGraph.Instance != null)
            {
                var head = headNode ?? GetNearestHead();
                if (head != null)
                    unit.AssignRoute(PathGraph.Instance.BuildRoute(head));
                else
                    Debug.LogWarning("[UnitSpawner] No head PathNode — unit will not move.");
            }

            WaveManager.Instance?.RegisterUnit(unit);
        }
    }

    PathNode GetNearestHead()
    {
        if (PathGraph.Instance == null) return null;
        var heads = PathGraph.Instance.GetHeads();
        if (heads.Count == 0) return null;
        if (heads.Count == 1) return heads[0];

        PathNode best = null; float bestDist = float.MaxValue;
        foreach (var h in heads)
        {
            float d = ((Vector2)transform.position - h.Position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = h; }
        }
        return best;
    }
}
