using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic menu-UI primitives: rects, panels, labels, buttons, icons,
/// sliders, and popup frames. Center-anchored coordinates (0,0 = parent
/// center), matching the menu screens' layout style. Feature-specific
/// composition (achievement banners, journal cards) lives with the feature.
/// </summary>
public static class UIControlFactory
{
    // ── Shared palette ────────────────────────────────────────────────
    public static readonly Color PanelColor    = new Color(0.08f, 0.10f, 0.15f, 1f);
    public static readonly Color PanelDark     = new Color(0.06f, 0.06f, 0.08f, 1f);
    public static readonly Color TitleColor    = new Color(0.95f, 0.88f, 0.55f, 1f);
    public static readonly Color TextColor     = new Color(0.85f, 0.85f, 0.90f, 1f);
    public static readonly Color TextDim       = new Color(0.55f, 0.55f, 0.65f, 1f);
    public static readonly Color ButtonColor   = new Color(0.16f, 0.24f, 0.38f, 1f);
    public static readonly Color ButtonGreen   = new Color(0.18f, 0.55f, 0.22f, 1f);
    public static readonly Color ButtonRed     = new Color(0.55f, 0.12f, 0.12f, 1f);

    static Font Font => HUDHelpers.GetFont();

    // ── Rects & panels ────────────────────────────────────────────────

    /// <summary>Center-anchored child rect. (0,0) = parent center.</summary>
    public static GameObject Rect(string name, GameObject parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt              = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);
        return go;
    }

    /// <summary>Center-anchored rect with a background image.</summary>
    public static GameObject Panel(string name, GameObject parent, float x, float y, float w, float h, Color color)
    {
        var go = Rect(name, parent, x, y, w, h);
        go.AddComponent<Image>().color = color;
        return go;
    }

    // ── Text ──────────────────────────────────────────────────────────

    public static Text Label(GameObject parent, string name, float x, float y, float w, float h,
        string content, Color color, int size,
        TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false)
    {
        var go  = Rect(name, parent, x, y, w, h);
        var txt = go.AddComponent<Text>();
        txt.text          = content;
        txt.font          = Font;
        txt.fontSize      = size;
        txt.fontStyle     = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.color         = color;
        txt.alignment     = anchor;
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow   = VerticalWrapMode.Truncate;
        return txt;
    }

    // ── Buttons ───────────────────────────────────────────────────────

    /// <summary>Styled button with a centered label. Plays ui_click.</summary>
    public static (Button btn, Text lbl) Button(GameObject parent, string name,
        float x, float y, float w, float h,
        Color bgColor, string labelText, int fontSize, bool bold = true)
    {
        var go  = Rect(name, parent, x, y, w, h);
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var cols = btn.colors;
        cols.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f) * 1.3f;
        cols.pressedColor     = bgColor * 0.7f;
        cols.disabledColor    = new Color(0.20f, 0.20f, 0.24f, 1f);
        btn.colors = cols;

        var lbl = Label(go, "Label", 0f, 0f, w - 8f, h, labelText, Color.white, fontSize, TextAnchor.MiddleCenter, bold);
        btn.onClick.AddListener(() => AudioManager.PlayEvent("ui_click"));
        return (btn, lbl);
    }

    // ── Icons ─────────────────────────────────────────────────────────

    /// <summary>Sprite image sized to fit; falls back to a flat colored square when sprite is null.</summary>
    public static Image Icon(GameObject parent, string name, float x, float y, float size,
        Sprite sprite, Color fallbackColor)
    {
        var go  = Rect(name, parent, x, y, size, size);
        var img = go.AddComponent<Image>();
        if (sprite != null)
        {
            img.sprite         = sprite;
            img.preserveAspect = true;
        }
        else
        {
            img.color = fallbackColor;
        }
        img.raycastTarget = false;
        return img;
    }

    // ── Slider ────────────────────────────────────────────────────────

    /// <summary>
    /// Horizontal 0..1 slider built from primitives (track, fill, handle).
    /// </summary>
    public static Slider HorizontalSlider(GameObject parent, string name,
        float x, float y, float w, float h, float initialValue)
    {
        var root   = Rect(name, parent, x, y, w, h);
        var slider = root.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        float trackH = Mathf.Min(12f, h * 0.4f);

        // Track (background)
        var track = new GameObject("Track");
        track.transform.SetParent(root.transform, false);
        var trackRT       = track.AddComponent<RectTransform>();
        trackRT.anchorMin = new Vector2(0f, 0.5f);
        trackRT.anchorMax = new Vector2(1f, 0.5f);
        trackRT.offsetMin = new Vector2(0f, -trackH * 0.5f);
        trackRT.offsetMax = new Vector2(0f,  trackH * 0.5f);
        track.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 1f);

        // Fill area + fill
        var fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(root.transform, false);
        var faRT       = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.5f);
        faRT.anchorMax = new Vector2(1f, 0.5f);
        faRT.offsetMin = new Vector2(0f, -trackH * 0.5f);
        faRT.offsetMax = new Vector2(0f,  trackH * 0.5f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillRT       = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        var fillImg   = fill.AddComponent<Image>();
        fillImg.color = new Color(0.35f, 0.60f, 0.95f, 1f);

        // Handle area + handle
        var handleArea = new GameObject("HandleArea");
        handleArea.transform.SetParent(root.transform, false);
        var haRT       = handleArea.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero;
        haRT.anchorMax = Vector2.one;
        haRT.offsetMin = Vector2.zero;
        haRT.offsetMax = Vector2.zero;

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var handleRT       = handle.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(h * 0.7f, h * 0.7f);
        var handleImg   = handle.AddComponent<Image>();
        handleImg.color = new Color(0.85f, 0.85f, 0.92f, 1f);

        slider.targetGraphic = handleImg;
        slider.fillRect      = fillRT;
        slider.handleRect    = handleRT;
        slider.SetValueWithoutNotify(Mathf.Clamp01(initialValue));
        return slider;
    }

    // ── Popup frame ───────────────────────────────────────────────────

    /// <summary>
    /// Modal popup: dim full-screen blocker + centered framed panel with a
    /// title bar and close (×) button. Returns the content panel to fill.
    /// </summary>
    public static GameObject Popup(GameObject canvasRoot, string name, string title,
        float w, float h, out Button closeBtn)
    {
        var blocker = new GameObject(name);
        blocker.transform.SetParent(canvasRoot.transform, false);
        var bRT       = blocker.AddComponent<RectTransform>();
        bRT.anchorMin = Vector2.zero;
        bRT.anchorMax = Vector2.one;
        bRT.offsetMin = Vector2.zero;
        bRT.offsetMax = Vector2.zero;
        var bImg   = blocker.AddComponent<Image>();
        bImg.color = new Color(0f, 0f, 0f, 0.6f);   // dim + swallow clicks

        var frame = Panel("Frame", blocker, 0f, 0f, w, h, PanelColor);

        const float HDR = 52f;
        Panel("TitleBar", frame, 0f, h * 0.5f - HDR * 0.5f, w, HDR, PanelDark);
        Label(frame, "Title", 0f, h * 0.5f - HDR * 0.5f, w - HDR * 2f, HDR, title, TitleColor, 26, TextAnchor.MiddleCenter, bold: true);

        (closeBtn, _) = Button(frame, "Close", w * 0.5f - HDR * 0.5f, h * 0.5f - HDR * 0.5f,
            HDR - 8f, HDR - 8f, ButtonRed, "×", 26);

        var content = Rect("Content", frame, 0f, -HDR * 0.5f, w, h - HDR);
        return content;
    }
}
