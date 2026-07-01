using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns wave flow, alive-unit tracking, win/lose state.
/// Each wave's groups are scheduled by their startTime — multiple groups can overlap.
/// </summary>
public class WaveManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────
    public static WaveManager Instance { get; private set; }

    // ── Public state ──────────────────────────────────────────────────
    public int  CurrentWave  { get; private set; }
    public int  TotalWaves   { get; private set; }
    public bool IsWaveActive { get; private set; }
    public bool IsVictory    { get; private set; }
    public bool IsGameOver   { get; private set; }

    // ── Auto-start ────────────────────────────────────────────────────
    public const float AutoStartDelay  = 5f;
    public float AutoStartCountdown    { get; set; } = -1f;
    public bool  IsCountingDown        => AutoStartCountdown >= 0f;

    // ── Per-wave modifier flags ───────────────────────────────────────
    public static bool ForgiveNextLeak { get; set; }

    // ── Private ───────────────────────────────────────────────────────
    private List<WaveDefinition>  _waveDefs    = new();
    private List<UnitSpawner>     _spawners    = new();
    private List<UnitManager>     _aliveUnits  = new();
    private LogicManager          _logic;
    private ResourceManagerScript _resources;
    private int                   _activeSpawnGroups;
    private bool                  _waveSchedulingDone;

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _logic     = LogicManager.Instance;
        _resources = ResourceManagerScript.Instance;
    }

    void Update()
    {
        if (IsGameOver || IsVictory) return;

        if (Input.GetKeyDown(KeyCode.Space) && CanStartWave)
        {
            AutoStartCountdown = -1f;
            StartNextWave();
        }

        if (IsWaveActive)
            CheckWaveClear();
        else if (IsCountingDown)
        {
            AutoStartCountdown -= Time.deltaTime;
            if (AutoStartCountdown <= 0f) { AutoStartCountdown = -1f; StartNextWave(); }
        }
    }

    // ── Wave scheduling ───────────────────────────────────────────────

    public bool CanStartWave =>
        !IsWaveActive && !IsVictory && !IsGameOver && CurrentWave < TotalWaves;

    public void StartNextWave()
    {
        if (!CanStartWave) return;
        CurrentWave++;
        IsWaveActive          = true;
        _activeSpawnGroups    = 0;
        _waveSchedulingDone   = false;
        var def               = _waveDefs[CurrentWave - 1];
        StartCoroutine(RunWave(def));
        Debug.Log($"[WaveManager] Wave {CurrentWave}/{TotalWaves} started.");
    }

    IEnumerator RunWave(WaveDefinition def)
    {
        // Sort groups by startTime so we can walk them in order
        var groups = new List<WaveEntry>(def.groups);
        groups.Sort((a, b) => a.startTime.CompareTo(b.startTime));

        float elapsed = 0f;
        int   gi      = 0;

        while (gi < groups.Count)
        {
            var g = groups[gi];
            if (elapsed >= g.startTime)
            {
                var spawner = FindSpawner(g.spawnerIndex);
                if (spawner != null)
                {
                    _activeSpawnGroups++;
                    StartCoroutine(SpawnGroupTracked(spawner, g, CurrentWave));
                }
                gi++;
            }
            else
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
        }
        _waveSchedulingDone = true;
    }

    IEnumerator SpawnGroupTracked(UnitSpawner spawner, WaveEntry entry, int waveNumber)
    {
        yield return StartCoroutine(spawner.SpawnGroup(entry, waveNumber));
        _activeSpawnGroups--;
    }

    UnitSpawner FindSpawner(int spawnerIndex)
    {
        foreach (var s in _spawners)
            if (s != null && (spawnerIndex < 0 || s.pathIndex == spawnerIndex))
                return s;
        return null;
    }

    void CheckWaveClear()
    {
        if (!_waveSchedulingDone) return;
        _aliveUnits.RemoveAll(u => u == null || !u.isAlive);
        if (_activeSpawnGroups <= 0 && _aliveUnits.Count == 0)
            OnWaveClear();
    }

    void OnWaveClear()
    {
        IsWaveActive = false;
        Debug.Log($"[WaveManager] Wave {CurrentWave} cleared.");

        ObjectiveTracker.NotifyWaveReached(CurrentWave);

        // Per-wave modifier effects
        float goldPerWave  = ModifierSelection.GetFloat("GoldPerWave");
        float livesDrain   = ModifierSelection.GetFloat("LivesDrainPerWave");
        if (goldPerWave > 0f)  _resources?.ChangeResourceOne((int)goldPerWave);
        if (livesDrain  > 0f)  _logic?.UpdateLives(-livesDrain);

        // Reset forgive flag for next wave
        ForgiveNextLeak = ModifierSelection.HasEffect("ForgiveFirstLeak");

        if (CurrentWave >= TotalWaves) Victory();
        else AutoStartCountdown = AutoStartDelay;
    }

    // ── Public API ────────────────────────────────────────────────────

    public void RegisterSpawner(UnitSpawner s)
    {
        if (s != null && !_spawners.Contains(s)) _spawners.Add(s);
    }

    public void RegisterUnit(UnitManager u)
    {
        if (u != null && !_aliveUnits.Contains(u)) _aliveUnits.Add(u);
    }

    public void NotifyGameOver()
    {
        if (IsGameOver) return;
        IsGameOver   = true;
        IsWaveActive = false;
        Time.timeScale = 0f;
        Debug.Log("[WaveManager] Game Over.");
    }

    public void ResetForLevel(WaveDefinition[] waveDefs, List<UnitSpawner> spawners)
    {
        StopAllCoroutines();
        _aliveUnits.Clear();
        _activeSpawnGroups = 0;

        _spawners.Clear();
        if (spawners != null)
            foreach (var s in spawners) if (s != null) _spawners.Add(s);

        CurrentWave           = 0;
        IsWaveActive          = false;
        IsVictory             = false;
        IsGameOver            = false;
        AutoStartCountdown    = -1f;
        ForgiveNextLeak       = false;
        _waveSchedulingDone   = false;

        _waveDefs  = waveDefs != null ? new List<WaveDefinition>(waveDefs) : new();
        TotalWaves = _waveDefs.Count;
        Debug.Log($"[WaveManager] {TotalWaves} wave(s) loaded.");
    }

    public static void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Victory()
    {
        IsVictory    = true;
        IsWaveActive = false;
        Debug.Log("[WaveManager] Victory!");
    }
}
