using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared runtime sprite utilities: cached Resources loading (single sprites and
/// sliced sheets) plus procedural fallback shapes. Used by the projectile and minion
/// factories and launch effects so each system doesn't grow its own copy.
/// </summary>
public static class RuntimeSprites
{
    static readonly Dictionary<string, Sprite>   _spriteCache = new();
    static readonly Dictionary<string, Sprite[]> _sheetCache  = new();
    static readonly Dictionary<int, Sprite>      _circleCache = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCaches()
    {
        _spriteCache.Clear();
        _sheetCache.Clear();
        _circleCache.Clear();
    }

    /// <summary>Strips a leading "Resources/" — a common data typo; Resources paths are relative to the folder.</summary>
    public static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        return path.StartsWith("Resources/", System.StringComparison.OrdinalIgnoreCase)
            ? path.Substring("Resources/".Length)
            : path;
    }

    /// <summary>Loads a single sprite from Resources (falls back to the first cell of a sheet). Cached.</summary>
    public static Sprite Load(string path)
    {
        path = Normalize(path);
        if (string.IsNullOrEmpty(path)) return null;
        if (_spriteCache.TryGetValue(path, out var cached)) return cached;

        var sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
        {
            var sheet = LoadSheet(path);
            if (sheet.Length > 0) sprite = sheet[0];
        }
        _spriteCache[path] = sprite;
        return sprite;
    }

    /// <summary>Loads all sprites of a sliced sheet from Resources. Cached. Never null.</summary>
    public static Sprite[] LoadSheet(string path)
    {
        path = Normalize(path);
        if (string.IsNullOrEmpty(path)) return System.Array.Empty<Sprite>();
        if (_sheetCache.TryGetValue(path, out var cached)) return cached;

        var sheet = Resources.LoadAll<Sprite>(path) ?? System.Array.Empty<Sprite>();
        _sheetCache[path] = sheet;
        return sheet;
    }

    /// <summary>Loads one cell of a sliced sheet by index. Cached.</summary>
    public static Sprite FromSheet(string path, int index)
    {
        if (index < 0) return null;
        var sheet = LoadSheet(path);
        return index < sheet.Length ? sheet[index] : null;
    }

    /// <summary>
    /// Standard resolution order used by definition JSON: single spritePath first,
    /// then spriteSheet + spriteIndex. Returns null if neither resolves.
    /// </summary>
    public static Sprite Resolve(string spritePath, string spriteSheet, int spriteIndex)
    {
        var sprite = Load(spritePath);
        if (sprite != null) return sprite;
        return FromSheet(spriteSheet, spriteIndex);
    }

    /// <summary>Procedural white circle used as the universal fallback sprite. Cached by size.</summary>
    public static Sprite Circle(int size = 8)
    {
        if (_circleCache.TryGetValue(size, out var cached) && cached != null) return cached;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = size / 2f, r = size / 2f - 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c + 0.5f, dy = y - c + 0.5f;
                tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        _circleCache[size] = sprite;
        return sprite;
    }
}
