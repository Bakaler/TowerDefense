using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks stat buffs applied to a tower (from modifiers, research, auras, etc.).
/// Add via TowerBuffHandler.GetOrAdd(tower). Read via DamageMult / FireRateMult / RangeMult.
/// </summary>
public class TowerBuffHandler : MonoBehaviour
{
    readonly List<TowerBuffDefinition> _buffs = new();

    public float DamageMult   { get; private set; }
    public float FireRateMult { get; private set; }
    public float RangeMult    { get; private set; }

    public static TowerBuffHandler GetOrAdd(GameObject go)
    {
        var h = go.GetComponent<TowerBuffHandler>() ?? go.AddComponent<TowerBuffHandler>();
        // Keep TowerInfo._buffHandler in sync so it never needs GetComponent at runtime
        var info = go.GetComponent<TowerInfo>();
        if (info != null) info._buffHandler = h;
        return h;
    }

    public void ApplyBuff(TowerBuffDefinition def)
    {
        if (_buffs.Exists(b => b.id == def.id)) return;
        _buffs.Add(def);
        Recalculate();
    }

    public void RemoveBuff(string id)
    {
        int removed = _buffs.RemoveAll(b => b.id == id);
        if (removed > 0) Recalculate();
    }

    public bool HasBuff(string id) => _buffs.Exists(b => b.id == id);

    void Recalculate()
    {
        float dmg = 0f, fr = 0f, rng = 0f;
        foreach (var b in _buffs) { dmg += b.damageMult; fr += b.fireRateMult; rng += b.rangeMult; }
        DamageMult   = dmg;
        FireRateMult = fr;
        RangeMult    = rng;
    }
}
