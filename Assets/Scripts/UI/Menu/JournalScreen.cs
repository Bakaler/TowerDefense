using UnityEngine;

/// <summary>
/// Journal hub: choose between the Towers and Enemies almanacs.
/// </summary>
public class JournalScreen : MenuScreen
{
    protected override GameObject Build(GameObject canvasRoot)
    {
        var panel = UIControlFactory.Rect("JournalScreen", canvasRoot, 0f, 0f, 1920f, 1080f);

        UIControlFactory.Label(panel, "Title", 0f, 380f, 700f, 80f,
            "JOURNAL", UIControlFactory.TitleColor, 52, TextAnchor.MiddleCenter, bold: true);

        var (towersBtn, _) = UIControlFactory.Button(panel, "TowersBtn", -220f, 40f, 360f, 200f,
            new Color(0.12f, 0.25f, 0.18f, 1f), "TOWERS", 36);
        towersBtn.onClick.AddListener(() => OpenAlmanac(JournalAlmanacScreen.Mode.Towers));

        var (enemiesBtn, _) = UIControlFactory.Button(panel, "EnemiesBtn", 220f, 40f, 360f, 200f,
            new Color(0.28f, 0.12f, 0.12f, 1f), "ENEMIES", 36);
        enemiesBtn.onClick.AddListener(() => OpenAlmanac(JournalAlmanacScreen.Mode.Enemies));

        var (backBtn, _) = UIControlFactory.Button(panel, "BackBtn", 0f, -280f, 220f, 52f,
            new Color(0.28f, 0.12f, 0.12f, 1f), "BACK", 22);
        backBtn.onClick.AddListener(() => Controller.Back());

        return panel;
    }

    void OpenAlmanac(JournalAlmanacScreen.Mode mode)
    {
        var almanac = Controller.Get<JournalAlmanacScreen>();
        almanac.SetMode(mode);
        Controller.Push(almanac);
    }
}
