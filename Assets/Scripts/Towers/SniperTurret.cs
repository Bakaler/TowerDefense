using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fires a hitscan shot at the enemy furthest along the path that is currently
/// inside the SniperZone rectangle. Uses the tower's fireAbilityId for damage and cooldown.
/// Disables Turret's own Update so the ability isn't double-fired.
///
/// Registered as "sniper_turret" in ComponentRegistry so TowerFactory can attach it.
/// </summary>
public class SniperTurret : MonoBehaviour, IFactoryInitializable
{
    private SniperZone     _zone;
    private AbilityManager _abilityManager;
    private Ability_Effect _fireAbility;

    // Units currently inside the sniper zone — maintained via trigger callbacks
    private readonly System.Collections.Generic.HashSet<UnitManager> _inRange
        = new System.Collections.Generic.HashSet<UnitManager>();

    void OnTriggerEnter2D(Collider2D other)
    {
        var u = other.GetComponent<UnitManager>();
        if (u != null) _inRange.Add(u);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var u = other.GetComponent<UnitManager>();
        if (u != null) _inRange.Remove(u);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("sniper_turret", typeof(SniperTurret));

    void Start()
    {
        _zone           = GetComponent<SniperZone>();
        _abilityManager = GetComponent<AbilityManager>();

        // Resolve ability from tower definition; disable Turret's own Update loop
        var turret = GetComponent<Turret>();
        if (turret != null)
        {
            string defId = turret.definitionId;
            turret.enabled = false;

            if (!string.IsNullOrEmpty(defId)
                && TowerDefinitionLibrary.Instance != null
                && TowerDefinitionLibrary.Instance.TryGet(defId, out var def)
                && !string.IsNullOrEmpty(def.fireAbilityId)
                && AbilityLibrary.Instance != null
                && AbilityLibrary.Instance.TryGet(def.fireAbilityId, out _fireAbility))
            {
                _abilityManager.RegisterAbility(_fireAbility);
            }
        }
    }

    void Update()
    {
        if (_fireAbility == null) return;

        var instance = _abilityManager?.GetInstance(_fireAbility);
        if (instance == null || !instance.IsReady) return;

        if (_zone != null && _zone.IsRepositioning) return;

        TryFire();
    }

    void TryFire()
    {
        if (_zone == null) return;

        UnitManager best         = null;
        float       bestProgress = -1f;

        // Clean destroyed entries, then pick furthest-along live unit in zone
        _inRange.RemoveWhere(u => u == null);
        foreach (var unit in _inRange)
        {
            if (!unit.isAlive) continue;
            if (!_zone.Contains(unit.transform.position)) continue;

            var   follower = unit.Follower;
            float prog     = follower != null ? follower.Progress : 0f;
            if (prog > bestProgress) { bestProgress = prog; best = unit; }
        }

        if (best == null) return;

        // Barriers block bullets — same interception ShieldBubble does for
        // projectiles: the bubble soaks the shot and the round is spent.
        var bubble = best.GetComponentInChildren<ShieldBubble>();
        if (bubble != null)
        {
            float dmg = 0f;
            var info  = GetComponent<TowerInfo>();
            if (info != null) dmg = info.damage * info.EffectiveDamageMult;

            bubble.AbsorbHit(dmg);
            _abilityManager.GetInstance(_fireAbility)?.Trigger();   // shot is spent
            if (!string.IsNullOrEmpty(_fireAbility.fireSoundId))
                AudioManager.Play(_fireAbility.fireSoundId);
            StartCoroutine(DrawBeam(transform.position, best.transform.position));
            return;
        }

        var target = best.GetComponent<UnitParentClass>();
        if (target == null) return;

        var context = new EffectContext
        {
            CasterTransform = transform,
            Target          = target,
            TargetPoint     = best.transform.position,
            OriginAbility   = _fireAbility,
            OriginTower     = gameObject,
            AimOrigin2D     = (Vector2)transform.position,
            AimDirection2D  = ((Vector2)(best.transform.position - transform.position)).normalized,
            CustomData      = new Dictionary<string, object>(),
        };

        bool fired = _abilityManager.TryExecuteAbility(_fireAbility, context);
        if (fired)
            StartCoroutine(DrawBeam(transform.position, best.transform.position));
    }

    IEnumerator DrawBeam(Vector3 from, Vector3 to)
    {
        var go = new GameObject("_SniperBeam");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace    = true;
        lr.positionCount    = 2;
        lr.startWidth       = 0.04f;
        lr.endWidth         = 0.01f;
        lr.numCapVertices   = 2;
        lr.sortingLayerName = "Default";
        lr.sortingOrder     = 9;
        lr.sharedMaterial   = RuntimeMaterials.SpriteDefault;
        lr.startColor       = new Color(0.9f, 0.95f, 1f, 0.9f);
        lr.endColor         = new Color(0.5f, 0.7f, 1f, 0f);
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);

        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(0.9f, 0f, t / 0.12f);
            lr.startColor = new Color(0.9f, 0.95f, 1f, alpha);
            lr.endColor   = new Color(0.5f, 0.7f, 1f, alpha * 0.5f);
            yield return null;
        }
        Destroy(go);
    }

    // Params now live in ability/effect data; nothing to read from component JSON
    public void Initialize(string dataJson) { }
}
