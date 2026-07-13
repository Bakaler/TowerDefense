using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
using System.Text;
#endif

/// Bright tab icons + header strips so our custom editor windows stand out
/// from Unity's default gray tabs. The dock tab background itself can't be
/// recolored, so we put a glowing neon dot in the tab's titleContent and
/// draw a matching strip across the top of the window.
static class NeonTab
{
    public static readonly Color Cyan    = new(0f, 1f, 1f);
    public static readonly Color Magenta = new(1f, 0.15f, 0.85f);
    public static readonly Color Lime    = new(0.55f, 1f, 0f);
    public static readonly Color Yellow  = new(1f, 0.92f, 0f);

    static readonly Dictionary<Color, Texture2D> _icons = new();

    /// Tab title with a neon dot icon — assign to EditorWindow.titleContent.
    public static GUIContent Title(string text, Color color) => new(text, Icon(color));

    /// Call first in OnGUI to draw a neon accent strip across the top.
    public static void DrawStrip(Color color, float height = 3f)
    {
        var r = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, color);
    }

    /// Tints the native OS title bar (the strip with the minimize/maximize/
    /// close buttons) of a floating editor window. Finds the top-level window
    /// in this process whose caption matches the tab title, so it silently
    /// does nothing while the window is docked. Windows 11+ only.
    public static void ColorTitleBar(string windowTitle, Color color)
    {
#if UNITY_EDITOR_WIN
        uint pid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        var sb = new StringBuilder(256);
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint winPid);
            if (winPid != pid || !IsWindowVisible(hwnd)) return true;

            sb.Clear();
            GetWindowText(hwnd, sb, sb.Capacity);
            if (sb.ToString() != windowTitle) return true;

            uint caption = (uint)(Mathf.RoundToInt(color.r * 255f)
                                | Mathf.RoundToInt(color.g * 255f) << 8
                                | Mathf.RoundToInt(color.b * 255f) << 16);
            uint text = 0x00000000;   // black caption text for contrast on neon
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(uint));
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR,    ref text,    sizeof(uint));
            return true;
        }, IntPtr.Zero);
#endif
    }

#if UNITY_EDITOR_WIN
    const int DWMWA_CAPTION_COLOR = 35;
    const int DWMWA_TEXT_COLOR    = 36;

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int size);
#endif

    static Texture2D Icon(Color color)
    {
        if (_icons.TryGetValue(color, out var tex) && tex != null)
            return tex;

        const int size = 16;
        tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags  = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
        };

        // Solid dot with a feathered rim so it reads as a glow at tab size.
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
            float a = Mathf.Clamp01((7f - d) / 2.5f);
            tex.SetPixel(x, y, new Color(color.r, color.g, color.b, a));
        }
        tex.Apply();

        _icons[color] = tex;
        return tex;
    }
}
