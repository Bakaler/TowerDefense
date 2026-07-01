using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class HUDHelpers
{
    // ── Layout constants shared by all panels ─────────────────────────
    public const float RIGHT_W  = 172f;   // right column (shop + tier selector)
    public const float INFO_H   = 186f;   // bottom info panel height
    public const float INFO_W   = 980f;   // info panel width (centered, ~50% screen)
    public const float STATS_W  = 172f;   // left stats panel width
    public const float STATS_H  = 540f;   // left stats panel height (top half)
    public const float TIER_H   = 160f;   // tier selector height (top-right)

    static Font _font;

    public static Font GetFont()
    {
        if (_font != null) return _font;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _font;
    }

    // Absolute-positioned child rect (x,y from bottom-left of parent)
    public static GameObject MakeRect(string name, GameObject parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt          = go.AddComponent<RectTransform>();
        rt.anchorMin    = Vector2.zero;
        rt.anchorMax    = Vector2.zero;
        rt.pivot        = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta    = new Vector2(w, h);
        return go;
    }

    // Text component on an existing GO
    public static Text MakeText(GameObject go, string text, Color color, int size, bool bold = false)
    {
        var txt           = go.AddComponent<Text>();
        txt.text          = text;
        txt.color         = color;
        txt.font          = GetFont();
        txt.fontSize      = size;
        txt.fontStyle     = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.alignment     = TextAnchor.MiddleLeft;
        txt.raycastTarget = false;
        return txt;
    }

    // Stretch-anchor label (fills parent rect via anchorMin/anchorMax)
    public static Text AddLabel(GameObject parent, string goName,
        Vector2 anchorMin, Vector2 anchorMax,
        string content, Color color, int size, TextAnchor anchor,
        float leftPad = 0f, float rightPad = 0f, bool bold = false)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(leftPad, 0f);
        rt.offsetMax = new Vector2(-rightPad, 0f);
        var txt = go.AddComponent<Text>();
        txt.text          = content;
        txt.color         = color;
        txt.font          = GetFont();
        txt.fontSize      = size;
        txt.fontStyle     = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.alignment     = anchor;
        txt.raycastTarget = false;
        return txt;
    }

    // Absolute-positioned label with optional stretchX or fromRight modes
    public static Text AddAbsLabel(GameObject parent, string goName,
        float x, float y, float w, float h,
        string content, Color color, int size, TextAnchor anchor,
        bool stretchX = false, bool fromRight = false)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        if (stretchX)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.offsetMin = new Vector2(0f, y);
            rt.offsetMax = new Vector2(0f, y + h);
        }
        else if (fromRight)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.offsetMin = new Vector2(-w, y);
            rt.offsetMax = new Vector2(0f, y + h);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot     = Vector2.zero;
            rt.offsetMin = new Vector2(x, y);
            rt.offsetMax = new Vector2(x + w, y + h);
        }
        var txt = go.AddComponent<Text>();
        txt.text          = content;
        txt.color         = color;
        txt.font          = GetFont();
        txt.fontSize      = size;
        txt.alignment     = anchor;
        txt.raycastTarget = false;
        return txt;
    }

    // Styled button with centered label, returns (Button, label Text)
    public static (Button btn, Text lbl) MakeBtn(GameObject parent, string name,
        float x, float y, float w, float h,
        Color bgColor, string labelText, int fontSize, bool bold = false)
    {
        var go  = MakeRect(name, parent, x, y, w, h);
        var img = go.AddComponent<Image>(); img.color = bgColor;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var lbl = MakeText(MakeRect("L", go, 0f, 0f, w, h), labelText, Color.white, fontSize, bold);
        lbl.alignment = TextAnchor.MiddleCenter;
        return (btn, lbl);
    }

    // ── Balance helpers ───────────────────────────────────────────────

    public static float BalanceRatio(int count)
    {
        if (count <= 0) return 0f;
        if (count <= 4) return 1f;
        float r = 4f / count;
        return r * r;
    }

    public static TowerInfo[] GetTowerData(out Dictionary<string, int> idCounts)
    {
        var towers = Object.FindObjectsByType<TowerInfo>(FindObjectsSortMode.None);
        idCounts = new Dictionary<string, int>();
        foreach (var t in towers)
        {
            if (t.isGhost) continue;
            if (!idCounts.ContainsKey(t.definitionId)) idCounts[t.definitionId] = 0;
            idCounts[t.definitionId]++;
        }
        return towers;
    }
}
