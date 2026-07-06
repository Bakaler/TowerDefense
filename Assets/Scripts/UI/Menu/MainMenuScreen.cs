using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Root menu screen: title, the four navigation buttons, and the active
/// profile footer with a switch-profile shortcut back to the landing scene.
/// </summary>
public class MainMenuScreen : MenuScreen
{
    UnityEngine.UI.Text _profileLabel;

    protected override GameObject Build(GameObject canvasRoot)
    {
        var panel = UIControlFactory.Rect("MainMenuScreen", canvasRoot, 0f, 0f, 1920f, 1080f);

        UIControlFactory.Label(panel, "Title", 0f, 330f, 900f, 160f,
            "Zen TD", UIControlFactory.TitleColor, 96, TextAnchor.MiddleCenter, bold: true);
        UIControlFactory.Label(panel, "Subtitle", 0f, 235f, 600f, 50f,
            "Balance", UIControlFactory.TextDim, 28);

        const float BTN_W = 340f, BTN_H = 72f, GAP = 22f;
        float y = 90f;

        var (playBtn, _) = UIControlFactory.Button(panel, "PlayBtn", 0f, y, BTN_W, BTN_H,
            UIControlFactory.ButtonGreen, "PLAY", 34);
        playBtn.onClick.AddListener(() => SceneManager.LoadScene(Controller.levelSelectSceneName));
        y -= BTN_H + GAP;

        var (settingsBtn, _) = UIControlFactory.Button(panel, "SettingsBtn", 0f, y, BTN_W, BTN_H,
            UIControlFactory.ButtonColor, "SETTINGS", 28);
        settingsBtn.onClick.AddListener(() => Controller.Push(Controller.Get<SettingsScreen>()));
        y -= BTN_H + GAP;

        var (achieveBtn, _) = UIControlFactory.Button(panel, "AchievementsBtn", 0f, y, BTN_W, BTN_H,
            UIControlFactory.ButtonColor, "ACHIEVEMENTS", 28);
        achieveBtn.onClick.AddListener(() => Controller.Push(Controller.Get<AchievementsScreen>()));
        y -= BTN_H + GAP;

        var (journalBtn, _) = UIControlFactory.Button(panel, "JournalBtn", 0f, y, BTN_W, BTN_H,
            UIControlFactory.ButtonColor, "JOURNAL", 28);
        journalBtn.onClick.AddListener(() => Controller.Push(Controller.Get<JournalScreen>()));

        // Profile footer
        _profileLabel = UIControlFactory.Label(panel, "ProfileLabel", 0f, -440f, 700f, 34f,
            "", UIControlFactory.TextDim, 20);
        var (switchBtn, _) = UIControlFactory.Button(panel, "SwitchProfileBtn", 0f, -490f, 220f, 44f,
            new Color(0.22f, 0.22f, 0.30f, 1f), "SWITCH PROFILE", 16);
        switchBtn.onClick.AddListener(() => SceneManager.LoadScene(Controller.landingSceneName));

        return panel;
    }

    protected override void Refresh()
    {
        if (_profileLabel != null)
            _profileLabel.text = $"Profile:  {SaveManager.ActiveProfileName()}   ·   ★ {SaveManager.TotalStarsAllLevels()}";
    }
}
