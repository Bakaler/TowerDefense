using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen game-over and victory overlays.
/// </summary>
public class HUDOverlays : MonoBehaviour
{
    static readonly Color C_OverlayBg  = new Color(0.04f, 0.04f, 0.10f, 0.94f);
    static readonly Color C_GameOver   = new Color(0.95f, 0.22f, 0.22f, 1.00f);
    static readonly Color C_Victory    = new Color(0.28f, 0.90f, 0.42f, 1.00f);
    static readonly Color C_BtnRestart = new Color(0.18f, 0.46f, 0.88f, 1.00f);

    GameObject _gameOverPanel;
    GameObject _victoryPanel;
    bool       _victorySaved;

    public bool IsGameOver => _gameOverPanel != null && _gameOverPanel.activeSelf;
    public bool IsVictory  => _victoryPanel  != null && _victoryPanel.activeSelf;

    public void Build(GameObject canvasRoot)
    {
        _gameOverPanel = BuildOverlay(canvasRoot, "GameOverPanel", "GAME OVER", C_GameOver, isVictory: false);
        _victoryPanel  = BuildOverlay(canvasRoot, "VictoryPanel",  "VICTORY",   C_Victory,  isVictory: true);
        _gameOverPanel.SetActive(false);
        _victoryPanel.SetActive(false);
    }

    public void Tick()
    {
        var wm = WaveManager.Instance;
        if (wm == null) return;

        if (_gameOverPanel != null) _gameOverPanel.SetActive(wm.IsGameOver);
        if (_victoryPanel  != null)
        {
            _victoryPanel.SetActive(wm.IsVictory);
            if (wm.IsVictory && !_victorySaved)
            {
                _victorySaved = true;
                SaveManager.RecordVictory(LevelSelection.SelectedLevel, LevelSelection.SelectedDifficulty);
            }
        }
    }

    public void Reset()
    {
        _gameOverPanel?.SetActive(false);
        _victoryPanel?.SetActive(false);
        _victorySaved = false;
    }

    GameObject BuildOverlay(GameObject root, string goName, string title, Color titleColor, bool isVictory)
    {
        var panel = new GameObject(goName);
        panel.transform.SetParent(root.transform, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = C_OverlayBg;

        // Title
        var tGO = new GameObject("Title");
        tGO.transform.SetParent(panel.transform, false);
        var tRT = tGO.AddComponent<RectTransform>();
        tRT.anchorMin = tRT.anchorMax = tRT.pivot = new Vector2(0.5f, 0.5f);
        tRT.anchoredPosition = new Vector2(0f, 100f); tRT.sizeDelta = new Vector2(900f, 120f);
        var tTxt = tGO.AddComponent<Text>();
        tTxt.text = title; tTxt.color = titleColor; tTxt.font = HUDHelpers.GetFont();
        tTxt.fontSize = 80; tTxt.fontStyle = FontStyle.Bold; tTxt.alignment = TextAnchor.MiddleCenter;

        // Subtitle
        var sGO = new GameObject("Sub");
        sGO.transform.SetParent(panel.transform, false);
        var sRT = sGO.AddComponent<RectTransform>();
        sRT.anchorMin = sRT.anchorMax = sRT.pivot = new Vector2(0.5f, 0.5f);
        sRT.anchoredPosition = new Vector2(0f, 20f); sRT.sizeDelta = new Vector2(700f, 60f);
        var sTxt = sGO.AddComponent<Text>();
        sTxt.text = isVictory ? "All waves repelled!" : "Your base has been breached.";
        sTxt.color = new Color(0.8f, 0.8f, 0.85f); sTxt.font = HUDHelpers.GetFont();
        sTxt.fontSize = 28; sTxt.alignment = TextAnchor.MiddleCenter;

        float btnY = -70f;
        MakeOverlayBtn(panel, "RestartBtn", new Vector2(0f, btnY),
            isVictory ? "Play Again" : "Restart", C_BtnRestart, WaveManager.Restart);

        if (isVictory)
        {
            MakeOverlayBtn(panel, "NextBtn", new Vector2(0f, btnY - 80f),
                "Next Level", new Color(0.18f, 0.65f, 0.28f, 1f), GoNextLevel);
            btnY -= 80f;
        }

        MakeOverlayBtn(panel, "MenuBtn", new Vector2(0f, btnY - 80f),
            "Main Menu", new Color(0.30f, 0.30f, 0.36f, 1f),
            () => UnityEngine.SceneManagement.SceneManager.LoadScene("LandingScene"));

        return panel;
    }

    void MakeOverlayBtn(GameObject parent, string name, Vector2 pos, string label, Color color,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var bRT = go.AddComponent<RectTransform>();
        bRT.anchorMin = bRT.anchorMax = bRT.pivot = new Vector2(0.5f, 0.5f);
        bRT.anchoredPosition = pos; bRT.sizeDelta = new Vector2(280f, 62f);
        var img = go.AddComponent<Image>(); img.color = color;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        HUDHelpers.AddLabel(go, "Label", Vector2.zero, Vector2.one,
            label, Color.white, 26, TextAnchor.MiddleCenter, bold: true);
    }

    static void GoNextLevel()
    {
        int next = LevelSelection.SelectedLevel + 1;
        var ta   = Resources.Load<TextAsset>($"Definitions/Levels/level_{next}");
        if (ta == null) { UnityEngine.SceneManagement.SceneManager.LoadScene("LevelSelectionScene"); return; }
        LevelSelection.SelectedLevel = next;
        ModifierSelection.Clear();
        var data    = JsonUtility.FromJson<LevelData>(ta.text);
        bool hasMods = data?.modifierColumns != null && data.modifierColumns.Length > 0;
        UnityEngine.SceneManagement.SceneManager.LoadScene(hasMods ? "ModifierSelectScene" : "GameScene");
    }
}
