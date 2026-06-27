using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fires a hitscan shot at the enemy furthest along the path that is currently
/// inside the SniperZone rectangle. Uses the tower's fireAbilityId for damage and cooldown.
/// Disables Turrent's own Update so the ability isn't double-fired.
///
/// Registered as "sniper_turrent" in ComponentRegistry so TowerFactory can attach it.
/// </summary>
public class SniperTurrent : MonoBehaviour, IFactoryInitializable
{
    private SniperZone     _zone;
    private AbilityManager _abilityManager;
    private Ability_Effect _fireAbility;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("sniper_turrent", typeof(SniperTurrent));

    void Start()
    {
        _zone           = GetComponent<SniperZone>();
        _abilityManager = GetComponent<AbilityManager>();

        // Resolve ability from tower definition; disable Turrent's own Update loop
        var turrent = GetComponent<Turrent>();
        if (turrent != null)
        {
            string defId = turrent.definitionId;
            turrent.enabled = false;

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

        foreach (var unit in FindObjectsByType<UnitManager>(FindObjectsSortMode.None))
        {
            if (!unit.isAlive) continue;
            if (!_zone.Contains(unit.transform.position)) continue;

            var   follower = unit.GetComponent<RouteFollower>();
            float prog     = follower != null ? follower.Progress : 0f;
            if (prog > bestProgress) { bestProgress = prog; best = unit; }
        }

        if (best == null) return;

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
        lr.material         = new Material(Shader.Find("Sprites/Default"));
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
