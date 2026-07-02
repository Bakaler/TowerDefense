using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic component: spawns N minions (minions.json) that wander the tower radius
/// and attack nearby enemies. The minion's look, brain, and projectile all come from
/// its MinionDefinition; this component only supplies swarm-level tuning.
/// JSON keys: minionId, droneCount, range, cooldown, damage, maxAwayTime, restDuration, effectId
/// </summary>
public class DroneSwarm : MonoBehaviour, IFactoryInitializable, IMinionHost
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("drone_swarm", typeof(DroneSwarm));

    public string minionId     = "bee";
    public int    droneCount   = 4;
    public float  range        = 5f;
    public float  cooldown     = 0.8f;
    public float  damage       = 6f;
    public float  maxAwayTime  = 6f;
    public float  restDuration = 1f;

    private readonly List<Minion> _minions = new();
    private Effect                _impactEffect;
    private string                _pendingEffectId;

    [System.Serializable]
    class Data
    {
        public string minionId     = "bee";
        public int    droneCount   = 4;
        public float  range        = 5f;
        public float  cooldown     = 0.8f;
        public float  damage       = 6f;
        public float  maxAwayTime  = 6f;
        public float  restDuration = 1f;
        public string effectId     = "";
    }

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<Data>(dataJson);
        if (d == null) return;
        minionId     = d.minionId;
        droneCount   = d.droneCount;
        range        = d.range;
        cooldown     = d.cooldown;
        damage       = d.damage;
        maxAwayTime  = d.maxAwayTime;
        restDuration = d.restDuration;
        _pendingEffectId = d.effectId;
        if (!string.IsNullOrEmpty(d.effectId) && EffectLibrary.Instance != null)
            _impactEffect = EffectLibrary.Instance.GetEffect(d.effectId);
    }

    void Start()
    {
        if (_impactEffect == null && !string.IsNullOrEmpty(_pendingEffectId) && EffectLibrary.Instance != null)
            _impactEffect = EffectLibrary.Instance.GetEffect(_pendingEffectId);
        SpawnMinions();
    }

    void SpawnMinions()
    {
        var def = MinionLibrary.Get(minionId);
        if (def == null)
        {
            Debug.LogWarning($"[DroneSwarm] Unknown minionId '{minionId}'.");
            return;
        }

        int totalCount = droneCount + (int)ModifierSelection.GetFloat("BonusDrones");
        for (int i = 0; i < totalCount; i++)
        {
            float a   = i * (360f / totalCount) * Mathf.Deg2Rad;
            var   pos = (Vector2)transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f;

            var minion = MinionFactory.Spawn(def, this, pos, new MinionSpawnArgs
            {
                range               = range,
                cooldownOverride    = cooldown + i * 0.2f,       // stagger shots
                maxAwayTimeOverride = maxAwayTime + i * 0.5f,    // stagger returns so not all leave at once
                restDurationOverride = restDuration,
                impactEffect        = _impactEffect,
                animTimeOffset      = totalCount > 0 && def.animFps > 0f
                    ? i * (1f / def.animFps / totalCount)
                    : 0f,
            });

            if (minion != null) _minions.Add(minion);
        }
    }

    // ── IMinionHost ───────────────────────────────────────────────────

    public Transform  HomeTransform => transform;
    public GameObject HostObject    => gameObject;

    // Returns raw damage; scaling is applied by Effect_Damage via OriginTower
    public float GetMinionDamage() => damage;
}
