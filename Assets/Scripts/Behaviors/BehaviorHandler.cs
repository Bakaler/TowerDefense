using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks active behaviors on a unit. Recalculates stat multipliers and visual
/// tint whenever the behavior list changes or a behavior expires.
/// Add this lazily via Effect_ApplyBehavior — no factory changes needed.
/// </summary>
public class BehaviorHandler : MonoBehaviour
{
    private class Instance
    {
        public BehaviorDefinition Def;
        public float              Remaining;
        public float              TickTimer;
        public Instance(BehaviorDefinition def)
        {
            Def       = def;
            Remaining = def.duration;
            TickTimer = def.tickInterval > 0f ? def.tickInterval : float.MaxValue;
        }
        // Returns true when expired
        public bool Advance(float dt, out bool ticked)
        {
            Remaining -= dt;
            ticked = false;
            if (Def.tickInterval > 0f)
            {
                TickTimer -= dt;
                if (TickTimer <= 0f) { TickTimer += Def.tickInterval; ticked = true; }
            }
            return Remaining <= 0f;
        }
    }

    private readonly List<Instance> _active = new();
    private SpriteRenderer          _sr;
    private Color                   _baseColor;
    private UnitManager             _unit;

    void Awake()
    {
        _sr    = GetComponent<SpriteRenderer>();
        _unit  = GetComponent<UnitManager>();
        if (_sr != null) _baseColor = _sr.color;
    }

    // ── Public API ────────────────────────────────────────────────────

    public void Apply(BehaviorDefinition def)
    {
        // Check if any active behavior grants immunity to the incoming type
        if (def.behaviorType != BehaviorType.None)
        {
            string typeName = def.behaviorType.ToString();
            foreach (var inst in _active)
            {
                if (inst.Def.immunities != null)
                    foreach (var imm in inst.Def.immunities)
                        if (imm == typeName) return;
            }
        }

        switch (def.stackRule)
        {
            case "refresh":
            {
                var ex = _active.Find(b => b.Def.id == def.id);
                if (ex != null) { ex.Remaining = def.duration; return; }
                break;
            }
            case "none":
                if (_active.Exists(b => b.Def.id == def.id)) return;
                break;
            // "stack": always add a new instance
        }
        _active.Add(new Instance(def));
        Recalculate();
    }

    /// <summary>Apply a behavior permanently (no expiry). Used for starting behaviors.</summary>
    public void ApplyPermanent(BehaviorDefinition def)
    {
        if (_active.Exists(b => b.Def.id == def.id)) return;
        var inst = new Instance(def);
        inst.Remaining = float.MaxValue;   // never expires
        _active.Add(inst);
        Recalculate();
    }

    /// <summary>Called by UnitManager.Die() — executes onDeathEffectId on any active behaviors.</summary>
    public void TriggerDeathEffects(GameObject self)
    {
        foreach (var inst in _active)
        {
            if (string.IsNullOrEmpty(inst.Def.onDeathEffectId)) continue;
            if (EffectLibrary.Instance == null) continue;
            var effect = EffectLibrary.Instance.GetEffect(inst.Def.onDeathEffectId);
            if (effect == null) continue;
            var ctx = new EffectContext
            {
                CasterTransform = self.transform,
                AimOrigin2D     = self.transform.position,
                CustomData      = new System.Collections.Generic.Dictionary<string, object>(),
            };
            effect.Execute(ctx);
        }
    }

    public bool HasBehavior(string behaviorId) => _active.Exists(b => b.Def.id == behaviorId);

    public void Remove(string behaviorId)
    {
        int removed = _active.RemoveAll(b => b.Def.id == behaviorId);
        if (removed > 0) Recalculate();
    }

    /// <summary>Force-reapply all active behaviors (e.g. after an external speed override ends).</summary>
    public void Refresh() => Recalculate();

    /// <summary>Remove every active behavior (cleanse).</summary>
    public void RemoveAll()
    {
        _active.Clear();
        Recalculate();
    }

    /// <summary>Remove all behaviors matching a specific type (e.g. all Rooted or Slowed).</summary>
    public void RemoveByType(BehaviorType type)
    {
        int removed = _active.RemoveAll(b => b.Def.behaviorType == type);
        if (removed > 0) Recalculate();
    }

    public bool HasBehaviorType(BehaviorType type) => _active.Exists(b => b.Def.behaviorType == type);

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Update()
    {
        bool changed = false;
        float dt = Time.deltaTime;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            bool expired = _active[i].Advance(dt, out bool ticked);
            if (ticked) ApplyTick(_active[i]);
            if (expired) { _active.RemoveAt(i); changed = true; }
        }
        if (changed) Recalculate();
    }

    void ApplyTick(Instance inst)
    {
        if (_unit == null || !_unit.isAlive) return;
        if (inst.Def.tickDamage <= 0f) return;
        var dmgType = (DamageType)inst.Def.tickDamageType;
        _unit.TakeDamage(inst.Def.tickDamage, 0f, 0f, inst.Def.tickDamage * 10f, dmgType);
    }

    // ── Recalculate ───────────────────────────────────────────────────

    void Recalculate()
    {
        float speedMult = 1f;
        Color tint      = _baseColor;

        foreach (var inst in _active)
        {
            speedMult *= inst.Def.moveSpeedMultiplier;
            // Use the tint from the highest-priority (last applied) non-white behavior
            if (inst.Def.tintColor != Color.white)
                tint = inst.Def.tintColor;
        }

        if (_unit != null)
            _unit.speedCurrent = _unit.speedMax * speedMult;

        if (_sr != null)
            _sr.color = _active.Count > 0 ? tint : _baseColor;
    }
}
