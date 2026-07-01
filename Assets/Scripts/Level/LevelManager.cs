using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven level factory. The scene is a blank canvas — LevelManager reads a JSON
/// and builds everything: background, path nodes, unit spawners, placement zones.
///
/// To add a level: drop a JSON file in Resources/Definitions/Levels/ named level_1.json,
/// level_2.json, etc. No scene changes required.
///
/// Keyboard: 1/2/3 load level 1/2/3 (temporary — will be replaced by menu system).
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    public int CurrentLevel { get; private set; } = -1;

    // All GameObjects this factory spawned — destroyed on next level load
    private readonly List<GameObject> _built = new();
    private PlacementZones _runtimeZones;

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start() => LoadLevel(LevelSelection.SelectedLevel);

    // ── Public API ────────────────────────────────────────────────────

    public void LoadLevel(int levelNumber)
    {
        var data = LoadData(levelNumber);
        if (data == null) return;

        Time.timeScale = 1f;
        CurrentLevel   = levelNumber;

        // ── Tear down ────────────────────────────────────────────────
        DestroyWorldObjects();
        DestroyBuiltObjects();

        // ── Build ────────────────────────────────────────────────────
        BuildBackground(data.backgroundSprite, data.backgroundX, data.backgroundY);
        var spawners = BuildPaths(data.paths);
        BuildPlacementZones(data.placementZones);

        PathGraph.Instance?.ScanScene();
        FindFirstObjectByType<PathVisualizer>()?.RebuildLines();

        // ── Apply difficulty overrides ───────────────────────────────
        var diff = ResolveDifficulty(data);

        int    gold    = diff != null && diff.startGold  > 0  ? Mathf.RoundToInt(diff.startGold  * diff.goldMult) : Mathf.RoundToInt(data.startGold * LevelSelection.GoldMult);
        int    lives   = diff != null && diff.startLives > 0  ? diff.startLives  : data.startLives;
        int    tech    = diff != null && diff.startTech  >= 0 ? diff.startTech   : data.startTech;
        int    tier    = diff != null && diff.startTier  > 0  ? diff.startTier   : data.startTier;
        int    cap     = diff != null && diff.maxTowers  > 0  ? diff.maxTowers   : data.maxTowers;
        var    towers  = diff != null && diff.allowedTowers.Length > 0 ? diff.allowedTowers : data.allowedTowers;
        var    objs    = diff != null && diff.objectives.Length   > 0 ? diff.objectives    : data.objectives;
        var    waves   = diff != null ? FilterWaves(data.waves, diff.disabledGroups) : data.waves;

        // ── Reset managers ───────────────────────────────────────────
        FindFirstObjectByType<ResourceManagerScript>()?.ResetToStart(gold);
        FindFirstObjectByType<LogicManager>()?.ResetToStart(lives);
        TechManager.Instance?.ResetAll(tech, tier);
        ResearchManager.Instance?.ResetAll();
        WaveManager.Instance?.ResetForLevel(waves, spawners);
        BalanceManager.Instance?.SetLevelCap(cap);
        TowerShop.Instance?.Rebuild(towers);
        ApplyModifiers();
        ObjectiveTracker.Load(objs);
        GameHUD.Instance?.ResetForLevelLoad();

        Debug.Log($"[LevelManager] Level {levelNumber} loaded: {data.displayName}");
    }

    // ── Difficulty helpers ────────────────────────────────────────────

    DifficultyDef ResolveDifficulty(LevelData data)
    {
        if (data.difficulties == null || data.difficulties.Length == 0) return null;
        int idx = Mathf.Clamp(LevelSelection.SelectedDifficulty, 0, data.difficulties.Length - 1);
        return data.difficulties[idx];
    }

    WaveDefinition[] FilterWaves(WaveDefinition[] src, string[] disabledGroups)
    {
        if (src == null) return src;
        if (disabledGroups == null || disabledGroups.Length == 0) return src;

        var disabled = new System.Collections.Generic.HashSet<string>(disabledGroups);
        var result   = new WaveDefinition[src.Length];
        for (int wi = 0; wi < src.Length; wi++)
        {
            var orig   = src[wi];
            var groups = new System.Collections.Generic.List<WaveEntry>();
            for (int gi = 0; gi < orig.groups.Count; gi++)
            {
                if (!disabled.Contains($"{wi},{gi}"))
                    groups.Add(orig.groups[gi]);
            }
            result[wi] = new WaveDefinition { groups = groups };
        }
        return result;
    }

    // ── Modifiers ─────────────────────────────────────────────────────

    void ApplyModifiers()
    {
        foreach (var mod in ModifierSelection.Chosen)
        {
            switch (mod.effectType)
            {
                case "StartingGold":
                    FindFirstObjectByType<ResourceManagerScript>()?.ChangeResourceOne((int)mod.value);
                    break;
                case "StartingLives":
                    FindFirstObjectByType<LogicManager>()?.UpdateLives(mod.value);
                    break;
                case "StartingTech":
                    TechManager.Instance?.AddTech((int)mod.value);
                    break;
                // Runtime modifiers — read from ModifierSelection at the point of effect
                case "FullRefund":
                case "TowerSpeedMult":
                case "TowerRangeMult":
                case "TowerDamageMult":
                case "PhysicalDamageMult":
                case "ElementalDamageMult":
                case "ArcaneSpdMult":
                case "ElementalSpdMult":
                case "AuraRadiusMult":
                case "AuraMult":
                case "AuraSlowEnemies":
                case "BountyPerKill":
                case "ForgiveFirstLeak":
                case "LeakDamageReduction":
                case "LastStand":
                case "BonusDrones":
                case "BonusShotgunBullets":
                case "BasicDoubleTap":
                case "GoldPerWave":
                case "LivesDrainPerWave":
                    break;
                default:
                    Debug.LogWarning($"[LevelManager] Unknown modifier effectType '{mod.effectType}'");
                    break;
            }
        }
    }

    // ── Data loading ──────────────────────────────────────────────────

    LevelData LoadData(int levelNumber)
    {
        string path = $"Definitions/Levels/level_{levelNumber}";
        var ta = Resources.Load<TextAsset>(path);
        if (ta == null)
        {
            Debug.LogError($"[LevelManager] Level JSON not found: Resources/{path}.json");
            return null;
        }
        var data = JsonUtility.FromJson<LevelData>(ta.text);
        if (data == null)
            Debug.LogError($"[LevelManager] Failed to parse {path}.json");
        return data;
    }

    // ── Background ────────────────────────────────────────────────────

    void BuildBackground(string spritePath, float x = 0f, float y = 0f, float scaleX = .82f, float scaleY = .82f)
    {
        var go = new GameObject("[Background]");
        go.transform.position = new Vector3(0, 1.125f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Background";
        sr.sortingOrder     = -100;

        if (!string.IsNullOrEmpty(spritePath))
        {
            var sp = Resources.Load<Sprite>(spritePath);
            if (sp != null)
            {
                sr.sprite = sp;
                ScaleBgToFillCamera(go, sr, scaleX, scaleY);
            }
            else Debug.LogWarning($"[LevelManager] Background sprite not found: {spritePath}");
        }

        _built.Add(go);
    }

    static void ScaleBgToFillCamera(GameObject go, SpriteRenderer sr, float scaleX = 1f, float scaleY = 1f)
    {
        var cam = Camera.main;
        if (cam == null || sr.sprite == null) return;

        float camH = cam.orthographicSize * 2f;
        float camW = camH * cam.aspect;

        float sprW = sr.sprite.bounds.size.x;
        float sprH = sr.sprite.bounds.size.y;

        // Base cover scale, then apply per-axis multipliers for manual stretch
        float baseScale = Mathf.Max(camW / sprW, camH / sprH);
        go.transform.localScale = new Vector3(baseScale * scaleX, baseScale * scaleY, 1f);
    }

    // ── Paths ─────────────────────────────────────────────────────────

    List<UnitSpawner> BuildPaths(PathData[] paths)
    {
        var spawners = new List<UnitSpawner>();
        if (paths == null) return spawners;

        foreach (var path in paths)
        {
            if (path.nodes == null || path.nodes.Length == 0) continue;

            // Pass 1 — instantiate nodes
            var nodeMap = new Dictionary<string, PathNode>();
            var nodeGOs = new List<GameObject>();

            foreach (var nd in path.nodes)
            {
                var go   = new GameObject($"PathNode_{nd.id}");
                go.transform.position = new Vector3(nd.x, nd.y, 0f);
                var pn   = go.AddComponent<PathNode>();
                nodeMap[nd.id] = pn;
                nodeGOs.Add(go);
                _built.Add(go);
            }

            // Pass 2 — wire connections
            for (int i = 0; i < path.nodes.Length; i++)
            {
                var nd = path.nodes[i];
                var pn = nodeMap[nd.id];
                if (nd.next == null) continue;
                foreach (var nxtId in nd.next)
                    if (nodeMap.TryGetValue(nxtId, out var nxt))
                        pn.connections.Add(nxt);
            }

            // Head node = nothing points to it = first node in data (by convention)
            // Find it properly: any node not referenced in any other's next list
            var referenced = new HashSet<string>();
            foreach (var nd in path.nodes)
                if (nd.next != null)
                    foreach (var nxt in nd.next)
                        referenced.Add(nxt);

            PathNode headNode = null;
            foreach (var nd in path.nodes)
                if (!referenced.Contains(nd.id))
                    { headNode = nodeMap[nd.id]; break; }

            // Spawner at head position
            if (headNode != null)
            {
                var spGO      = new GameObject($"[UnitSpawner_{path.spawnerIndex}]");
                spGO.transform.position = headNode.transform.position;
                var sp        = spGO.AddComponent<UnitSpawner>();
                sp.headNode   = headNode;
                sp.pathIndex  = path.spawnerIndex;
                spawners.Add(sp);
                _built.Add(spGO);
            }
        }

        return spawners;
    }

    // ── Placement zones ───────────────────────────────────────────────

    void BuildPlacementZones(ZoneData[] zones)
    {
        _runtimeZones = ScriptableObject.CreateInstance<PlacementZones>();

        if (zones != null)
        {
            foreach (var z in zones)
            {
                if (z.type == "lane" && z.points != null && z.points.Length >= 2)
                {
                    var pts  = new Vector2[z.points.Length];
                    var hws  = new float[z.points.Length];
                    for (int i = 0; i < z.points.Length; i++)
                    {
                        pts[i] = new Vector2(z.points[i].x, z.points[i].y);
                        hws[i] = z.points[i].width * 0.5f;
                    }
                    _runtimeZones.zones.Add(new PlacementZones.Zone
                        { type = PlacementZones.ZoneType.Lane, points = pts, halfWidths = hws,
                          halfWidth = z.width * 0.5f });
                }
                else
                {
                    _runtimeZones.zones.Add(new PlacementZones.Zone
                        { type = PlacementZones.ZoneType.Circle,
                          center = new Vector2(z.x, z.y), radius = z.radius });
                }
            }
        }

        var placer = FindFirstObjectByType<TowerPlacer>();
        if (placer != null)
            placer.placementZones = _runtimeZones;

    }

    // ── Cleanup ───────────────────────────────────────────────────────

    void DestroyWorldObjects()
    {
        foreach (var t in FindObjectsByType<TowerInfo>(FindObjectsSortMode.None))
            if (!t.isGhost) Destroy(t.gameObject);

        foreach (var u in FindObjectsByType<UnitManager>(FindObjectsSortMode.None))
            Destroy(u.gameObject);

        foreach (var b in FindObjectsByType<BountyDrop>(FindObjectsSortMode.None))
            Destroy(b.gameObject);

        foreach (var r in FindObjectsByType<ResearchOrb>(FindObjectsSortMode.None))
            Destroy(r.gameObject);

        foreach (var o in FindObjectsByType<IncomeOrb>(FindObjectsSortMode.None))
            Destroy(o.gameObject);

        foreach (var f in FindObjectsByType<FloatingText>(FindObjectsSortMode.None))
            Destroy(f.gameObject);
    }

    void DestroyBuiltObjects()
    {
        foreach (var go in _built)
            if (go != null) DestroyImmediate(go);
        _built.Clear();

        if (_runtimeZones != null)
        {
            DestroyImmediate(_runtimeZones);
            _runtimeZones = null;
        }
    }
}
