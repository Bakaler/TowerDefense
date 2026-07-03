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

    Button _waveButton;
    Text   _waveButtonLabel;
    Button _pauseButton;
    Text   _pauseButtonLabel;
    Text   _waveText;
    Text   _livesText;
    bool   _paused;

    public bool Paused => _paused;

    public void Build(GameObject canvasRoot)
    {
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

        // Fast-forward button (right of pause)
        float ffX = PAD + (W - PAD * 2f - PW - 4f) + 4f;
        var (ffBtn, _) = HUDHelpers.MakeBtn(panel, "FFBtn", ffX, PAD, PW, BTNH,
            new Color(0.20f, 0.30f, 0.22f, 1f), "▶▶", 14, bold: true);
        ffBtn.onClick.AddListener(() => Time.timeScale = _paused ? 0f : (Time.timeScale >= 2f ? 1f : 2f));
    }

    public void ResetPause()
    {
        _paused = false;
        Time.timeScale = 1f;
        AudioManager.SetSfxPaused(false);
        if (_pauseButtonLabel != null) _pauseButtonLabel.text = "II";
    }

    void TogglePause()
    {
        _paused = !_paused;
        Time.timeScale = _paused ? 0f : 1f;
        AudioManager.SetSfxPaused(_paused);   // SFX pause with the game; music keeps playing
        if (_pauseButtonLabel != null) _pauseButtonLabel.text = _paused ? "▶" : "II";
    }

    LogicManager _lm;
    void Start() => _lm = LogicManager.Instance;

    void Update()
    {
        var wm = WaveManager.Instance;

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
