using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AbilityManager))]
public class Turrent : MonoBehaviour
{
    public GameObject target;
    public List<GameObject> enemiesInRange = new List<GameObject>();

    [Tooltip("Must match an id in Resources/Definitions/towers.json")]
    public string definitionId;

    [HideInInspector] public Ability_Effect fireAbility;

    private AbilityManager  _abilityManager;
    private CircleCollider2D _rangeCollider;

    // Rotation speed from tower definition; arc from the ability
    private float _rotationSpeed = 0f;    // deg/s; 0 = no rotation

    void Start()
    {
        _abilityManager = GetComponent<AbilityManager>();
        _rangeCollider  = GetComponent<CircleCollider2D>();
        ResolveAbilityFromDefinition();
    }

    void ResolveAbilityFromDefinition()
    {
        if (string.IsNullOrEmpty(definitionId)) return;
        if (TowerDefinitionLibrary.Instance == null) return;
        if (!TowerDefinitionLibrary.Instance.TryGet(definitionId, out var def)) return;

        _rotationSpeed = def.rotationSpeed;

        if (string.IsNullOrEmpty(def.fireAbilityId)) return;
        if (AbilityLibrary.Instance == null) return;
        if (!AbilityLibrary.Instance.TryGet(def.fireAbilityId, out var ability)) return;

        fireAbility = ability;
        _abilityManager.RegisterAbility(ability);

        if (_rangeCollider != null && ability.range > 0f)
            _rangeCollider.radius = ability.range;

        if (ability.range > 0f)
            GetComponent<TowerInfo>()?.SetupRangeCircle(ability.range);
    }

    void Update()
    {
        CleanUpDead();
        target = GetLeadEnemy();

        if (target == null) return;

        Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;

        if (_rotationSpeed > 0f)
        {
            // Rotate so transform.up points at target
            float targetAngle  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            float currentAngle = transform.eulerAngles.z;
            float newAngle     = Mathf.MoveTowardsAngle(currentAngle, targetAngle, _rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }

        // Gate firing on arc — always passes for 360°
        float arc      = fireAbility != null ? fireAbility.fireArc : 360f;
        float aimAngle = Vector2.Angle(transform.up, dir);
        if (aimAngle <= arc * 0.5f)
            TryFire();
    }

    void TryFire()
    {
        if (fireAbility == null) return;

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
        if (other.GetComponent<UnitParentClass>() != null)
            enemiesInRange.Add(other.gameObject);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject == target) target = null;
        enemiesInRange.Remove(other.gameObject);
    }

    GameObject GetLeadEnemy()
    {
        var validators = fireAbility?.targetValidators;
        bool hasValidators = validators != null && validators.Length > 0;

        GameObject best          = null;
        float      bestProgress  = -1f;
        GameObject fallback      = null;
        float      fallbackProg  = -1f;

        foreach (var go in enemiesInRange)
        {
            if (go == null) continue;
            var unit = go.GetComponent<UnitParentClass>();
            if (unit == null || !unit.isAlive) continue;

            float progress = go.GetComponent<RouteFollower>()?.Progress ?? 0f;

            // Always track overall lead for fallback
            if (progress > fallbackProg) { fallbackProg = progress; fallback = go; }

            // Track validated lead separately
            if (hasValidators && !PassesTargetValidators(go, validators)) continue;
            if (progress > bestProgress) { bestProgress = progress; best = go; }
        }

        return best ?? fallback;
    }

    static bool PassesTargetValidators(GameObject candidate, TargetValidator[] validators)
    {
        foreach (var v in validators)
            if (!v.IsValid(candidate)) return false;
        return true;
    }
}
