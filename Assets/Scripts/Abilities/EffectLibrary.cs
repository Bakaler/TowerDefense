using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads effects.json, instantiates Effect ScriptableObjects at runtime, and caches them by id.
/// Effects reference each other by id string — no direct object references in data.
/// </summary>
public class EffectLibrary : MonoBehaviour
{
    public static EffectLibrary Instance { get; private set; }

    [Tooltip("Resources path to effects JSON (no extension).")]
    public string definitionsPath = "Definitions/effects";

    private readonly Dictionary<string, Effect> _cache = new();
    private bool _loaded;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsureLoaded();
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>Force-loads if not yet loaded. Safe to call from any Awake/Start.</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        LoadAll();
    }

    public Effect GetEffect(string id)
    {
        _cache.TryGetValue(id, out var effect);
        return effect;
    }

    public bool TryGet(string id, out Effect effect) =>
        _cache.TryGetValue(id, out effect);

    // ── Loading ───────────────────────────────────────────────────────

    void LoadAll()
    {
        var json = Preprocess(Resources.Load<TextAsset>(definitionsPath)?.text);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError($"[EffectLibrary] Could not load '{definitionsPath}'.");
            return;
        }

        var collection = JsonUtility.FromJson<EffectDefinitionCollection>(json);
        if (collection?.effects == null) return;

        // Pass 1: instantiate all Effects (so cross-references can resolve in pass 2)
        var instances = new List<(EffectDefinition def, Effect instance)>();
        foreach (var def in collection.effects)
        {
            if (!EffectRegistry.TryGet(def.type, out Type t))
            {
                Debug.LogWarning($"[EffectLibrary] Unknown effect type '{def.type}' for id '{def.id}'.");
                continue;
            }

            var effect = (Effect)ScriptableObject.CreateInstance(t);
            effect.effectID   = def.id;
            effect.effectName = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
            effect.chance     = def.chance > 0f ? def.chance : 1f;
            effect.name       = def.id; // SO asset name

            _cache[def.id] = effect;
            instances.Add((def, effect));
        }

        // Pass 2: ApplyData (now all effects are in cache so cross-refs resolve)
        foreach (var (def, effect) in instances)
            effect.ApplyData(def.data, this);

        Debug.Log($"[EffectLibrary] Loaded {_cache.Count} effect(s).");
    }

    // ── JSON preprocessor (converts "data":{...} → "data":"{...}") ────
    // Mirrors the robust char-by-char implementation in UnitDefinitionLibrary.
    static string Preprocess(string raw)
    {
        if (raw == null) return null;

        const string dataKey = "\"data\"";
        var sb  = new System.Text.StringBuilder(raw.Length + 64);
        int pos = 0;

        while (pos < raw.Length)
        {
            int keyPos = raw.IndexOf(dataKey, pos, StringComparison.Ordinal);
            if (keyPos < 0) { sb.Append(raw, pos, raw.Length - pos); break; }

            int colonPos = keyPos + dataKey.Length;
            while (colonPos < raw.Length && raw[colonPos] != ':') colonPos++;

            int valueStart = colonPos + 1;
            while (valueStart < raw.Length && char.IsWhiteSpace(raw[valueStart])) valueStart++;

            if (valueStart >= raw.Length || raw[valueStart] != '{')
            {
                // Not an object — copy up through the key and continue
                sb.Append(raw, pos, keyPos - pos + dataKey.Length);
                pos = keyPos + dataKey.Length;
                continue;
            }

            // Copy everything up to (not including) the opening '{'
            sb.Append(raw, pos, valueStart - pos);
            pos = valueStart;

            // Walk to matching closing brace, respecting strings
            int depth = 0; bool inStr = false; int objStart = pos;
            while (pos < raw.Length)
            {
                char c = raw[pos];
                if (inStr) { if (c == '\\') pos++; else if (c == '"') inStr = false; }
                else        { if (c == '"') inStr = true; else if (c == '{') depth++; else if (c == '}') { depth--; if (depth == 0) { pos++; break; } } }
                pos++;
            }

            // Emit the object as an escaped JSON string value
            sb.Append('"');
            for (int i = objStart; i < pos; i++)
            {
                switch (raw[i])
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': break;                     // drop bare CR
                    case '\t': sb.Append("\\t");  break;
                    default:   sb.Append(raw[i]); break;
                }
            }
            sb.Append('"');
        }

        return sb.ToString();
    }
}

[Serializable]
public class EffectDefinition
{
    public string id          = "";
    public string displayName = "";
    public string type        = "";
    public float  chance      = 1f;
    /// <summary>JSON string of type-specific parameters. Preprocessor converts {} → "...".</summary>
    public string data        = "";
}

[Serializable]
public class EffectDefinitionCollection
{
    public EffectDefinition[] effects;
}
