using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scrollable containers for menu screens: a vertical list (achievement
/// banners) and a grid (journal cards). Returns the content GameObject —
/// children added to it are laid out and sized automatically.
/// </summary>
public static class UIScrollListFactory
{
    /// <summary>
    /// Vertical scroll list. Children are stacked top-down with the given
    /// spacing; child heights come from each child's LayoutElement
    /// (or defaultChildHeight if none).
    /// </summary>
    public static GameObject VerticalList(GameObject parent, string name,
        float x, float y, float w, float h, float spacing = 10f, float padding = 12f)
    {
        var content = BuildScrollRoot(parent, name, x, y, w, h, out _);

        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing                = spacing;
        layout.padding                = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
        layout.childAlignment         = TextAnchor.UpperCenter;
        layout.childControlWidth      = true;
        layout.childControlHeight     = false;
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return content;
    }

    /// <summary>Grid scroll list with fixed cell size, filling left-to-right, top-down.</summary>
    public static GameObject Grid(GameObject parent, string name,
        float x, float y, float w, float h, Vector2 cellSize, float spacing = 14f, float padding = 12f)
    {
        var content = BuildScrollRoot(parent, name, x, y, w, h, out _);

        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize       = cellSize;
        grid.spacing        = new Vector2(spacing, spacing);
        grid.padding        = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
        grid.startCorner    = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis      = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return content;
    }

    /// <summary>Removes all children of a list/grid content (for rebuilds).</summary>
    public static void Clear(GameObject content)
    {
        if (content == null) return;
        for (int i = content.transform.childCount - 1; i >= 0; i--)
            Object.Destroy(content.transform.GetChild(i).gameObject);
    }

    // ── Internals ─────────────────────────────────────────────────────

    /// <summary>ScrollRect + masked viewport + top-anchored content, mouse-wheel scrollable.</summary>
    static GameObject BuildScrollRoot(GameObject parent, string name,
        float x, float y, float w, float h, out ScrollRect scroll)
    {
        var root = UIControlFactory.Rect(name, parent, x, y, w, h);
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        scroll = root.AddComponent<ScrollRect>();

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(root.transform, false);
        var vpRT       = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        vpRT.pivot     = new Vector2(0.5f, 1f);
        viewport.AddComponent<Image>().color = Color.clear;   // raycast target for drag scrolling
        viewport.AddComponent<RectMask2D>();

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var cRT       = content.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0f, 1f);
        cRT.anchorMax = new Vector2(1f, 1f);
        cRT.pivot     = new Vector2(0.5f, 1f);
        cRT.offsetMin = Vector2.zero;
        cRT.offsetMax = Vector2.zero;

        scroll.viewport         = vpRT;
        scroll.content          = cRT;
        scroll.horizontal       = false;
        scroll.vertical         = true;
        scroll.movementType     = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        return content;
    }
}
