using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Escape pause menu: resume, volume sliders, quit to main menu.
/// Pauses via HUDWaveBar so the wave bar's pause button stays in sync and the
/// chosen game speed survives the pause. Escape is ignored while placing a
/// tower (TowerPlacer uses it to cancel) and while an end overlay is up.
/// </summary>
public class HUDPauseMenu : MonoBehaviour
{
    public string mainMenuSceneName = "MainMenuScene";

    GameObject _canvasRoot;
    GameObject _panel;
    readonly List<(string bus, Slider slider)> _sliders = new();

    static readonly (string bus, string label)[] BusLabels =
    {
        ("master",  "Master"),
        ("music",   "Music"),
        ("combat",  "Combat"),
        ("ui",      "UI"),
        ("ambient", "Ambient"),
    };

    public bool IsOpen => _panel != null && _panel.activeSelf;

    public void Build(GameObject canvasRoot) => _canvasRoot = canvasRoot;   // panel built lazily

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        var wm = WaveManager.Instance;
        if (wm != null && (wm.IsGameOver || wm.IsVictory)) return;
        // Escape cancels tower placement first — don't also open the menu
        if (!IsOpen && TowerPlacer.Instance != null && TowerPlacer.Instance.IsPlacing) return;

        if (IsOpen) CloseMenu();
        else        OpenMenu();
    }

    public void OpenMenu()
    {
        if (_panel == null) BuildPanel();
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();   // above every other HUD element
        foreach (var (bus, slider) in _sliders)
            slider.SetValueWithoutNotify(AudioManager.GetBusVolume(bus));
        GameHUD.Instance?.WaveBar?.SetPaused(true);
    }

    public void CloseMenu()
    {
        _panel?.SetActive(false);
        GameHUD.Instance?.WaveBar?.SetPaused(false);
    }

    /// <summary>Hides the panel without touching pause state (level reload path).</summary>
    public void HideImmediate() => _panel?.SetActive(false);

    // ── Build ─────────────────────────────────────────────────────────

    void BuildPanel()
    {
        _panel = new GameObject("PauseMenu");
        _panel.transform.SetParent(_canvasRoot.transform, false);
        var rt       = _panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);   // dim + swallow clicks

        var box = UIControlFactory.Panel("Box", _panel, 0f, 0f, 520f, 660f, UIControlFactory.PanelColor);

        UIControlFactory.Label(box, "Title", 0f, 280f, 400f, 60f,
            "PAUSED", UIControlFactory.TitleColor, 44, TextAnchor.MiddleCenter, bold: true);

        var (resumeBtn, _) = UIControlFactory.Button(box, "ResumeBtn", 0f, 200f, 360f, 60f,
            UIControlFactory.ButtonGreen, "RESUME", 26);
        resumeBtn.onClick.AddListener(CloseMenu);

        // Volume sliders
        UIControlFactory.Label(box, "VolHeader", 0f, 130f, 360f, 30f,
            "— VOLUME —", UIControlFactory.TextDim, 18);

        const float ROW_H = 52f;
        float y = 80f;
        foreach (var (bus, label) in BusLabels)
        {
            UIControlFactory.Label(box, $"Lbl_{bus}", -160f, y, 120f, 34f,
                label, UIControlFactory.TextColor, 20, TextAnchor.MiddleRight);
            var slider = UIControlFactory.HorizontalSlider(box, $"Sld_{bus}", 55f, y, 270f, 34f,
                AudioManager.GetBusVolume(bus));
            string busId = bus;
            slider.onValueChanged.AddListener(v => AudioManager.SetBusVolume(busId, v));
            _sliders.Add((bus, slider));
            y -= ROW_H;
        }

        var (restartBtn, _) = UIControlFactory.Button(box, "RestartBtn", 0f, y - 30f, 360f, 60f,
            new Color(0.18f, 0.46f, 0.88f, 1f), "RESTART LEVEL", 24);
        restartBtn.onClick.AddListener(() =>
        {
            RunStats.FlushToProfile();                 // abandoned runs still count toward lifetime stats
            GameHUD.Instance?.WaveBar?.ResetPause();   // timeScale 1, SFX unpaused
            HideImmediate();
            WaveManager.Restart();
        });

        var (quitBtn, _) = UIControlFactory.Button(box, "QuitBtn", 0f, y - 100f, 360f, 60f,
            new Color(0.28f, 0.12f, 0.12f, 1f), "QUIT TO MENU", 24);
        quitBtn.onClick.AddListener(() =>
        {
            RunStats.FlushToProfile();                 // abandoned runs still count toward lifetime stats
            GameHUD.Instance?.WaveBar?.ResetPause();   // timeScale 1, SFX unpaused
            ScreenFader.LoadScene(mainMenuSceneName);
        });
    }
}
