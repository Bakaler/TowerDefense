using UnityEngine;

/// <summary>About popup — placeholder copy for now.</summary>
public class AboutPopup : MenuScreen
{
    public override bool IsOverlay => true;

    protected override GameObject Build(GameObject canvasRoot)
    {
        var content = UIControlFactory.Popup(canvasRoot, "AboutPopup", "ABOUT", 760f, 520f, out var closeBtn);
        closeBtn.onClick.AddListener(() => Controller.Back());

        UIControlFactory.Label(content, "GameName", 0f, 170f, 600f, 60f,
            "Zen TD", UIControlFactory.TitleColor, 40, TextAnchor.MiddleCenter, bold: true);
        UIControlFactory.Label(content, "Version", 0f, 125f, 600f, 30f,
            $"Version {Application.version}", UIControlFactory.TextDim, 18);

        var body = UIControlFactory.Label(content, "Body", 0f, -40f, 640f, 260f,
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod " +
            "tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim " +
            "veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea " +
            "commodo consequat.\n\nDuis aute irure dolor in reprehenderit in voluptate " +
            "velit esse cillum dolore eu fugiat nulla pariatur.",
            UIControlFactory.TextColor, 20, TextAnchor.UpperCenter);
        body.verticalOverflow = VerticalWrapMode.Overflow;

        return content.transform.parent.parent.gameObject;   // the full-screen blocker root
    }
}
