using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads minions.json and caches MinionDefinitions by id.
/// Static and lazy — no scene object required.
/// </summary>
public static class MinionLibrary
{
    const string DefinitionsPath = "Definitions/minions";

    static Dictionary<string, MinionDefinition> _cache;

    // Re-read the JSON on every play session even with Domain Reload disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => _cache = null;

    public static MinionDefinition Get(string id)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id)) return null;
        _cache.TryGetValue(id, out var def);
        return def;
    }

    public static bool TryGet(string id, out MinionDefinition def)
    {
        EnsureLoaded();
        def = null;
        return !string.IsNullOrEmpty(id) && _cache.TryGetValue(id, out def);
    }

    public static IReadOnlyCollection<MinionDefinition> All
    {
        get { EnsureLoaded(); return _cache.Values; }
    }

    static void EnsureLoaded()
    {
        if (_cache != null) return;
        _cache = new Dictionary<string, MinionDefinition>();

        var text = Resources.Load<TextAsset>(DefinitionsPath)?.text;
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogError($"[MinionLibrary] Could not load '{DefinitionsPath}'.");
            return;
        }

        var collection = JsonUtility.FromJson<MinionDefinitionCollection>(text);
        if (collection?.minions == null) return;

        foreach (var def in collection.minions)
            if (!string.IsNullOrEmpty(def.id))
                _cache[def.id] = def;

        Debug.Log($"[MinionLibrary] Loaded {_cache.Count} minion(s).");
    }
}
