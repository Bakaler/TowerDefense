using System;
using UnityEngine;

/// <summary>
/// The Shielder periodically grants a ShieldBubble to itself and nearby allies.
/// Registered as "shielder_aura".
/// </summary>
public class ShielderAura : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("shielder_aura", typeof(ShielderAura));

    public float auraRadius  = 3.5f;
    public float cooldown    = 8f;
    public float shieldHp    = 40f;
    public float bubbleRadius = 0.55f;

    [Serializable]
    class Data
    {
        public float auraRadius   = 3.5f;
        public float shieldHp     = 40f;
        public float bubbleRadius = 0.55f;
    }

    public void Initialize(string dataJson)
    {
        if (!string.IsNullOrEmpty(dataJson))
        {
            var d = JsonUtility.FromJson<Data>(dataJson);
            auraRadius   = d.auraRadius;
            shieldHp     = d.shieldHp;
            bubbleRadius = d.bubbleRadius;
        }
        PulseShields();   // one-time shield grant on spawn
    }

    void PulseShields()
    {
        // Shield self
        ShieldBubble.AddTo(gameObject, shieldHp, bubbleRadius);

        // Shield nearby allies
        var hits = Physics2D.OverlapCircleAll(transform.position, auraRadius, LayerMask.GetMask("Enemy"));
        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;
            var unit = col.GetComponent<UnitManager>();
            if (unit == null || !unit.isAlive) continue;
            ShieldBubble.AddTo(col.gameObject, shieldHp, bubbleRadius);
        }
    }
}
