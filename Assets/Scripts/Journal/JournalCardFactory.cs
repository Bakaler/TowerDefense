using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds one almanac card: sprite on top, name, then stat lines. Cards are
/// grid-cell sized (see CardSize). SetDiscovered supports the future
/// discovered-only journal: undiscovered cards show a silhouette and "???".
/// </summary>
public static class JournalCardFactory
{
    public static readonly Vector2 CardSize = new Vector2(300f, 360f);

    static readonly Color SilhouetteTint = new Color(0.04f, 0.04f, 0.06f, 1f);

    public class View
    {
        public GameObject Root;
        public Image      Sprite;
        public Image      Overlay;   // optional turret layer, may be null
        public Text       Name;
        public Text       Stats;
        string _name;
        string _stats;
        Color  _tint;

        public View(string name, string stats, Color tint) { _name = name; _stats = stats; _tint = tint; }

        public void SetDiscovered(bool discovered)
        {
            Sprite.color = discovered ? _tint : SilhouetteTint;
            if (Overlay != null) Overlay.color = discovered ? _tint : SilhouetteTint;
            Name.text    = discovered ? _name : "???";
            Stats.text   = discovered ? _stats : "Not yet encountered.";
        }
    }

    /// <summary>
    /// <paramref name="overlaySprite"/> composites a second layer (tower turret)
    /// over the base sprite, scaled together like the editor tooltip preview.
    /// <paramref name="tint"/> is the definition's tintColor (defaults to white).
    /// </summary>
    public static View Create(GameObject parent, string entryName, Sprite sprite,
        IEnumerable<string> statLines, bool discovered = true,
        Sprite overlaySprite = null, Color? tint = null)
    {
        var root = new GameObject($"Card_{entryName}");
        root.transform.SetParent(parent.transform, false);
        root.AddComponent<RectTransform>().sizeDelta = CardSize;
        root.AddComponent<Image>().color = UIControlFactory.PanelColor;

        float w = CardSize.x, h = CardSize.y;

        // Sprite — top center. With an overlay, both layers share one scale
        // (largest sprite fills the box) so base and turret stay proportional,
        // matching the editor tooltip preview.
        const float IMG = 110f;
        var frame = new GameObject("SpriteFrame");
        frame.transform.SetParent(root.transform, false);
        var fRT              = frame.AddComponent<RectTransform>();
        fRT.anchorMin        = fRT.anchorMax = new Vector2(0.5f, 1f);
        fRT.pivot            = new Vector2(0.5f, 1f);
        fRT.anchoredPosition = new Vector2(0f, -14f);
        fRT.sizeDelta        = new Vector2(IMG, IMG);

        float maxDim = 1f;
        if (sprite        != null) maxDim = Mathf.Max(maxDim, sprite.rect.width, sprite.rect.height);
        if (overlaySprite != null) maxDim = Mathf.Max(maxDim, overlaySprite.rect.width, overlaySprite.rect.height);
        float scale = IMG / maxDim;

        var view = new View(entryName, string.Join("\n", statLines), tint ?? Color.white)
        {
            Root   = root,
            Sprite = MakeLayer(frame, "Base", sprite != null ? sprite : RuntimeSprites.Circle(32),
                sprite != null ? scale : 0f, IMG),
        };
        if (overlaySprite != null)
            view.Overlay = MakeLayer(frame, "Overlay", overlaySprite, scale, IMG);

        // Name
        view.Name = UIControlFactory.Label(root, "Name", 0f, h * 0.5f - IMG - 14f - 22f, w - 16f, 36f,
            entryName, UIControlFactory.TitleColor, 22, TextAnchor.MiddleCenter, bold: true);

        // Stat block — remaining space
        float statsTop = IMG + 14f + 44f;
        var statsGO = new GameObject("Stats");
        statsGO.transform.SetParent(root.transform, false);
        var sRT       = statsGO.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0f);
        sRT.anchorMax = new Vector2(1f, 1f);
        sRT.offsetMin = new Vector2(18f, 10f);
        sRT.offsetMax = new Vector2(-18f, -statsTop);
        view.Stats = statsGO.AddComponent<Text>();
        view.Stats.font          = HUDHelpers.GetFont();
        view.Stats.fontSize      = 16;
        view.Stats.color         = UIControlFactory.TextColor;
        view.Stats.alignment     = TextAnchor.UpperLeft;
        view.Stats.lineSpacing   = 1.15f;
        view.Stats.raycastTarget = false;
        view.Stats.horizontalOverflow = HorizontalWrapMode.Wrap;
        view.Stats.verticalOverflow   = VerticalWrapMode.Truncate;

        view.SetDiscovered(discovered);
        return view;
    }

    /// <summary>Centered sprite layer inside the frame. scale &gt; 0 sizes by texel
    /// dimensions (shared-scale compositing); scale 0 fills the box.</summary>
    static Image MakeLayer(GameObject frame, string name, Sprite sprite, float scale, float boxSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(frame.transform, false);
        var rt       = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = scale > 0f
            ? new Vector2(sprite.rect.width * scale, sprite.rect.height * scale)
            : new Vector2(boxSize, boxSize);

        var img = go.AddComponent<Image>();
        img.sprite         = sprite;
        img.preserveAspect = true;
        img.raycastTarget  = false;
        return img;
    }
}
