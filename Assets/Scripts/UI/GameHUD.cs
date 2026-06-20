using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and drives the entire game HUD in code. No inspector wiring, no prefabs.
///
/// Setup: drop this component on any persistent GameObject in the scene.
///
/// Layout:
///   Top bar   — Gold (left) | Wave status (center) | Lives (right)
///   Bottom    — "Start Wave" button, center-bottom
///   Overlays  — Game Over and Victory panels (shown/hidden automatically)
/// </summary>
[DisallowMultipleComponent]
public class GameHUD : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────
    public static GameHUD Instance { get; private set; }

    // ── Live refs ─────────────────────────────────────────────────────
    private Text   _goldText;
    private Text   _waveText;
    private Text   _livesText;
    private Button _waveButton;
    private Text   _waveButtonLabel;
    private GameObject _gameOverPanel;
    private GameObject _victoryPanel;

    // ── Header balance scores ─────────────────────────────────────────
    private Text _hdrElem;
    private Text _hdrArc;
    private Text _hdrPhys;

    // ── Tower info panel ──────────────────────────────────────────────
    private GameObject _towerPanel;
    private Text       _tpName;
    private Text       _tpBalance;
    private Text       _tpBalanceDist;
    private Text       _tpDamage;
    private Text       _tpFireRate;
    private Text       _tpKills;
    private TowerInfo  _selectedTower;

    // ── Cached managers ───────────────────────────────────────────────
    private ResourceManagerScript _rm;
    private LogicManager          _lm;

    // ── Style constants ───────────────────────────────────────────────
    static readonly Color C_BarBg      = new Color(0.07f, 0.07f, 0.13f, 0.92f);
    static readonly Color C_Gold       = new Color(1.00f, 0.85f, 0.30f, 1.00f);
    static readonly Color C_Lives      = new Color(0.95f, 0.30f, 0.30f, 1.00f);
    static readonly Color C_Wave       = new Color(0.75f, 0.90f, 1.00f, 1.00f);
    static readonly Color C_BtnReady   = new Color(0.18f, 0.60f, 0.28f, 1.00f);
    static readonly Color C_BtnWait    = new Color(0.28f, 0.28f, 0.32f, 1.00f);
    static readonly Color C_OverlayBg  = new Color(0.04f, 0.04f, 0.10f, 0.94f);
    static readonly Color C_GameOver   = new Color(0.95f, 0.22f, 0.22f, 1.00f);
    static readonly Color C_Victory    = new Color(0.28f, 0.90f, 0.42f, 1.00f);
    static readonly Color C_BtnRestart = new Color(0.18f, 0.46f, 0.88f, 1.00f);

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildHUD();
    }

    void Start()
    {
        _rm = FindFirstObjectByType<ResourceManagerScript>();
        _lm = FindFirstObjectByType<LogicManager>();
    }

    // ── Build ─────────────────────────────────────────────────────────

    void BuildHUD()
    {
        // Root canvas
        var canvasGO = new GameObject("[GameHUD]");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;   // above everything else

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        BuildTopBar(canvasGO);
        BuildWaveButton(canvasGO);
        BuildTowerPanel(canvasGO);
        _gameOverPanel = BuildOverlay(canvasGO, "GameOverPanel",  "GAME OVER", C_GameOver,  "Restart",    WaveManager.Restart);
        _victoryPanel  = BuildOverlay(canvasGO, "VictoryPanel",   "VICTORY",   C_Victory,   "Play Again", WaveManager.Restart);

        _gameOverPanel.SetActive(false);
        _victoryPanel.SetActive(false);
    }

    void OnEnable()
    {
        TowerInfo.OnTowerClicked += ShowTowerPanel;
        TowerInfo.OnTowerKill    += RefreshTowerPanel;
    }

    void OnDisable()
    {
        TowerInfo.OnTowerClicked -= ShowTowerPanel;
        TowerInfo.OnTowerKill    -= RefreshTowerPanel;
    }

    void ShowTowerPanel(TowerInfo info)
    {
        _selectedTower = info;
        RefreshTowerPanel(info);
        _towerPanel?.SetActive(true);
    }

    void RefreshTowerPanel(TowerInfo info)
    {
        if (info != _selectedTower) return;
        if (_tpName     != null) _tpName.text     = info.displayName.ToUpper();
        if (_tpBalance  != null) _tpBalance.text  = $"Type  {info.balanceType}";
        RefreshBalanceDist();
        if (_tpDamage   != null) _tpDamage.text   = $"Damage   {info.damage:0.#}";
        if (_tpFireRate != null) _tpFireRate.text  = $"Fire Rate  {info.FireRate:0.##}/s";
        if (_tpKills    != null) _tpKills.text     = $"Kills  {info.KillCount}";
    }

    // Returns all non-ghost towers, plus a dictionary of definitionId → count
    static TowerInfo[] GetTowerData(out Dictionary<string, int> idCounts)
    {
        var towers = FindObjectsByType<TowerInfo>(FindObjectsSortMode.None);
        idCounts = new Dictionary<string, int>();
        foreach (var t in towers)
        {
            if (t.isGhost) continue;
            if (!idCounts.ContainsKey(t.definitionId)) idCounts[t.definitionId] = 0;
            idCounts[t.definitionId]++;
        }
        return towers;
    }

    // 1-4 towers of the same id = 1.00; steeply declines past 4 via (4/count)^2
    static float BalanceRatio(int count)
    {
        if (count <= 0) return 0f;
        if (count <= 4) return 1f;
        float r = 4f / count;
        return r * r;
    }

    void RefreshBalanceDist()
    {
        if (_tpBalanceDist == null || _selectedTower == null) return;
        GetTowerData(out var idCounts);
        idCounts.TryGetValue(_selectedTower.definitionId, out int count);
        if (count == 0) { _tpBalanceDist.text = "—"; return; }
        string lbl   = _selectedTower.balanceType.ToString()[0].ToString();
        float  ratio = BalanceRatio(count);
        _tpBalanceDist.text = $"{lbl}  1 ({ratio:0.00})";
    }

    void RefreshHeaderBalance()
    {
        var towers = GetTowerData(out var idCounts);
        float eCum = 0f, aCum = 0f, pCum = 0f;
        foreach (var t in towers)
        {
            if (t.isGhost) continue;
            float contribution = BalanceRatio(idCounts[t.definitionId]);
            switch (t.balanceType)
            {
                case BalanceType.Elemental: eCum += contribution; break;
                case BalanceType.Arcane:    aCum += contribution; break;
                case BalanceType.Physical:  pCum += contribution; break;
            }
        }
        if (_hdrElem != null) _hdrElem.text = $"E  {eCum:0.00}";
        if (_hdrArc  != null) _hdrArc.text  = $"A  {aCum:0.00}";
        if (_hdrPhys != null) _hdrPhys.text = $"P  {pCum:0.00}";
    }

    // ── Tower info panel ──────────────────────────────────────────────

    void BuildTowerPanel(GameObject root)
    {
        const float W = 260f, H = 256f, PAD = 14f, ROW = 36f;

        _towerPanel = new GameObject("TowerInfoPanel");
        _towerPanel.transform.SetParent(root.transform, false);

        var rt = _towerPanel.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(0f, 0f);
        rt.pivot            = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(20f, 20f);
        rt.sizeDelta        = new Vector2(W, H);

        _towerPanel.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.13f, 0.95f);

        // × close button — top-right corner
        var closeGO = MakeRect("CloseBtn", _towerPanel, W - 34f, H - 34f, 30f, 30f);
        var cImg    = closeGO.AddComponent<Image>();
        cImg.color  = new Color(0.55f, 0.12f, 0.12f, 1f);
        var cBtn    = closeGO.AddComponent<Button>();
        cBtn.targetGraphic = cImg;
        cBtn.onClick.AddListener(() => { _towerPanel.SetActive(false); _selectedTower = null; });
        MakeText(MakeRect("Label", closeGO, 0f, 0f, 30f, 30f), "×", Color.white, 22, bold: true);

        // Stat rows — stacked top-down with PAD margin
        float y = H - PAD - ROW;
        _tpName        = MakeText(MakeRect("Name",        _towerPanel, PAD, y, W - PAD*2, ROW), "", new Color(1f, 0.85f, 0.3f), 17, bold: true); y -= ROW;
        _tpBalance     = MakeText(MakeRect("Balance",     _towerPanel, PAD, y, W - PAD*2, ROW), "", new Color(0.6f, 0.9f, 0.6f), 14); y -= ROW;
        _tpBalanceDist = MakeText(MakeRect("BalanceDist", _towerPanel, PAD, y, W - PAD*2, ROW), "", new Color(0.75f, 0.75f, 0.85f), 12); y -= ROW;
        _tpDamage      = MakeText(MakeRect("Damage",      _towerPanel, PAD, y, W - PAD*2, ROW), "", new Color(0.9f, 0.9f, 0.9f), 14); y -= ROW;
        _tpFireRate    = MakeText(MakeRect("FireRate",    _towerPanel, PAD, y, W - PAD*2, ROW), "", new Color(0.9f, 0.9f, 0.9f), 14); y -= ROW;
        _tpKills       = MakeText(MakeRect("Kills",       _towerPanel, PAD, y, W - PAD*2, ROW), "", new Color(0.95f, 0.5f, 0.5f), 14);

        _towerPanel.SetActive(false);
    }

    // Absolute-positioned rect child (x,y from bottom-left of parent)
    static GameObject MakeRect(string name, GameObject parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt          = go.AddComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0f, 0f);
        rt.anchorMax    = new Vector2(0f, 0f);
        rt.pivot        = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta    = new Vector2(w, h);
        return go;
    }

    Text MakeText(GameObject go, string text, Color color, int size, bool bold = false)
    {
        var txt       = go.AddComponent<Text>();
        txt.text      = text;
        txt.color     = color;
        txt.font      = GetFont();
        txt.fontSize  = size;
        txt.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.alignment = TextAnchor.MiddleLeft;
        return txt;
    }

    // ── Top bar ───────────────────────────────────────────────────────

    void BuildTopBar(GameObject root)
    {
        var bar = new GameObject("TopBar");
        bar.transform.SetParent(root.transform, false);

        var rt = bar.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, 90f);

        bar.AddComponent<Image>().color = C_BarBg;

        // Top row: Gold | Wave | Lives
        _goldText  = AddLabel(bar, "Gold",  new Vector2(0f,    0.45f), new Vector2(0.33f, 1f),
            "⚡  0",  C_Gold,  26, TextAnchor.MiddleLeft,   leftPad: 24f);
        _waveText  = AddLabel(bar, "Wave",  new Vector2(0.33f, 0.45f), new Vector2(0.67f, 1f),
            "Ready", C_Wave,  24, TextAnchor.MiddleCenter);
        _livesText = AddLabel(bar, "Lives", new Vector2(0.67f, 0.45f), new Vector2(1f,    1f),
            "♥  20", C_Lives, 26, TextAnchor.MiddleRight,  rightPad: 24f);

        // Bottom row: Elemental | Arcane | Physical balance totals
        _hdrElem = AddLabel(bar, "Elem", new Vector2(0f,    0f), new Vector2(0.33f, 0.5f),
            "E  0 (0%)", new Color(0.4f, 0.85f, 0.5f), 18, TextAnchor.MiddleLeft, leftPad: 24f);
        _hdrArc  = AddLabel(bar, "Arc",  new Vector2(0.33f, 0f), new Vector2(0.67f, 0.5f),
            "A  0 (0%)", new Color(0.7f, 0.5f, 1.0f),  18, TextAnchor.MiddleCenter);
        _hdrPhys = AddLabel(bar, "Phys", new Vector2(0.67f, 0f), new Vector2(1f,    0.5f),
            "P  0 (0%)", new Color(0.9f, 0.55f, 0.3f), 18, TextAnchor.MiddleRight, rightPad: 24f);
    }

    // ── Start Wave button ─────────────────────────────────────────────

    void BuildWaveButton(GameObject root)
    {
        var btnGO = new GameObject("WaveButton");
        btnGO.transform.SetParent(root.transform, false);

        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 14f);
        rt.sizeDelta        = new Vector2(280f, 58f);

        var img = btnGO.AddComponent<Image>();
        img.color = C_BtnReady;

        _waveButton = btnGO.AddComponent<Button>();
        _waveButton.targetGraphic = img;

        var cols = _waveButton.colors;
        cols.normalColor      = C_BtnReady;
        cols.highlightedColor = C_BtnReady + new Color(0.1f, 0.1f, 0.1f, 0f);
        cols.pressedColor     = C_BtnReady - new Color(0.1f, 0.1f, 0.1f, 0f);
        cols.disabledColor    = C_BtnWait;
        _waveButton.colors    = cols;

        _waveButton.onClick.AddListener(() => WaveManager.Instance?.StartNextWave());

        // Label
        _waveButtonLabel = AddLabel(btnGO, "Label",
            Vector2.zero, Vector2.one,
            "START WAVE 1", Color.white, 22, TextAnchor.MiddleCenter,
            bold: true);
    }

    // ── Overlay builder ───────────────────────────────────────────────

    GameObject BuildOverlay(GameObject root, string goName,
        string title, Color titleColor, string btnText,
        UnityEngine.Events.UnityAction onBtn)
    {
        var panel = new GameObject(goName);
        panel.transform.SetParent(root.transform, false);

        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        panel.AddComponent<Image>().color = C_OverlayBg;

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panel.transform, false);
        var tRT = titleGO.AddComponent<RectTransform>();
        tRT.anchorMin        = new Vector2(0.5f, 0.5f);
        tRT.anchorMax        = new Vector2(0.5f, 0.5f);
        tRT.pivot            = new Vector2(0.5f, 0.5f);
        tRT.anchoredPosition = new Vector2(0f, 80f);
        tRT.sizeDelta        = new Vector2(900f, 120f);

        var tTxt = titleGO.AddComponent<Text>();
        tTxt.text      = title;
        tTxt.color     = titleColor;
        tTxt.font      = GetFont();
        tTxt.fontSize  = 80;
        tTxt.fontStyle = FontStyle.Bold;
        tTxt.alignment = TextAnchor.MiddleCenter;

        // Subtitle
        var subGO = new GameObject("Sub");
        subGO.transform.SetParent(panel.transform, false);
        var sRT = subGO.AddComponent<RectTransform>();
        sRT.anchorMin        = new Vector2(0.5f, 0.5f);
        sRT.anchorMax        = new Vector2(0.5f, 0.5f);
        sRT.pivot            = new Vector2(0.5f, 0.5f);
        sRT.anchoredPosition = new Vector2(0f, 10f);
        sRT.sizeDelta        = new Vector2(700f, 60f);

        var subTxt = subGO.AddComponent<Text>();
        subTxt.text      = goName == "GameOverPanel" ? "Your base has been breached." : "All waves repelled.";
        subTxt.color     = new Color(0.8f, 0.8f, 0.85f, 1f);
        subTxt.font      = GetFont();
        subTxt.fontSize  = 28;
        subTxt.alignment = TextAnchor.MiddleCenter;

        // Button
        var bGO = new GameObject("RestartButton");
        bGO.transform.SetParent(panel.transform, false);
        var bRT = bGO.AddComponent<RectTransform>();
        bRT.anchorMin        = new Vector2(0.5f, 0.5f);
        bRT.anchorMax        = new Vector2(0.5f, 0.5f);
        bRT.pivot            = new Vector2(0.5f, 0.5f);
        bRT.anchoredPosition = new Vector2(0f, -80f);
        bRT.sizeDelta        = new Vector2(280f, 62f);

        var bImg = bGO.AddComponent<Image>();
        bImg.color = C_BtnRestart;
        var btn = bGO.AddComponent<Button>();
        btn.targetGraphic = bImg;
        btn.onClick.AddListener(onBtn);

        AddLabel(bGO, "Label", Vector2.zero, Vector2.one,
            btnText, Color.white, 26, TextAnchor.MiddleCenter, bold: true);

        return panel;
    }

    // ── Update ────────────────────────────────────────────────────────

    void Update()
    {
        var wm = WaveManager.Instance;

        // Gold
        if (_goldText != null && _rm != null)
            _goldText.text = $"⚡  {_rm.resourceOne}";

        // Lives
        if (_livesText != null && _lm != null)
            _livesText.text = $"♥  {Mathf.Max(0, (int)_lm.lives)}";

        // Wave label
        if (_waveText != null && wm != null)
        {
            if (wm.IsGameOver || wm.IsVictory)
                _waveText.text = string.Empty;
            else if (wm.CurrentWave == 0)
                _waveText.text = "Prep Phase";
            else if (wm.IsWaveActive)
                _waveText.text = $"Wave  {wm.CurrentWave} / {wm.TotalWaves}";
            else if (wm.CurrentWave >= wm.TotalWaves)
                _waveText.text = $"Wave  {wm.CurrentWave} / {wm.TotalWaves}  —  Cleared";
            else
                _waveText.text = $"Wave  {wm.CurrentWave} / {wm.TotalWaves}  —  Prep";
        }

        // Wave button
        if (_waveButton != null && wm != null)
        {
            bool canSend = wm.CanStartWave;
            _waveButton.interactable = canSend;

            if (_waveButtonLabel != null)
            {
                int next = wm.CurrentWave + 1;
                _waveButtonLabel.text = wm.IsWaveActive
                    ? "Wave in progress..."
                    : wm.CurrentWave == 0
                        ? "START WAVE 1"
                        : $"SEND WAVE {next}";
            }
        }

        // Overlays
        if (wm != null)
        {
            if (_gameOverPanel != null) _gameOverPanel.SetActive(wm.IsGameOver);
            if (_victoryPanel  != null) _victoryPanel.SetActive(wm.IsVictory);
        }

        // Header balance (always live)
        RefreshHeaderBalance();

        // Tower panel live refresh
        if (_selectedTower != null)
        {
            if (_tpKills != null) _tpKills.text = $"Kills  {_selectedTower.KillCount}";
            RefreshBalanceDist();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    Font _font;
    Font GetFont()
    {
        if (_font != null) return _font;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _font;
    }

    Text AddLabel(GameObject parent, string goName,
        Vector2 anchorMin, Vector2 anchorMax,
        string content, Color color, int size,
        TextAnchor anchor,
        float leftPad = 0f, float rightPad = 0f,
        bool bold = false)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(leftPad, 0f);
        rt.offsetMax = new Vector2(-rightPad, 0f);

        var txt = go.AddComponent<Text>();
        txt.text      = content;
        txt.color     = color;
        txt.font      = GetFont();
        txt.fontSize  = size;
        txt.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.alignment = anchor;

        return txt;
    }
}
