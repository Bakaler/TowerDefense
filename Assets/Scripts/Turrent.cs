using System.Collections.Generic;
using UnityEngine;

public enum TargetingMode
{
    Furthest,        // most route progress (the leading unit)
    Closest,         // nearest to this tower
    LowestHP,
    HighestHP,
    LeastShields,    // no shields first, then lowest shield pool
    HighestShields,
    HighPrio,        // support units (tag "high_prio": shielders, priests, barrier weavers)
    Boss,            // tag "boss"
    Invisible,       // units that can go invisible (cloakers)
}

[RequireComponent(typeof(AbilityManager))]
public class Turrent : MonoBehaviour
{
    public GameObject target;
    public List<GameObject> enemiesInRange = new List<GameObject>();

    [Tooltip("Must match an id in Resources/Definitions/towers.json")]
    public string definitionId;

    public TargetingMode Targeting          { get; set; } = TargetingMode.Furthest;
    /// <summary>Tie-breaker when candidates score equal on the primary mode
    /// (e.g. primary Boss, secondary Furthest → leading boss, else leading unit).</summary>
    public TargetingMode TargetingSecondary { get; set; } = TargetingMode.Furthest;

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

    private TowerInfo _info;

    void Start()
    {
        _abilityManager = GetComponent<AbilityManager>();
        _rangeCollider  = GetComponent<CircleCollider2D>();
        _info           = GetComponent<TowerInfo>();
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

        // Pair towers (fence posts) are structurally oriented toward their partner
        // post — they must never rotate toward enemies.
        _rotationSpeed = def.placementMode == "pair" ? 0f : def.rotationSpeed;

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
        float      bestVal  = float.MinValue;
        float      bestVal2 = float.MinValue;
        float      bestProg = float.MinValue;
        GameObject fallback = null;
        float      fbVal    = float.MinValue;
        float      fbVal2   = float.MinValue;
        float      fbProg   = float.MinValue;

        float rangeSqr   = _worldRange > 0f ? _worldRange * _worldRange : float.MaxValue;
        bool  isDetector = _info != null && _info.IsDetector;

        foreach (var go in enemiesInRange)
        {
            if (go == null) continue;
            var unit = go.GetComponent<UnitParentClass>();
            if (unit == null || !unit.isAlive) continue;
            if (!IsOnScreen(go)) continue;

            // Invisible units can only be targeted by towers with active detection
            if (!isDetector)
            {
                var bh = go.GetComponent<BehaviorHandler>();
                if (bh != null && bh.HasBehaviorType(BehaviorType.Invisible)) continue;
            }

            // The trigger list includes collider-edge touches — require the enemy's
            // center inside the range so shots match the displayed circle.
            if (Vector2.SqrMagnitude((Vector2)go.transform.position - (Vector2)transform.position) > rangeSqr)
                continue;

            float prog = go.GetComponent<RouteFollower>()?.Progress ?? 0f;
            float val  = Score(go, unit, prog, Targeting);
            float val2 = Score(go, unit, prog, TargetingSecondary);

            // Track unvalidated fallback
            if (IsBetter(val, val2, prog, fbVal, fbVal2, fbProg))
                { fbVal = val; fbVal2 = val2; fbProg = prog; fallback = go; }

            if (hasValidators && !PassesTargetValidators(go, validators)) continue;
            if (IsBetter(val, val2, prog, bestVal, bestVal2, bestProg))
                { bestVal = val; bestVal2 = val2; bestProg = prog; best = go; }
        }

        return best ?? fallback;
    }

    /// <summary>
    /// Higher score wins. Candidates are ranked by the primary mode's score,
    /// then the secondary mode's, then route progress ("ties go to whoever
    /// is leading" no matter what the player picked).
    /// </summary>
    float Score(GameObject go, UnitParentClass unit, float prog, TargetingMode mode)
    {
        switch (mode)
        {
            case TargetingMode.Closest:
                return -Vector2.SqrMagnitude((Vector2)go.transform.position - (Vector2)transform.position);
            case TargetingMode.LowestHP:       return -unit.lifeCurrent;
            case TargetingMode.HighestHP:      return  unit.lifeCurrent;
            case TargetingMode.LeastShields:   return -unit.shieldCurrent;
            case TargetingMode.HighestShields: return  unit.shieldCurrent;
            case TargetingMode.HighPrio:       return (unit as UnitManager)?.HasTag("high_prio") == true ? 1f : 0f;
            case TargetingMode.Boss:           return (unit as UnitManager)?.HasTag("boss")      == true ? 1f : 0f;
            case TargetingMode.Invisible:      return (unit as UnitManager)?.canGoInvisible      == true ? 1f : 0f;
            default:                           return prog; // Furthest
        }
    }

    static bool IsBetter(float val, float val2, float prog,
                         float bestVal, float bestVal2, float bestProg)
    {
        const float EPS = 0.0001f;
        if (val > bestVal + EPS) return true;
        if (val < bestVal - EPS) return false;
        if (val2 > bestVal2 + EPS) return true;      // primary tie → secondary mode
        if (val2 < bestVal2 - EPS) return false;
        return prog > bestProg;                      // full tie → leading unit
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
