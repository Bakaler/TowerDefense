using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps type-key strings to Effect subclass Types.
/// Effect subclasses self-register via [RuntimeInitializeOnLoadMethod].
///
/// Example registration (inside Effect_Damage.cs):
///   [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
///   static void Register() => EffectRegistry.Register("damage", typeof(Effect_Damage));
/// </summary>
public static class EffectRegistry
{
    private static readonly Dictionary<string, Type> _registry = new();

    public static void Register(string typeKey, Type effectType)
    {
        if (string.IsNullOrEmpty(typeKey)) return;
        if (!typeof(Effect).IsAssignableFrom(effectType))
        {
            Debug.LogWarning($"[EffectRegistry] '{effectType.Name}' does not extend Effect.");
            return;
        }
        if (_registry.ContainsKey(typeKey))
        {
            Debug.LogWarning($"[EffectRegistry] Key '{typeKey}' already registered.");
            return;
        }
        _registry[typeKey] = effectType;
    }

    public static bool TryGet(string typeKey, out Type type) =>
        _registry.TryGetValue(typeKey, out type);

    public static void Clear() => _registry.Clear();
}
