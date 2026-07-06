using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds the screen-space canvas scaffolding every menu screen needs:
/// canvas + scaler (1920x1080 reference), raycaster, optional full-screen
/// background, and a guaranteed EventSystem. Screens compose their content
/// with UIControlFactory / UIScrollListFactory on top of this root.
/// </summary>
public static class UICanvasFactory
{
    public static readonly Color MenuBackground = new Color(0.05f, 0.05f, 0.10f, 1f);

    /// <summary>Creates a ScreenSpaceOverlay canvas root. No background image.</summary>
    public static GameObject CreateCanvas(string name, int sortingOrder = 10)
    {
        var go = new GameObject(name);

        var canvas          = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler                 = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
        return go;
    }

    /// <summary>Canvas root with a full-screen background color.</summary>
    public static GameObject CreateCanvasWithBackground(string name, Color bgColor, int sortingOrder = 10)
    {
        var root = CreateCanvas(name, sortingOrder);
        AddFullScreenImage(root, "BG", bgColor).raycastTarget = false;
        return root;
    }

    /// <summary>Adds a child image stretched to fill the parent rect.</summary>
    public static Image AddFullScreenImage(GameObject parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt       = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img   = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    /// <summary>Creates an EventSystem if the scene has none.</summary>
    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}
