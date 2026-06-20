using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Executes a sequence of effects on the same context.
/// Use to bundle multiple effects under one impact point (e.g. damage + search).
///
/// JSON data fields:
///   effectIds — ordered array of effect IDs to run
///   e.g. { "effectIds": ["chain_damage_primary", "chain_search"] }
/// </summary>
public class Effect_Set : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("set", typeof(Effect_Set));

    // Resolved at load time from effectIds
    private Effect[]   _effects   = Array.Empty<Effect>();
    private string[]   _effectIds = Array.Empty<string>();

    public string[] EffectIds => _effectIds;

    public override void ApplyData(string dataJson, EffectLibrary library)
    {
        if (string.IsNullOrEmpty(dataJson)) return;

        var data = JsonUtility.FromJson<EffectSetData>(dataJson);
        if (data?.effectIds == null) return;

        _effectIds = data.effectIds;
        var resolved = new List<Effect>();
        foreach (var id in data.effectIds)
        {
            var e = library.GetEffect(id);
            if (e != null)
                resolved.Add(e);
            else
                Debug.LogWarning($"[Effect_Set] Could not resolve effectId '{id}'.");
        }
        _effects = resolved.ToArray();
    }

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        foreach (var e in _effects)
            EffectExecutor.ExecuteEffect(e, context);
    }

    [Serializable]
    class EffectSetData
    {
        public string[] effectIds;
    }
}
