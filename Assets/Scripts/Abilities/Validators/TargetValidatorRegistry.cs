using System;
using System.Collections.Generic;

/// <summary>
/// Maps "type" prefix strings to factory functions that create TargetValidator instances.
/// Concrete validators self-register via [RuntimeInitializeOnLoadMethod].
/// ID format: "type:param"  —  param is passed to the factory and may be empty.
/// </summary>
public static class TargetValidatorRegistry
{
    private static readonly Dictionary<string, Func<string, TargetValidator>> _factories = new();

    public static void Register(string prefix, Func<string, TargetValidator> factory)
        => _factories[prefix] = factory;

    /// <summary>Creates a TargetValidator from a full id string like "no_behavior:poisoned".</summary>
    public static TargetValidator Create(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        int colon  = id.IndexOf(':');
        string key = colon >= 0 ? id.Substring(0, colon) : id;
        string arg = colon >= 0 ? id.Substring(colon + 1) : "";
        return _factories.TryGetValue(key, out var factory) ? factory(arg) : null;
    }
}
