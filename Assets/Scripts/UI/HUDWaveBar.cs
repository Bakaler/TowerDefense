using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-right panel — wave status, Start Wave button, pause button, lives.
/// Sits in the right column at the bottom (below the shop panel).
/// </summary>
public class HUDWaveBar : MonoBehaviour
{
    const float W = HUDHelpers.RIGHT_W;
    const float H = HUDHelpers.INFO_H;

    static readonly Color C_BtnReady = new Color(0.18f, 0.60f, 0.28f, 1f);
    static readonly Color C_BtnWait  = new Color(0.28f, 0.28f, 0.32f, 1f);

    static readonly Color C_FfIdle   = new Color(0.20f, 0.30f, 0.22f, 1f);
    static readonly Color C_FfActive = new Color(0.20f, 0.60f, 0.30f, 1f);

    Button _waveButton;
    Text   _waveButtonLabel;
    Button _pauseButton;
    Text   _pauseButtonLabel;
    Text   _waveText;
    Text   _livesText;
    Image  _ffImage;
    Text   _ffLabel;
    bool   _paused;
    float  _speed = 1f;   // desired game speed while unpaused (1 or 2)

    GameObject _canvasRoot;
    GameObject _previewPanel;
    int        _previewIndex = -2;   // wave index the preview was built for (-1 = hidden)

    public bool Paused => _paused;

    public void Build(GameObject canvasRoot)
    {
        _canvasRoot = canvasRoot;
        var panel = new GameObject("WaveBar");
        panel.transform.SetParent(canvasRoot.transform, false);
        var rt       = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(1f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(W, H);
        panel.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.13f, 0.92f);

        const float PAD  = 10f;
        const float BTNH = 52f;

        // Lives — top of panel
        _livesText = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("Lives", panel, PAD, H - PAD - 26f, W - PAD * 2f, 26f),
            "♥  20", new Color(0.95f, 0.3f, 0.3f), 14, bold: true);
        _livesText.alignment = TextAnchor.MiddleCenter;

        // Wave status label
        _waveText = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("WaveLabel", panel, PAD, H - PAD - 52f, W - PAD * 2f, 20f),
            "Prep Phase", new Color(0.75f, 0.9f, 1f), 10);
        _waveText.alignment = TextAnchor.MiddleCenter;

        // Wave button
        float btnY = PAD + BTNH + 6f;
        var wGO  = HUDHelpers.MakeRect("WaveBtn", panel, PAD, btnY, W - PAD * 2f, BTNH);
        var wImg = wGO.AddComponent<Image>(); wImg.color = C_BtnReady;
        _waveButton = wGO.AddComponent<Button>(); _waveButton.targetGraphic = wImg;
        var wc = _waveButton.colors;
        wc.normalColor      = C_BtnReady;
        wc.highlightedColor = C_BtnReady + new Color(0.1f, 0.1f, 0.1f, 0f);
        wc.pressedColor     = C_BtnReady - new Color(0.1f, 0.1f, 0.1f, 0f);
        wc.disabledColor    = C_BtnWait;
        _waveButton.colors = wc;
        _waveButtonLabel = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("L", wGO, 0f, 0f, W - PAD * 2f, BTNH),
            "START WAVE 1", Color.white, 14, bold: true);
        _waveButtonLabel.alignment = TextAnchor.MiddleCenter;
        _waveButton.onClick.AddListener(() =>
        {
            var wm = WaveManager.Instance;
            if (wm == null) return;
            wm.AutoStartCountdown = -1f;
            wm.StartNextWave();
        });

        // Pause button
        const float PW = 52f;
        var pGO  = HUDHelpers.MakeRect("PauseBtn", panel, PAD, PAD, W - PAD * 2f - PW - 4f, BTNH);
        var pImg = pGO.AddComponent<Image>(); pImg.color = new Color(0.22f, 0.22f, 0.30f, 1f);
        _pauseButton = pGO.AddComponent<Button>(); _pauseButton.targetGraphic = pImg;
        _pauseButtonLabel = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("L", pGO, 0f, 0f, W - PAD * 2f - PW - 4f, BTNH),
            "II", Color.white, 18, bold: true);
        _pauseButtonLabel.alignment = TextAnchor.MiddleCenter;
        _pauseButton.onClick.AddListener(TogglePause);

        // Fast-forward button (right of pause) — F key also toggles
        float ffX = PAD + (W - PAD * 2f - PW - 4f) + 4f;
        var (ffBtn, ffLbl) = HUDHelpers.MakeBtn(panel, "FFBtn", ffX, PAD, PW, BTNH,
            C_FfIdle, "▶▶", 14, bold: true);
        _ffImage = ffBtn.image;
        _ffLabel = ffLbl;
        ffBtn.onClick.AddListener(ToggleSpeed);
    }

    public void ResetPause()
    {
        _paused = false;
        _speed  = 1f;
        ApplyTimeScale();
        AudioManager.SetSfxPaused(false);
        if (_pauseButtonLabel != null) _pauseButtonLabel.text = "II";
        RefreshSpeedButton();
    }

    /// <summary>Pause/unpause without losing the chosen game speed. Also used by the Escape menu.</summary>
    public void SetPaused(bool paused)
    {
        _paused = paused;
        ApplyTimeScale();
        AudioManager.SetSfxPaused(paused);   // SFX pause with the game; music keeps playing
        if (_pauseButtonLabel != null) _pauseButtonLabel.text = paused ? "▶" : "II";
    }

    /// <summary>Flips between 1x and 2x. Survives pause — the speed re-applies on resume.</summary>
    public void ToggleSpeed()
    {
        _speed = _speed >= 2f ? 1f : 2f;
        ApplyTimeScale();
        RefreshSpeedButton();
    }

    void TogglePause() => SetPaused(!_paused);

    void ApplyTimeScale() => Time.timeScale = _paused ? 0f : _speed;

    void RefreshSpeedButton()
    {
        if (_ffImage != null) _ffImage.color = _speed >= 2f ? C_FfActive : C_FfIdle;
        if (_ffLabel != null) _ffLabel.text  = _speed >= 2f ? "2×" : "▶▶";
    }

    // ── Next-wave preview (pop-out left of the wave bar) ──────────────

    void UpdateWavePreview(WaveManager wm)
    {
        int idx = wm == null || wm.IsVictory || wm.IsGameOver || wm.CurrentWave >= wm.TotalWaves
            ? -1 : wm.CurrentWave;
        if (idx == _previewIndex) return;
        _previewIndex = idx;

        if (_previewPanel != null) Destroy(_previewPanel);
        _previewPanel = null;
        if (idx < 0) return;

        var def = wm.PeekNextWave();
        if (def == null || def.groups == null || def.groups.Count == 0) return;

        // Aggregate counts per unit type, preserving first-seen order
        var order  = new List<string>();
        var counts = new Dictionary<string, int>();
        foreach (var g in def.groups)
        {
            if (string.IsNullOrEmpty(g.unitDefinitionId)) continue;
            if (!counts.ContainsKey(g.unitDefinitionId)) { counts[g.unitDefinitionId] = 0; order.Add(g.unitDefinitionId); }
            counts[g.unitDefinitionId] += g.count;
        }
        if (order.Count == 0) return;

        const float PW = 218f, ROW = 30f, HDR = 26f, PAD = 8f;
        float ph = HDR + PAD * 2f + order.Count * ROW;

        _previewPanel = new GameObject("NextWavePreview");
        _previewPanel.transform.SetParent(_canvasRoot.transform, false);
        var rt       = _previewPanel.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-W - 6f, 6f);
        rt.sizeDelta = new Vector2(PW, ph);
        var bg = _previewPanel.AddComponent<Image>();
        bg.color         = new Color(0.06f, 0.07f, 0.12f, 0.90f);
        bg.raycastTarget = false;

        var hdr = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("Hdr", _previewPanel, PAD, ph - PAD - HDR, PW - PAD * 2f, HDR),
            $"NEXT WAVE  {idx + 1}", new Color(0.95f, 0.88f, 0.55f), 12, bold: true);
        hdr.alignment = TextAnchor.MiddleCenter;

        var lib = UnitDefinitionLibrary.Instance;
        for (int i = 0; i < order.Count; i++)
        {
            var unitDef = lib != null ? lib.Get(order[i]) : null;
            float y = ph - PAD - HDR - (i + 1) * ROW;

            var iconGO = HUDHelpers.MakeRect($"Icon_{i}", _previewPanel, PAD, y + 2f, ROW - 4f, ROW - 4f);
            var icon   = iconGO.AddComponent<Image>();
            var sprite = DefinitionIcons.Unit(unitDef);
            icon.sprite         = sprite != null ? sprite : RuntimeSprites.Circle(16);
            icon.color          = unitDef != null ? unitDef.tintColor : Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget  = false;

            string unitName = unitDef?.displayName ?? order[i];
            HUDHelpers.MakeText(
                HUDHelpers.MakeRect($"Lbl_{i}", _previewPanel, PAD + ROW + 4f, y, PW - PAD * 2f - ROW - 4f, ROW),
                $"{unitName}  ×{counts[order[i]]}", new Color(0.85f, 0.85f, 0.90f), 12);
        }
    }

    LogicManager _lm;
    void Start() => _lm = LogicManager.Instance;

    void Update()
    {
        var wm = WaveManager.Instance;

        // F toggles game speed — but never while the game-over freeze holds timeScale at 0
        if (Input.GetKeyDown(KeyCode.F) && (wm == null || !wm.IsGameOver))
            ToggleSpeed();

        UpdateWavePreview(wm);

        if (_livesText != null && _lm != null)
            _livesText.text = $"♥  {Mathf.Max(0, (int)_lm.lives)}";

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
            else if (wm.IsCountingDown)
                _waveText.text = $"Wave  {wm.CurrentWave} / {wm.TotalWaves}  —  Next in {Mathf.CeilToInt(wm.AutoStartCountdown)}s";
            else
                _waveText.text = $"Wave  {wm.CurrentWave} / {wm.TotalWaves}  —  Prep";
        }

        if (_waveButton != null && wm != null)
        {
            bool canSend = wm.CanStartWave || wm.IsCountingDown;
            _waveButton.interactable = canSend;
            if (_waveButtonLabel != null)
            {
                int next = wm.CurrentWave + 1;
                _waveButtonLabel.text = wm.IsWaveActive
                    ? "Wave in progress..."
                    : wm.IsCountingDown
                        ? $"SEND NOW  ({Mathf.CeilToInt(wm.AutoStartCountdown)}s)"
                        : wm.CurrentWave == 0
                            ? "START WAVE 1"
                            : $"SEND WAVE {next}";
            }
        }
    }
}
