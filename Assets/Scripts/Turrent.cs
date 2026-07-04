using System.Collections.Generic;
using UnityEngine;

public enum TargetingMode { Furthest, Closest, Lowest }

[RequireComponent(typeof(AbilityManager))]
public class Turrent : MonoBehaviour
{
    public GameObject target;
    public List<GameObject> enemiesInRange = new List<GameObject>();

    [Tooltip("Must match an id in Resources/Definitions/towers.json")]
    public string definitionId;

    public TargetingMode Targeting { get; set; } = TargetingMode.Furthest;

    [HideInInspector] public Ability_Effect fireAbility;

    private AbilityManager   _abilityManager;
    private CircleCollider2D _rangeCollider;
    public  Transform        RotatingPart => _rotatingPart;
    private Transform        _rotatingPart;   // child "Turret" if present, else self

    // Rotation speed from tower definition; arc from the ability
    private float _rotationSpeed = 0f;    // deg/s; 0 = no rotation

    // World-space firing range — targets must have their *center* inside this,
    // so firing matches the displayed range circle exactly.
    private float _worldRange = 0f;

    /// <summary>Called when range changes at runtime (e.g. rangePerTier upgrades).</summary>
    public void SetWorldRange(float range) => _worldRange = range;

    void Start()
    {
        _abilityManager = GetComponent<AbilityManager>();
        _rangeCollider  = GetComponent<CircleCollider2D>();
        if (_rangeCollider != null)
            _worldRange = _rangeCollider.radius * Mathf.Max(0.01f, transform.localScale.x);

        // Use the dedicated "Turret" child for rotation if present; fall back to root
        var turretChild = transform.Find("Turret");
        _rotatingPart   = turretChild != null ? turretChild : transform;

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
        {
            // Collider radius scales with the transform — compensate so world range matches
            _rangeCollider.radius = ability.range / Mathf.Max(0.01f, transform.localScale.x);
            _worldRange           = ability.range;
        }

        if (ability.range > 0f)
            GetComponent<TowerInfo>()?.SetupRangeCircle(ability.range);
    }

    void Update()
    {
        CleanUpDead();

        float arc = fireAbility != null ? fireAbility.fireArc : 360f;

        target = GetLeadEnemy();
        if (target == null) return;

        Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;

        if (_rotationSpeed > 0f)
        {
            float targetAngle  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            float currentAngle = _rotatingPart.eulerAngles.z;
            float newAngle     = Mathf.MoveTowardsAngle(currentAngle, targetAngle, _rotationSpeed * Time.deltaTime);
            _rotatingPart.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }

        if (Vector2.Angle(_rotatingPart.up, dir) <= arc * 0.5f)
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
            TargetPoint     = target.transform.position,   // captured at fire time — mortar uses this
            AimOrigin2D     = (Vector2)transform.position,
            OriginAbility   = fireAbility,
            OriginTower     = gameObject,
            CustomData      = new Dictionary<string, object>(),
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
        var validators    = fireAbility?.targetValidators;
        bool hasValidators = validators != null && validators.Length > 0;

        GameObject best     = null;
        float      bestVal  = Targeting == TargetingMode.Closest ? float.MaxValue : -1f;
        GameObject fallback = null;
        float      fbVal    = bestVal;

        float rangeSqr = _worldRange > 0f ? _worldRange * _worldRange : float.MaxValue;

        foreach (var go in enemiesInRange)
        {
            if (go == null) continue;
            var unit = go.GetComponent<UnitParentClass>();
            if (unit == null || !unit.isAlive) continue;
            if (!IsOnScreen(go)) continue;

            // The trigger list includes collider-edge touches — require the enemy's
            // center inside the range so shots match the displayed circle.
            if (Vector2.SqrMagnitude((Vector2)go.transform.position - (Vector2)transform.position) > rangeSqr)
                continue;

            float val = Score(go, unit);

            // Track unvalidated fallback
            if (IsBetter(val, fbVal)) { fbVal = val; fallback = go; }

            if (hasValidators && !PassesTargetValidators(go, validators)) continue;
            if (IsBetter(val, bestVal)) { bestVal = val; best = go; }
        }

        return best ?? fallback;
    }

    float Score(GameObject go, UnitParentClass unit)
    {
        switch (Targeting)
        {
            case TargetingMode.Closest:
                return Vector2.SqrMagnitude((Vector2)go.transform.position - (Vector2)transform.position);
            case TargetingMode.Lowest:
                return unit.lifeCurrent;
            default: // Furthest
                return go.GetComponent<RouteFollower>()?.Progress ?? 0f;
        }
    }

    bool IsBetter(float val, float current)
    {
        return Targeting == TargetingMode.Closest ? val < current : val > current;
    }

    static bool IsOnScreen(GameObject go)
    {
        if (Camera.main == null) return true;
        var vp = Camera.main.WorldToViewportPoint(go.transform.position);
        return vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f && vp.z > 0f;
    }

    static bool PassesTargetValidators(GameObject candidate, TargetValidator[] validators)
    {
        foreach (var v in validators)
            if (!v.IsValid(candidate)) return false;
        return true;
    }
}
