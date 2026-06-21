using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns wave flow, alive-unit tracking, win/lose state.
///
/// Setup:
///   1. Add this component to any persistent GameObject in the scene.
///   2. UnitSpawners auto-register via RegisterSpawner() in their Start().
///   3. Press Space or click the Start Wave button in GameHUD to begin each wave.
///
/// Win  — all waves cleared with no enemies alive.
/// Lose — player lives reach zero (LogicManager calls NotifyGameOver).
/// </summary>
public class WaveManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────
    public static WaveManager Instance { get; private set; }

    // ── Public state ──────────────────────────────────────────────────
    public int  CurrentWave  { get; private set; }   // 1-based; 0 = not started
    public int  TotalWaves   { get; private set; }
    public bool IsWaveActive { get; private set; }
    public bool IsVictory    { get; private set; }
    public bool IsGameOver   { get; private set; }

    // ── Auto-start ────────────────────────────────────────────────────
    public const float AutoStartDelay = 5f;
    public float AutoStartCountdown   { get; set; } = -1f;  // -1 = not counting
    public bool  IsCountingDown       => AutoStartCountdown >= 0f;

    // ── Private ───────────────────────────────────────────────────────
    private List<WaveDefinition> _waveDefs   = new List<WaveDefinition>();
    private List<UnitSpawner>    _spawners   = new List<UnitSpawner>();
    private List<UnitManager>    _aliveUnits = new List<UnitManager>();
    private LogicManager         _logic;

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        _logic = FindFirstObjectByType<LogicManager>();
        LoadWaves();
    }

    void LoadWaves()
    {
        var asset = Resources.Load<TextAsset>("Definitions/waves");
        if (asset == null)
        {
            Debug.LogError("[WaveManager] waves.json not found at Resources/Definitions/waves.json");
            return;
        }

        var col = JsonUtility.FromJson<WaveCollection>(asset.text);
        _waveDefs  = col?.waves ?? new List<WaveDefinition>();
        TotalWaves = _waveDefs.Count;
        Debug.Log($"[WaveManager] Loaded {TotalWaves} wave(s).");
    }

    void Update()
    {
        if (IsGameOver || IsVictory) return;

        // Space bar as a convenience shortcut
        if (Input.GetKeyDown(KeyCode.Space) && CanStartWave)
        {
            AutoStartCountdown = -1f;
            StartNextWave();
        }

        if (IsWaveActive)
        {
            CheckWaveClear();
        }
        else if (IsCountingDown)
        {
            AutoStartCountdown -= Time.deltaTime;
            if (AutoStartCountdown <= 0f)
            {
                AutoStartCountdown = -1f;
                StartNextWave();
            }
        }
    }

    // ── Wave clear detection ──────────────────────────────────────────

    void CheckWaveClear()
    {
        // Remove dead or destroyed unit references
        _aliveUnits.RemoveAll(u => u == null || !u.isAlive);

        // Any spawner still emitting?
        bool spawnersActive = false;
        foreach (var s in _spawners)
        {
            if (s != null && s.IsSpawning) { spawnersActive = true; break; }
        }

        if (!spawnersActive && _aliveUnits.Count == 0)
            OnWaveClear();
    }

    void OnWaveClear()
    {
        IsWaveActive = false;
        Debug.Log($"[WaveManager] Wave {CurrentWave} cleared.");

        if (CurrentWave >= TotalWaves)
            Victory();
        else
            AutoStartCountdown = AutoStartDelay;
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>True when a new wave can be sent.</summary>
    public bool CanStartWave =>
        !IsWaveActive && !IsVictory && !IsGameOver && CurrentWave < TotalWaves;

    /// <summary>Advance to the next wave and activate all registered spawners.</summary>
    public void StartNextWave()
    {
        if (!CanStartWave) return;

        CurrentWave++;
        IsWaveActive = true;

        var def = _waveDefs[CurrentWave - 1];
        foreach (var s in _spawners)
        {
            if (s == null) continue;
            int pi = s.pathIndex;
            var groups = def.groups.FindAll(g => g.spawnerIndex < 0 || g.spawnerIndex == pi);
            s.BeginWave(groups, CurrentWave);
        }

        Debug.Log($"[WaveManager] Wave {CurrentWave}/{TotalWaves} started.");
    }

    /// <summary>Called by UnitSpawner in its Start().</summary>
    public void RegisterSpawner(UnitSpawner s)
    {
        if (s != null && !_spawners.Contains(s))
            _spawners.Add(s);
    }

    /// <summary>Called by UnitSpawner each time it spawns a unit.</summary>
    public void RegisterUnit(UnitManager u)
    {
        if (u != null && !_aliveUnits.Contains(u))
            _aliveUnits.Add(u);
    }

    /// <summary>Called by LogicManager when lives reach zero.</summary>
    public void NotifyGameOver()
    {
        if (IsGameOver) return;
        IsGameOver   = true;
        IsWaveActive = false;
        Time.timeScale = 0f;
        Debug.Log("[WaveManager] Game Over.");
    }

    /// <summary>Reload the active scene and reset time scale.</summary>
    public static void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ── Private ───────────────────────────────────────────────────────

    void Victory()
    {
        IsVictory    = true;
        IsWaveActive = false;
        Debug.Log("[WaveManager] Victory!");
    }
}
