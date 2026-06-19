using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps string keys to MonoBehaviour component types for use by the factory system.
/// Also stores optional dependency declarations so the factory can auto-resolve
/// required sibling components without listing them in every definition.
///
/// Registration — call from a [RuntimeInitializeOnLoadMethod] static method inside
/// your component class so the registry is populated before any factory runs:
///
///   [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
///   static void Register() =>
///       ComponentRegistry.Register("unit_stats", typeof(UnitStatsComponent));
///
/// With dependencies:
///   ComponentRegistry.Register("armored", typeof(ArmoredComponent),
///       requires: new[] { "unit_stats" });
/// </summary>
public static class ComponentRegistry
{
    private static readonly Dictionary<string, Type>     _registry = new();
    private static readonly Dictionary<string, string[]> _requires = new();

    public static void Register(string key, Type componentType, string[] requires = null)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (!typeof(MonoBehaviour).IsAssignableFrom(componentType))
        {
            Debug.LogWarning($"[ComponentRegistry] '{componentType.Name}' is not a MonoBehaviour.");
            return;
        }

        if (_registry.ContainsKey(key))
        {
            Debug.LogWarning($"[ComponentRegistry] Key '{key}' already registered. Skipping '{componentType.Name}'.");
            return;
        }

        _registry[key] = componentType;
        if (requires != null && requires.Length > 0)
            _requires[key] = requires;
    }

    public static Type Get(string key)
    {
        _registry.TryGetValue(key, out var type);
        return type;
    }

    public static bool TryGet(string key, out Type type) =>
        _registry.TryGetValue(key, out type);

    public static bool Contains(string key) => _registry.ContainsKey(key);

    public static string[] GetRequires(string key) =>
        _requires.TryGetValue(key, out var deps) ? deps : Array.Empty<string>();

    public static IReadOnlyDictionary<string, Type> All => _registry;

    public static string KeyFor(Type componentType)
    {
        foreach (var kvp in _registry)
            if (kvp.Value == componentType) return kvp.Key;
        return null;
    }

    public static void Clear() { _registry.Clear(); _requires.Clear(); }
}
