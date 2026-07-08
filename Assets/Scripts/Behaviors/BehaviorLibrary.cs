using System.Collections.Generic;
using UnityEngine;

public class BehaviorLibrary : MonoBehaviour
{
    public static BehaviorLibrary Instance { get; private set; }

    private readonly Dictionary<string, BehaviorDefinition> _defs = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Load();
    }

    void Load()
    {
        var asset = Resources.Load<TextAsset>("Definitions/behaviors");
        if (asset == null) { Debug.LogWarning("[BehaviorLibrary] behaviors.json not found."); return; }
        var wrapper = JsonUtility.FromJson<BehaviorLibraryData>(asset.text);
        if (wrapper?.behaviors == null) return;
        foreach (var def in wrapper.behaviors)
            _defs[def.id] = def;
        Debug.Log($"[BehaviorLibrary] Loaded {_defs.Count} behavior(s).");
    }

    public bool TryGet(string id, out BehaviorDefinition def) => _defs.TryGetValue(id, out def);

    /// <summary>Re-reads behaviors.json. Lets play-mode tools pick up definitions saved by the editor.</summary>
    public void Reload()
    {
        _defs.Clear();
        Load();
    }
}

[System.Serializable]
class BehaviorLibraryData { public List<BehaviorDefinition> behaviors; }
