using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads sounds.json and caches SoundDefinitions by id, plus the event → sound map.
/// Static and lazy — no scene object required.
/// </summary>
public static class SoundLibrary
{
    const string DefinitionsPath = "Definitions/sounds";

    static Dictionary<string, SoundDefinition> _cache;
    static Dictionary<string, string>          _events;

    // Re-read the JSON on every play session even with Domain Reload disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { _cache = null; _events = null; }

    /// <summary>Re-reads sounds.json on next access. Lets play-mode tools pick up edits live.</summary>
    public static void Reload() { _cache = null; _events = null; }

    public static SoundDefinition Get(string id)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(id)) return null;
        _cache.TryGetValue(id, out var def);
        return def;
    }

    public static bool TryGet(string id, out SoundDefinition def)
    {
        EnsureLoaded();
        def = null;
        return !string.IsNullOrEmpty(id) && _cache.TryGetValue(id, out def);
    }

    /// <summary>Sound id mapped to a named game event, or null if unmapped.</summary>
    public static string GetEventSound(string eventId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(eventId)) return null;
        _events.TryGetValue(eventId, out var soundId);
        return soundId;
    }

    public static IReadOnlyCollection<SoundDefinition> All
    {
        get { EnsureLoaded(); return _cache.Values; }
    }

    static void EnsureLoaded()
    {
        if (_cache != null) return;
        _cache  = new Dictionary<string, SoundDefinition>();
        _events = new Dictionary<string, string>();

        var text = Resources.Load<TextAsset>(DefinitionsPath)?.text;
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning($"[SoundLibrary] Could not load '{DefinitionsPath}' — audio disabled.");
            return;
        }

        var collection = JsonUtility.FromJson<SoundDefinitionCollection>(text);
        if (collection == null) return;

        if (collection.sounds != null)
            foreach (var def in collection.sounds)
                if (!string.IsNullOrEmpty(def.id))
                    _cache[def.id] = def;

        if (collection.events != null)
            foreach (var e in collection.events)
                if (!string.IsNullOrEmpty(e.eventId) && !string.IsNullOrEmpty(e.soundId))
                    _events[e.eventId] = e.soundId;

        Debug.Log($"[SoundLibrary] Loaded {_cache.Count} sound(s), {_events.Count} event mapping(s).");
    }
}
