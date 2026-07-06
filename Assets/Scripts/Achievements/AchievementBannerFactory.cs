using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds one achievement banner: icon on the left; title + description on
/// the right. Three visual states — earned (full color), unearned (greyed
/// icon), hidden+unearned (blacked-out icon, masked text).
/// Used by both the achievements screen and the earn toast.
/// </summary>
public static class AchievementBannerFactory
{
    public const float Height = 110f;

    static readonly Color EarnedTint   = Color.white;
    static readonly Color UnearnedTint = new Color(0.38f, 0.38f, 0.42f, 1f);
    static readonly Color HiddenTint   = new Color(0.05f, 0.05f, 0.07f, 1f);
    static readonly Color EarnedBg     = new Color(0.10f, 0.14f, 0.10f, 1f);
    static readonly Color UnearnedBg   = new Color(0.08f, 0.09f, 0.12f, 1f);

    public class View
    {
        public GameObject Root;
        public Image      Background;
        public Image      Icon;
        public Text       Title;
        public Text       Description;
        AchievementDefinition _def;

        public View(AchievementDefinition def) => _def = def;

        public void SetEarned(bool earned)
        {
            bool masked = !earned && _def.hidden;

            Background.color = earned ? EarnedBg : UnearnedBg;
            Icon.color       = earned ? EarnedTint : masked ? HiddenTint : UnearnedTint;

            Title.text       = masked ? "???" : _def.title;
            Title.color      = earned ? UIControlFactory.TitleColor : UIControlFactory.TextDim;
            Description.text = masked ? "Hidden achievement — keep playing to reveal it." : _def.description;
            Description.color = earned ? UIControlFactory.TextColor : UIControlFactory.TextDim;
        }
    }

    /// <summary>
    /// Creates a banner under <paramref name="parent"/>. Sized for layout
    /// groups (LayoutElement preferredHeight set); pass an explicit width
    /// for free-floating use (toast).
    /// </summary>
    public static View Create(GameObject parent, AchievementDefinition def, float width = 0f)
    {
        var root = new GameObject($"Achv_{def.id}");
        root.transform.SetParent(parent.transform, false);
        var rt = root.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width > 0f ? width : 600f, Height);

        var layout = root.AddComponent<LayoutElement>();
        layout.preferredHeight = Height;
        layout.minHeight       = Height;

        var view = new View(def)
        {
            Root       = root,
            Background = root.AddComponent<Image>(),
        };

        // Icon — left, vertically centered
        const float ICON = 78f, PAD = 16f;
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(root.transform, false);
        var iRT              = iconGO.AddComponent<RectTransform>();
        iRT.anchorMin        = new Vector2(0f, 0.5f);
        iRT.anchorMax        = new Vector2(0f, 0.5f);
        iRT.pivot            = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(PAD, 0f);
        iRT.sizeDelta        = new Vector2(ICON, ICON);
        view.Icon = iconGO.AddComponent<Image>();
        var sprite = RuntimeSprites.Resolve(def.iconPath, def.iconSheet, def.iconIndex);
        view.Icon.sprite         = sprite != null ? sprite : RuntimeSprites.Circle(32);
        view.Icon.preserveAspect = true;
        view.Icon.raycastTarget  = false;

        // Title + description — right of the icon, stretched to banner width
        float textX = PAD + ICON + PAD;
        view.Title       = StretchLabel(root, "Title", textX, 0.52f, 1f, 28, TextAnchor.LowerLeft, bold: true);
        view.Description = StretchLabel(root, "Desc",  textX, 0.08f, 0.5f, 18, TextAnchor.UpperLeft, bold: false);

        view.SetEarned(AchievementManager.Instance != null && AchievementManager.Instance.IsEarned(def.id));
        return view;
    }

    static Text StretchLabel(GameObject parent, string name, float leftPad,
        float anchorYMin, float anchorYMax, int size, TextAnchor anchor, bool bold)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt       = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, anchorYMin);
        rt.anchorMax = new Vector2(1f, anchorYMax);
        rt.offsetMin = new Vector2(leftPad, 0f);
        rt.offsetMax = new Vector2(-12f, 0f);

        var txt = go.AddComponent<Text>();
        txt.font          = HUDHelpers.GetFont();
        txt.fontSize      = size;
        txt.fontStyle     = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.alignment     = anchor;
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow   = VerticalWrapMode.Truncate;
        return txt;
    }
}
