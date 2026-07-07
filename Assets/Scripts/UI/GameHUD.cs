using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Orchestrates all HUD sub-components. Owns the canvas, event routing,
/// click-to-select logic, and the public API used by other game systems.
/// </summary>
[DisallowMultipleComponent]
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    // ── Sub-components ─────────────────────────────────────────────────
    HUDStatsPanel    _stats;
    HUDInfoPanel     _info;
    HUDWaveBar       _wave;
    HUDDebugPanel    _debug;
    HUDTierSelector  _tier;
    HUDOverlays      _overlays;
    HUDPauseMenu     _pause;
    Canvas           _canvas;

    /// <summary>Wave bar exposes pause/speed control for the pause menu.</summary>
    public HUDWaveBar WaveBar => _wave;

    // ── Lifecycle ──────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildHUD();
    }

    void BuildHUD()
    {
        if (StarManager.Instance == null)
            new GameObject("[StarManager]").AddComponent<StarManager>();

        // Root canvas
        var canvasGO = new GameObject("[GameHUD]");
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50;
        var canvas = _canvas;
        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Shop panel (magenta right — built by TowerShop, we provide the RectTransform)
        BuildShopArea(canvasGO);

        // Sub-components — each adds itself to this GO and builds into canvasGO
        _stats    = gameObject.AddComponent<HUDStatsPanel>();   _stats.Build(canvasGO);
        _info     = gameObject.AddComponent<HUDInfoPanel>();    _info.Build(canvasGO);
        _wave     = gameObject.AddComponent<HUDWaveBar>();      _wave.Build(canvasGO);
        _debug    = gameObject.AddComponent<HUDDebugPanel>();   _debug.Build(canvasGO);
        _tier     = gameObject.AddComponent<HUDTierSelector>(); _tier.Build(canvasGO);
        _overlays = gameObject.AddComponent<HUDOverlays>();     _overlays.Build(canvasGO);
        _pause    = gameObject.AddComponent<HUDPauseMenu>();    _pause.Build(canvasGO);

        // Decorative border overlay (Art/UI/GameBorder)
        BuildSpriteOverlay(canvasGO, "Art/UI/GameBorder", sortingOrder: 10);

        // Teal play-field border
        BuildPlayFieldBorder(canvasGO);
    }

    void Start() => AudioManager.PlayMusicEvent("music_game");

    void BuildShopArea(GameObject canvasRoot)
    {
        const float RW     = HUDHelpers.RIGHT_W;
        const float INFO_H = HUDHelpers.INFO_H;
        const float TIER_H = HUDHelpers.TIER_H;
        float shopH = 1080f - TIER_H - INFO_H;

        var shopGO = new GameObject("ShopPanel");
        shopGO.transform.SetParent(canvasRoot.transform, false);
        var shopRT       = shopGO.AddComponent<RectTransform>();
        shopRT.anchorMin = new Vector2(1f, 0f);
        shopRT.anchorMax = new Vector2(1f, 0f);
        shopRT.pivot     = new Vector2(1f, 0f);
        shopRT.anchoredPosition = new Vector2(0f, INFO_H);
        shopRT.sizeDelta = new Vector2(RW, shopH);
        shopGO.AddComponent<UnityEngine.UI.Image>().color = new Color(0.10f, 0.05f, 0.14f, 0.95f);

        // Ensure TowerShop exists and give it the panel RT
        var ts = FindFirstObjectByType<TowerShop>() ?? gameObject.AddComponent<TowerShop>();
        ts.shopPanel = shopRT;
    }

    void BuildPlayFieldBorder(GameObject canvasRoot)
    {
        // Black outer frame only — decorative border sprite handles the rest
        const float F = 5f;
        Color cBlack = new Color(0f, 0f, 0f, 1f);

        void Strip(string n, float x, float y, float w, float h)
        {
            var go  = HUDHelpers.MakeRect(n, canvasRoot, x, y, w, h);
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = cBlack;
            img.raycastTarget = false;
        }

        Strip("Frame_L", 0f,        0f,        F,     1080f);
        Strip("Frame_R", 1920f - F, 0f,        F,     1080f);
        Strip("Frame_B", 0f,        0f,        1920f, F);
        Strip("Frame_T", 0f,        1080f - F, 1920f, F);
    }

    void OnEnable()
    {
        TowerInfo.OnTowerClicked           += OnTowerClicked;
        TowerInfo.OnTowerKill              += OnTowerKill;
        ObjectiveTracker.OnObjectiveUpdated += OnObjectiveUpdated;
    }

    void OnDisable()
    {
        TowerInfo.OnTowerClicked           -= OnTowerClicked;
        TowerInfo.OnTowerKill              -= OnTowerKill;
        ObjectiveTracker.OnObjectiveUpdated -= OnObjectiveUpdated;
    }

    void OnTowerClicked(TowerInfo info)  => _info?.ShowTower(info);
    void OnTowerKill(TowerInfo info)     => _info?.OnTowerKill(info);
    void OnObjectiveUpdated()            => _stats?.RefreshObjectives();

    // ── Public API (called by other game systems) ──────────────────────

    public void SelectEnemy(UnitManager unit) => _info?.ShowEnemy(unit);

    public void OpenResearchPanel() => _info?.ShowResearch();

    public void CloseAllPanels()
    {
        _info?.Hide();
    }

    public void ResetForLevelLoad()
    {
        RunStats.ResetForLevel();
        _info?.Reset();
        _overlays?.Reset();
        _wave?.ResetPause();
        _pause?.HideImmediate();
        _stats?.RefreshObjectives();
        _tier?.SyncWithShop();
    }

    // ── Update ─────────────────────────────────────────────────────────

    void Update()
    {
        BalanceManager.Instance?.Recalculate();
        _overlays?.Tick();
        HandleClickSelection();
        HandleTowerRotation();

        // Lifetime playtime — skip while paused or on an end screen
        var wm = WaveManager.Instance;
        if (Time.timeScale > 0f && (wm == null || (!wm.IsVictory && !wm.IsGameOver)))
            RunStats.TickPlaytime(Time.unscaledDeltaTime);
    }

    void HandleClickSelection()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current.IsPointerOverGameObject()) return;
        if (TowerPlacer.Instance != null && TowerPlacer.Instance.IsPlacing) return;
        // The click that placed a tower shouldn't also select it (script-order race)
        if (Time.frameCount == TowerPlacer.LastPlacementFrame) return;
        if (Camera.main == null) return;

        Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // Ignore clicks on collectible drops
        foreach (var drop in FindObjectsByType<BountyDrop>(FindObjectsSortMode.None))
            if (Vector2.Distance(mouse, drop.transform.position) <= drop.clickRadius) return;

        // Towers — pick closest within click radius
        TowerInfo best = null;
        float bestDist = float.MaxValue;
        foreach (var ti in FindObjectsByType<TowerInfo>(FindObjectsSortMode.None))
        {
            if (ti.isGhost) continue;
            float dist = Vector2.Distance(mouse, ti.transform.position);
            if (dist <= ti.ClickRadius && dist < bestDist) { best = ti; bestDist = dist; }
        }

        if (best != null) { TowerInfo.OnTowerClickedPublic(best); return; }

        // Enemies — pick closest within 0.6 world units
        UnitManager bestUnit = null;
        float bestUDist = 0.6f;
        foreach (var um in FindObjectsByType<UnitManager>(FindObjectsSortMode.None))
        {
            if (!um.isAlive) continue;
            float dist = Vector2.Distance(mouse, um.transform.position);
            if (dist < bestUDist) { bestUnit = um; bestUDist = dist; }
        }

        if (bestUnit != null) SelectEnemy(bestUnit);
        else TowerInfo.OnTowerClickedPublic(null);
    }

    void HandleTowerRotation()
    {
        var selectedTower = _info?.GetSelectedTower();
        if (selectedTower == null) return;
        if (TowerPlacer.Instance != null && TowerPlacer.Instance.IsPlacing) return;

        const float ROT_SPEED = 90f;
        float rot = 0f;
        if (Input.GetKey(KeyCode.Q)) rot =  ROT_SPEED * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) rot = -ROT_SPEED * Time.deltaTime;
        if (rot != 0f) selectedTower.transform.Rotate(0f, 0f, rot);
    }

    static void BuildSpriteOverlay(GameObject canvasRoot, string resourcePath, int sortingOrder = 10)
    {
        var sp = Resources.Load<Sprite>(resourcePath);
        if (sp == null) { Debug.LogWarning($"[GameHUD] Overlay sprite not found: {resourcePath}"); return; }

        var go  = new GameObject(System.IO.Path.GetFileName(resourcePath));
        go.transform.SetParent(canvasRoot.transform, false);
        var rt        = go.AddComponent<UnityEngine.UI.RawImage>();
        rt.texture    = sp.texture;
        rt.raycastTarget = false;
        var rectT     = go.GetComponent<RectTransform>();
        rectT.anchorMin = Vector2.zero;
        rectT.anchorMax = Vector2.one;
        rectT.offsetMin = rectT.offsetMax = Vector2.zero;
        go.transform.SetSiblingIndex(sortingOrder);
    }
}
