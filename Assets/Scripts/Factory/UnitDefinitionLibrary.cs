using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads enemy unit definitions from Resources/Definitions/units.json.
/// Place this on a persistent scene GameObject alongside UnitFactory.
/// Call is automatic — library loads on first access if not yet initialized.
/// </summary>
public class UnitDefinitionLibrary : MonoBehaviour
{
    public static UnitDefinitionLibrary Instance { get; private set; }

    private readonly Dictionary<string, UnitDefinition> _definitions = new();
    private bool _loaded;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Load();
    }

    // ── Public API ────────────────────────────────────────────────────

    public UnitDefinition Get(string id)
    {
        EnsureLoaded();
        _definitions.TryGetValue(id, out var def);
        return def;
    }

    public bool TryGet(string id, out UnitDefinition def)
    {
        EnsureLoaded();
        return _definitions.TryGetValue(id, out def);
    }

    public bool Contains(string id)
    {
        EnsureLoaded();
        return _definitions.ContainsKey(id);
    }

    public IReadOnlyDictionary<string, UnitDefinition> All
    {
        get { EnsureLoaded(); return _definitions; }
    }

    // ── Loading ───────────────────────────────────────────────────────

    private void EnsureLoaded()
    {
        if (!_loaded) Load();
    }

    private void Load()
    {
        _loaded = true;
        _definitions.Clear();

        var asset = Resources.Load<TextAsset>("Definitions/units");
        if (asset == null)
        {
            Debug.LogError("[UnitDefinitionLibrary] 'Resources/Definitions/units.json' not found!");
            return;
        }

        UnitDefinitionCollection collection;
        try
        {
            collection = JsonUtility.FromJson<UnitDefinitionCollection>(PreprocessJson(asset.text));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UnitDefinitionLibrary] Failed to parse units.json: {e.Message}");
            return;
        }

        if (collection?.units == null) return;

        foreach (var def in collection.units)
        {
            if (string.IsNullOrEmpty(def?.id)) continue;
            if (_definitions.ContainsKey(def.id)) { Debug.LogWarning($"[UnitDefinitionLibrary] Duplicate id '{def.id}' skipped."); continue; }
            _definitions[def.id] = def;
        }

        Debug.Log($"[UnitDefinitionLibrary] Loaded {_definitions.Count} unit definition(s): [{string.Join(", ", _definitions.Keys)}]");
    }

    // ── JSON Preprocessor ─────────────────────────────────────────────
    // Converts "data": { ... } → "data": "{ ... }" so JsonUtility can
    // deserialize ComponentEntry.data as a plain string field.
    private static string PreprocessJson(string json)
    {
        const string dataKey = "\"data\"";
        var sb  = new System.Text.StringBuilder(json.Length + 64);
        int pos = 0;

        while (pos < json.Length)
        {
            int keyPos = json.IndexOf(dataKey, pos, System.StringComparison.Ordinal);
            if (keyPos < 0) { sb.Append(json, pos, json.Length - pos); break; }

            int colonPos = keyPos + dataKey.Length;
            while (colonPos < json.Length && json[colonPos] != ':') colonPos++;

            int valueStart = colonPos + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart])) valueStart++;

            if (valueStart >= json.Length || json[valueStart] != '{')
            {
                sb.Append(json, pos, keyPos - pos + dataKey.Length);
                pos = keyPos + dataKey.Length;
                continue;
            }

            sb.Append(json, pos, valueStart - pos);
            pos = valueStart;

            int depth = 0; bool inStr = false; int objStart = pos;
            while (pos < json.Length)
            {
                char c = json[pos];
                if (inStr) { if (c == '\\') pos++; else if (c == '"') inStr = false; }
                else { if (c == '"') inStr = true; else if (c == '{') depth++; else if (c == '}') { depth--; if (depth == 0) { pos++; break; } } }
                pos++;
            }

            sb.Append('"');
            for (int i = objStart; i < pos; i++)
            {
                switch (json[i])
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': break;
                    case '\t': sb.Append("\\t");  break;
                    default:   sb.Append(json[i]); break;
                }
            }
            sb.Append('"');
        }

        return sb.ToString();
    }
}
