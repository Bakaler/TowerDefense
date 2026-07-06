using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings screen: one volume slider per AudioManager bus (persisted by the
/// AudioManager itself via PlayerPrefs) and an About popup.
/// </summary>
public class SettingsScreen : MenuScreen
{
    static readonly (string bus, string label)[] BusLabels =
    {
        ("master",  "Master"),
        ("music",   "Music"),
        ("combat",  "Combat"),
        ("ui",      "UI"),
        ("ambient", "Ambient"),
    };

    readonly System.Collections.Generic.List<(string bus, Slider slider)> _sliders = new();

    protected override GameObject Build(GameObject canvasRoot)
    {
        var panel = UIControlFactory.Rect("SettingsScreen", canvasRoot, 0f, 0f, 1920f, 1080f);

        UIControlFactory.Label(panel, "Title", 0f, 380f, 700f, 80f,
            "SETTINGS", UIControlFactory.TitleColor, 52, TextAnchor.MiddleCenter, bold: true);

        UIControlFactory.Label(panel, "VolumeHeader", 0f, 280f, 600f, 40f,
            "— VOLUME —", UIControlFactory.TextDim, 22);

        const float ROW_H = 64f;
        const float LBL_W = 220f, SLD_W = 420f, VAL_W = 90f;
        float y = 200f;

        foreach (var (bus, label) in BusLabels)
        {
            UIControlFactory.Label(panel, $"Lbl_{bus}", -280f, y, LBL_W, 40f,
                label, UIControlFactory.TextColor, 26, TextAnchor.MiddleRight);

            var slider   = UIControlFactory.HorizontalSlider(panel, $"Sld_{bus}", 60f, y, SLD_W, 40f,
                AudioManager.GetBusVolume(bus));
            var valueLbl = UIControlFactory.Label(panel, $"Val_{bus}", 340f, y, VAL_W, 40f,
                PercentText(slider.value), UIControlFactory.TextDim, 22, TextAnchor.MiddleLeft);

            string busId = bus;
            slider.onValueChanged.AddListener(v =>
            {
                AudioManager.SetBusVolume(busId, v);
                valueLbl.text = PercentText(v);
            });

            _sliders.Add((bus, slider));
            y -= ROW_H;
        }

        var (aboutBtn, _) = UIControlFactory.Button(panel, "AboutBtn", 0f, y - 40f, 280f, 56f,
            UIControlFactory.ButtonColor, "ABOUT", 24);
        aboutBtn.onClick.AddListener(() => Controller.Push(Controller.Get<AboutPopup>()));

        var (backBtn, _) = UIControlFactory.Button(panel, "BackBtn", 0f, -440f, 220f, 52f,
            new Color(0.28f, 0.12f, 0.12f, 1f), "BACK", 22);
        backBtn.onClick.AddListener(() => Controller.Back());

        return panel;
    }

    protected override void Refresh()
    {
        // Re-sync in case volumes changed elsewhere (e.g. in-game debug panel)
        foreach (var (bus, slider) in _sliders)
            slider.SetValueWithoutNotify(AudioManager.GetBusVolume(bus));
    }

    static string PercentText(float v) => $"{Mathf.RoundToInt(v * 100f)}%";
}
