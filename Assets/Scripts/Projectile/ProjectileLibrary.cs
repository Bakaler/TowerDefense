using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads projectiles.json and caches ProjectileDefinitions by id.
/// Static and lazy — no scene object required, safe to call from any effect or factory.
/// </summary>
public static class ProjectileLibrary
{
    const string DefinitionsPath = "Definitions/projectiles";

    static Dictionary<string, ProjectileDefinition> _cache;

    // Re-read the JSON on every play session even with Domain Reload disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => _cache = null;

    public static ProjectileDefinition Get(string id)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id)) return null;
        _cache.TryGetValue(id, out var def);
        return def;
    }

    public static bool TryGet(string id, out ProjectileDefinition def)
    {
        EnsureLoaded();
        def = null;
        return !string.IsNullOrEmpty(id) && _cache.TryGetValue(id, out def);
    }

    public static IReadOnlyCollection<ProjectileDefinition> All
    {
        get { EnsureLoaded(); return _cache.Values; }
    }

    static void EnsureLoaded()
    {
        if (_cache != null) return;
        _cache = new Dictionary<string, ProjectileDefinition>();

        var text = Resources.Load<TextAsset>(DefinitionsPath)?.text;
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogError($"[ProjectileLibrary] Could not load '{DefinitionsPath}'.");
            return;
        }

        var collection = JsonUtility.FromJson<ProjectileDefinitionCollection>(text);
        if (collection?.projectiles == null) return;

        foreach (var def in collection.projectiles)
            if (!string.IsNullOrEmpty(def.id))
                _cache[def.id] = def;

        Debug.Log($"[ProjectileLibrary] Loaded {_cache.Count} projectile(s).");
    }
}
