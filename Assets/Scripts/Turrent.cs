using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AbilityManager))]
public class Turrent : MonoBehaviour
{
    public GameObject target;
    public List<GameObject> enemiesInRange = new List<GameObject>();

    [Tooltip("Must match an id in Resources/Definitions/towers.json")]
    public string definitionId;

    // Resolved in Start() from definitionId via TowerDefinitionLibrary → AbilityLibrary.
    // Never injected by the factory — the data drives it.
    [HideInInspector] public Ability_Effect fireAbility;

    private AbilityManager _abilityManager;
    private CircleCollider2D _rangeCollider;

    void Start()
    {
        _abilityManager  = GetComponent<AbilityManager>();
        _rangeCollider   = GetComponent<CircleCollider2D>();
        ResolveAbilityFromDefinition();
    }

    void ResolveAbilityFromDefinition()
    {
        if (string.IsNullOrEmpty(definitionId)) return;

        if (TowerDefinitionLibrary.Instance == null)
        {
            Debug.LogWarning($"[Turrent] TowerDefinitionLibrary not ready for '{definitionId}'.");
            return;
        }

        if (!TowerDefinitionLibrary.Instance.TryGet(definitionId, out var def)) return;
        if (string.IsNullOrEmpty(def.fireAbilityId)) return;

        if (AbilityLibrary.Instance == null)
        {
            Debug.LogWarning($"[Turrent] AbilityLibrary not ready for '{def.fireAbilityId}'.");
            return;
        }

        if (!AbilityLibrary.Instance.TryGet(def.fireAbilityId, out var ability))
        {
            Debug.LogWarning($"[Turrent] Ability '{def.fireAbilityId}' not found in AbilityLibrary.");
            return;
        }

        fireAbility = ability;
        _abilityManager.RegisterAbility(ability);

        // Range lives on the ability — size the detection collider to match
        if (_rangeCollider != null && ability.range > 0f)
            _rangeCollider.radius = ability.range;

        Debug.Log($"[Turrent] '{definitionId}' armed with '{ability.abilityID}', range={ability.range}");
    }

    void Update()
    {
        CleanUpDead();
        // Always re-evaluate — lock onto whoever is furthest along the path
        target = GetLeadEnemy();
        if (target != null) TryFire();
    }

    void TryFire()
    {
        if (fireAbility == null)
        {
            Debug.LogWarning($"[Turrent:{definitionId}] TryFire — fireAbility is NULL");
            return;
        }

        var targetUnit = target.GetComponent<UnitParentClass>();
        if (targetUnit == null) return;

        var context = new EffectContext
        {
            Caster          = null,
            CasterTransform = transform,
            Target          = targetUnit,
            TargetPoint     = target.transform.position,
            OriginAbility   = fireAbility,
            OriginTower     = gameObject,
            CustomData      = new Dictionary<string, object>(),
            AimOrigin2D     = (Vector2)transform.position,
            AimDirection2D  = ((Vector2)(target.transform.position - transform.position)).normalized,
        };

        _abilityManager.TryExecuteAbility(fireAbility, context);
    }

    void CleanUpDead()
    {
        for (int i = enemiesInRange.Count - 1; i >= 0; i--)
        {
            var go = enemiesInRange[i];
            if (go == null || !go.GetComponent<UnitParentClass>()?.isAlive == true)
                enemiesInRange.RemoveAt(i);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var unit = other.GetComponent<UnitParentClass>();
        Debug.Log($"[Turrent:{definitionId}] OnTriggerEnter2D — '{other.name}' hasUnit={unit != null}");
        if (unit != null)
            enemiesInRange.Add(other.gameObject);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject == target) target = null;
        enemiesInRange.Remove(other.gameObject);
    }

    /// <summary>Returns the enemy in range that is furthest along the path.</summary>
    GameObject GetLeadEnemy()
    {
        GameObject best        = null;
        float      bestProgress = -1f;

        foreach (var go in enemiesInRange)
        {
            if (go == null) continue;
            var unit = go.GetComponent<UnitParentClass>();
            if (unit == null || !unit.isAlive) continue;

            float progress = go.GetComponent<RouteFollower>()?.Progress ?? 0f;
            if (progress > bestProgress) { bestProgress = progress; best = go; }
        }

        return best;
    }
}
