using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scrollable list of achievement banners. States refresh on every open so
/// achievements earned mid-session show correctly.
/// </summary>
public class AchievementsScreen : MenuScreen
{
    readonly List<(string id, AchievementBannerFactory.View view)> _views = new();

    protected override GameObject Build(GameObject canvasRoot)
    {
        var panel = UIControlFactory.Rect("AchievementsScreen", canvasRoot, 0f, 0f, 1920f, 1080f);

        UIControlFactory.Label(panel, "Title", 0f, 420f, 800f, 80f,
            "ACHIEVEMENTS", UIControlFactory.TitleColor, 52, TextAnchor.MiddleCenter, bold: true);

        var content = UIScrollListFactory.VerticalList(panel, "List", 0f, -30f, 860f, 720f);

        var mgr = AchievementManager.Instance;
        if (mgr != null)
            foreach (var def in mgr.All)
                _views.Add((def.id, AchievementBannerFactory.Create(content, def)));
        else
            UIControlFactory.Label(panel, "NoMgr", 0f, 0f, 700f, 40f,
                "Achievement system unavailable.", UIControlFactory.TextDim, 22);

        var (backBtn, _) = UIControlFactory.Button(panel, "BackBtn", 0f, -470f, 220f, 52f,
            new Color(0.28f, 0.12f, 0.12f, 1f), "BACK", 22);
        backBtn.onClick.AddListener(() => Controller.Back());

        return panel;
    }

    protected override void Refresh()
    {
        var mgr = AchievementManager.Instance;
        if (mgr == null) return;
        foreach (var (id, view) in _views)
            view.SetEarned(mgr.IsEarned(id));
    }
}
