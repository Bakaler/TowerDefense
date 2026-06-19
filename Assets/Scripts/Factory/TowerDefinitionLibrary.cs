using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads tower definitions from Resources/Definitions/towers.json.
/// Place this on a persistent scene GameObject alongside TowerFactory.
/// </summary>
public class TowerDefinitionLibrary : MonoBehaviour
{
    public static TowerDefinitionLibrary Instance { get; private set; }

    private readonly Dictionary<string, TowerDefinition> _definitions = new();
    private bool _loaded;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Load();
    }

    // ── Public API ────────────────────────────────────────────────────

    public TowerDefinition Get(string id)
    {
        EnsureLoaded();
        _definitions.TryGetValue(id, out var def);
        return def;
    }

    public bool TryGet(string id, out TowerDefinition def)
    {
        EnsureLoaded();
        return _definitions.TryGetValue(id, out def);
    }

    public IReadOnlyDictionary<string, TowerDefinition> All
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

        var asset = Resources.Load<TextAsset>("Definitions/towers");
        if (asset == null)
        {
            Debug.LogError("[TowerDefinitionLibrary] 'Resources/Definitions/towers.json' not found!");
            return;
        }

        TowerDefinitionCollection collection;
        try
        {
            collection = JsonUtility.FromJson<TowerDefinitionCollection>(PreprocessJson(asset.text));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TowerDefinitionLibrary] Failed to parse towers.json: {e.Message}");
            return;
        }

        if (collection?.towers == null) return;

        foreach (var def in collection.towers)
        {
            if (string.IsNullOrEmpty(def?.id)) continue;
            if (_definitions.ContainsKey(def.id)) continue;
            _definitions[def.id] = def;
        }

        Debug.Log($"[TowerDefinitionLibrary] Loaded {_definitions.Count} tower definition(s): [{string.Join(", ", _definitions.Keys)}]");
    }

    // ── JSON Preprocessor ─────────────────────────────────────────────
    // Converts "data": { ... } → "data": "{ ... }" so ComponentEntry.data
    // can be deserialized as a plain string by JsonUtility.
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
