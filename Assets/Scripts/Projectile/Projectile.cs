using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic data-driven projectile. Movement style, hit policy, and visuals come from a
/// ProjectileDefinition (projectiles.json); launch context (caster, target, impact effect)
/// is supplied by ProjectileFactory.Spawn. Replaces the old per-type scripts
/// (ProjectileUnit, ShotgunPellet, BeeBullet, BoomerangProjectile).
/// </summary>
public class Projectile : MonoBehaviour
{
    public enum MovementMode { Straight, Homing, Arc, Orbit }

    // ── Set by ProjectileFactory ──────────────────────────────────────
    [HideInInspector] public ProjectileDefinition def;
    public MovementMode    movement;
    public float           speed;      // def.speed with per-shot jitter applied
    public float           lifetime;   // def.lifetime with per-shot jitter applied
    public Effect          impactEffect;
    public UnitParentClass caster;
    public Transform       casterTransform;
    public Ability_Effect  originAbility;
    public GameObject      originTower;
    public UnitParentClass targetUnit;   // homing
    public Vector3         targetPoint;  // arc
    public Vector2         direction;    // straight / orbit launch direction
    /// <summary>>0 replaces the impact effect's base damage (EffectContext.DamageOverride).</summary>
    public float           damageOverride;
    /// <summary>Remaining hits before the projectile breaks. 0 = unlimited.
    /// Set by ProjectileFactory from def.maxHits + per-tier bonus.</summary>
    public int             maxHits;

    /// <summary>All live projectiles — lets units (e.g. Disruptor) find and destroy them.</summary>
    public static readonly HashSet<Projectile> Active = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() => Active.Clear();

    void OnEnable()  => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    // ── Internal ──────────────────────────────────────────────────────
    private bool    _done;
    private Vector3 _spawnPos;
    private readonly HashSet<UnitParentClass> _hitUnits = new();

    // Arc state
    private float _arcTotalDist;
    private float _arcTravelled;

    // Orbit state
    private Vector2 _orbitCenter;
    private float   _orbitRadius;
    private float   _orbitAngle;
    private float   _orbitSweep;
    private bool    _orbitReturnLeg;

    /// <summary>Called by ProjectileFactory after all fields are assigned.</summary>
    public void Launch()
    {
        _spawnPos = transform.position;

        switch (movement)
        {
            case MovementMode.Straight:
                if (def.faceDirection && direction.sqrMagnitude > 0.0001f)
                    transform.up = direction.normalized;
                break;

            case MovementMode.Arc:
                _arcTotalDist = Vector3.Distance(_spawnPos, targetPoint);
                break;

            case MovementMode.Orbit:
                // Size the loop so its apex (2 × radius) reaches the target it was
                // thrown at; def.arcRadius caps the maximum loop size.
                Vector2 casterPos = _spawnPos;
                float span = ((Vector2)targetPoint - casterPos).magnitude;
                _orbitRadius = span > 0.05f
                    ? Mathf.Min(span * 0.5f, Mathf.Max(0.1f, def.arcRadius))
                    : def.arcRadius;
                _orbitCenter = casterPos + direction.normalized * _orbitRadius;
                Vector2 toStart = casterPos - _orbitCenter;
                _orbitAngle = Mathf.Atan2(toStart.y, toStart.x) * Mathf.Rad2Deg;
                break;
        }

        if (lifetime > 0f)
            Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (_done) return;

        switch (movement)
        {
            case MovementMode.Straight: UpdateStraight(); break;
            case MovementMode.Homing:   UpdateHoming();   break;
            case MovementMode.Arc:      UpdateArc();      break;
            case MovementMode.Orbit:    UpdateOrbit();    break;
        }
    }

    // ── Movement ──────────────────────────────────────────────────────

    void UpdateStraight()
    {
        transform.position += (Vector3)(direction * (speed * Time.deltaTime));
    }

    void UpdateHoming()
    {
        if (targetUnit == null || !targetUnit.isAlive) { Destroy(gameObject); return; }

        Vector3 destination = targetUnit.transform.position;
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);

        if (def.faceDirection)
        {
            Vector3 dir = destination - transform.position;
            if (dir.sqrMagnitude > 0.0001f) transform.up = dir.normalized;
        }
    }

    void UpdateArc()
    {
        float step = speed * Time.deltaTime;

        if (_arcTotalDist > 0f)
        {
            _arcTravelled += step;
            float t      = Mathf.Clamp01(_arcTravelled / _arcTotalDist);
            Vector3 flat = Vector3.Lerp(_spawnPos, targetPoint, t);
            flat.y      += Mathf.Sin(t * Mathf.PI) * (_arcTotalDist * 0.35f);
            transform.position = flat;
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPoint, step);
        }

        if (Vector3.Distance(transform.position, targetPoint) < 0.25f)
            DetonateAtPoint();
    }

    void UpdateOrbit()
    {
        float delta = def.sweepSpeed * Time.deltaTime;
        _orbitAngle += delta;
        _orbitSweep += delta;

        // Switch to return leg at 180° — clear hit set to allow re-hits
        if (!_orbitReturnLeg && _orbitSweep >= 180f)
        {
            _orbitReturnLeg = true;
            _hitUnits.Clear();
        }

        if (_orbitSweep >= 360f) { Destroy(gameObject); return; }

        float rad = _orbitAngle * Mathf.Deg2Rad;
        transform.position = new Vector3(
            _orbitCenter.x + Mathf.Cos(rad) * _orbitRadius,
            _orbitCenter.y + Mathf.Sin(rad) * _orbitRadius,
            0f);

        // Tangent-facing + extra spin
        float tangentAngle = _orbitAngle + 90f;
        float spin         = def.sweepSpeed > 0f ? _orbitSweep / def.sweepSpeed * def.spinSpeed : 0f;
        transform.rotation = Quaternion.Euler(0f, 0f, tangentAngle + spin);

        OrbitCheckHits();
    }

    // ── Hit detection ─────────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_done || movement == MovementMode.Orbit) return;

        if (def.blockedByShields)
        {
            var shield = other.GetComponent<ShieldBubble>();
            if (shield != null)
            {
                shield.AbsorbHit(def.shieldAbsorb);
                _done = true;
                Destroy(gameObject);
                return;
            }
        }

        if (movement == MovementMode.Arc) return; // detonates on arrival, not on touch

        var unit = other.GetComponent<UnitParentClass>();
        if (unit == null || !unit.isAlive) return;
        if (movement == MovementMode.Homing && targetUnit != null && unit != targetUnit) return;

        HitUnit(unit);
    }

    void OrbitCheckHits()
    {
        if (impactEffect == null) return;

        Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, def.hitRadius);
        foreach (var col in overlaps)
        {
            var unit = col.GetComponent<UnitParentClass>();
            if (unit == null || !unit.isAlive) continue;
            if (_hitUnits.Contains(unit)) continue;

            _hitUnits.Add(unit);
            ApplyImpact(unit);
            if (ConsumeHit()) { _done = true; Destroy(gameObject); return; }
        }
    }

    /// <summary>Spends one hit from the budget. Returns true when the projectile breaks.</summary>
    bool ConsumeHit()
    {
        if (maxHits <= 0) return false;   // 0 = unlimited
        maxHits--;
        return maxHits <= 0;
    }

    void HitUnit(UnitParentClass unit)
    {
        if (def.pierce && _hitUnits.Contains(unit)) return;
        _hitUnits.Add(unit);

        if (def.drawImpactLine)
        {
            var go  = new GameObject("[ChainLine]");
            var vis = go.AddComponent<ChainLightningVisual>();
            vis.SetPath(new List<Vector3> { _spawnPos, unit.transform.position });
        }

        ApplyImpact(unit);

        if (!def.pierce || ConsumeHit())
        {
            _done = true;
            Destroy(gameObject);
        }
    }

    void DetonateAtPoint()
    {
        _done = true;
        if (!string.IsNullOrEmpty(def.impactSoundId))
            AudioManager.Play(def.impactSoundId);
        if (impactEffect != null)
        {
            var ctx = BuildContext(null);
            ctx.TargetPoint = targetPoint;
            ctx.AimOrigin2D = (Vector2)targetPoint;
            EffectExecutor.ExecuteEffect(impactEffect, ctx);
        }
        Destroy(gameObject);
    }

    void ApplyImpact(UnitParentClass unit)
    {
        if (!string.IsNullOrEmpty(def.impactSoundId))
            AudioManager.Play(def.impactSoundId);
        if (impactEffect == null) return;
        var ctx = BuildContext(unit);
        EffectExecutor.ExecuteEffect(impactEffect, ctx);
    }

    EffectContext BuildContext(UnitParentClass target)
    {
        return new EffectContext
        {
            Caster          = caster,
            CasterTransform = casterTransform ?? caster?.transform,
            Target          = target,
            TargetPoint     = target != null ? target.transform.position : targetPoint,
            OriginAbility   = originAbility,
            OriginTower     = originTower,
            DamageOverride  = damageOverride,
            CustomData      = new Dictionary<string, object>(),
        };
    }
}
