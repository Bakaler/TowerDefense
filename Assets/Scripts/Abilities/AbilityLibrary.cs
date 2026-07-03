using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads abilities.json, instantiates Ability_Effect ScriptableObjects at runtime,
/// resolves effectId → Effect via EffectLibrary, and caches by id.
/// </summary>
public class AbilityLibrary : MonoBehaviour
{
    public static AbilityLibrary Instance { get; private set; }

    [Tooltip("Resources path to abilities JSON (no extension).")]
    public string definitionsPath = "Definitions/abilities";

    private readonly Dictionary<string, Ability_Effect> _cache = new();

    private bool _loaded;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Load in Start so EffectLibrary.Awake() has already run on all objects first.
    void Start() => EnsureLoaded();

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>Force-loads if not yet loaded. Safe to call from any Start().</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        // Make sure EffectLibrary is also loaded before we resolve effect IDs
        EffectLibrary.Instance?.EnsureLoaded();
        LoadAll();
    }

    public Ability_Effect GetAbility(string id)
    {
        EnsureLoaded();
        _cache.TryGetValue(id, out var ability);
        return ability;
    }

    public bool TryGet(string id, out Ability_Effect ability)
    {
        EnsureLoaded();
        return _cache.TryGetValue(id, out ability);
    }

    // ── Loading ───────────────────────────────────────────────────────

    void LoadAll()
    {
        var asset = Resources.Load<TextAsset>(definitionsPath);
        if (asset == null)
        {
            Debug.LogError($"[AbilityLibrary] Could not load '{definitionsPath}'.");
            return;
        }

        var collection = JsonUtility.FromJson<AbilityDefinitionCollection>(asset.text);
        if (collection?.abilities == null) return;

        foreach (var def in collection.abilities)
        {
            var ability = ScriptableObject.CreateInstance<Ability_Effect>();
            ability.name             = def.id;
            ability.abilityName      = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
            ability.abilityID        = def.id;
            ability.effectId         = def.effectId;
            ability.prepare_time     = def.prepare_time;
            ability.cast_start_time  = def.cast_start_time;
            ability.cast_finish_time = def.cast_finish_time;
            ability.finish_time      = def.finish_time;
            ability.range            = def.range;
            ability.fireArc          = def.fireArc;
            ability.fireSoundId      = def.fireSoundId;
            ability.cost             = new AbilityCost { cooldownDuration = def.cooldownDuration };
            ability.attackSheetPath  = def.attackSheetPath;
            ability.attackFrameCount = def.attackFrameCount;
            ability.attackFps        = def.attackFps;
            ability.attackScale      = def.attackScale;

            // Resolve effect reference
            if (!string.IsNullOrEmpty(def.effectId))
            {
                if (EffectLibrary.Instance != null && EffectLibrary.Instance.TryGet(def.effectId, out var effect))
                    ability.effect = effect;
                else
                    Debug.LogWarning($"[AbilityLibrary] Effect id '{def.effectId}' not found for ability '{def.id}'.");
            }

            // Resolve target validators
            if (def.targetValidatorIds != null && def.targetValidatorIds.Length > 0)
            {
                var validators = new System.Collections.Generic.List<TargetValidator>();
                foreach (var vid in def.targetValidatorIds)
                {
                    var v = TargetValidatorRegistry.Create(vid);
                    if (v != null) validators.Add(v);
                    else Debug.LogWarning($"[AbilityLibrary] Unknown targetValidatorId '{vid}' on ability '{def.id}'.");
                }
                ability.targetValidators = validators.ToArray();
            }

            _cache[def.id] = ability;
        }

        Debug.Log($"[AbilityLibrary] Loaded {_cache.Count} ability/abilities.");
    }
}

[Serializable]
public class AbilityDefinition
{
    public string   id              = "";
    public string   displayName     = "";
    public string   effectId        = "";
    public float    cooldownDuration = 1f;
    public float    range            = 5f;
    public float    fireArc          = 360f;
    public string   fireSoundId      = "";
    public float    prepare_time    = 0f;
    public float    cast_start_time = 0f;
    public float    cast_finish_time = 0f;
    public float    finish_time     = 0f;
    public string[] targetValidatorIds;
    public string   attackSheetPath  = "";
    public int      attackFrameCount = 0;
    public float    attackFps        = 12f;
    public float    attackScale      = 1f;
}

[Serializable]
public class AbilityDefinitionCollection
{
    public AbilityDefinition[] abilities;
}
