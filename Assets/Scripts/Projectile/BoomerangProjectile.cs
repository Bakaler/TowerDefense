using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sweeps a full circular arc from the caster toward the target direction and back.
/// Hits every unit it overlaps during travel — piercing, with re-hit allowed on return leg.
/// </summary>
public class BoomerangProjectile : MonoBehaviour
{
    // ── Set by Effect_Launch_Boomerang ────────────────────────────────
    public UnitParentClass  caster;
    public Transform        casterTransform;
    public Ability_Effect   originAbility;
    public Effect           impactEffect;
    public GameObject       originTower;
    public float            arcRadius   = 4f;   // distance of the circle center from caster
    public float            sweepSpeed  = 180f; // degrees per second
    public float            hitRadius   = 0.4f; // overlap check radius for piercing hits
    public float            spinSpeed   = 0f;   // extra self-rotation degrees per second

    // ── Internal ─────────────────────────────────────────────────────
    private Vector2 _center;
    private float   _radius;
    private float   _startAngle;
    private float   _currentAngle;
    private float   _totalSweep;   // 360 degrees
    private bool    _returnLeg;    // true after 180°, allows re-hits

    private readonly HashSet<UnitParentClass> _hitThisLeg = new HashSet<UnitParentClass>();

    public void Launch(Vector2 casterPos, Vector2 targetDir)
    {
        // Circle center is arcRadius ahead of the caster in the target direction
        _center      = casterPos + targetDir.normalized * arcRadius;
        _radius      = arcRadius;

        // Start angle: angle from center back to caster
        Vector2 toStart = casterPos - _center;
        _startAngle     = Mathf.Atan2(toStart.y, toStart.x) * Mathf.Rad2Deg;
        _currentAngle   = _startAngle;
        _totalSweep     = 0f;
        _returnLeg      = false;

        transform.position = casterPos;
    }

    void Update()
    {
        float delta = sweepSpeed * Time.deltaTime;
        _currentAngle += delta;
        _totalSweep   += delta;

        // Switch to return leg at 180° — clear hit set to allow re-hits
        if (!_returnLeg && _totalSweep >= 180f)
        {
            _returnLeg = true;
            _hitThisLeg.Clear();
        }

        // Full circle complete — destroy
        if (_totalSweep >= 360f)
        {
            Destroy(gameObject);
            return;
        }

        float rad = _currentAngle * Mathf.Deg2Rad;
        transform.position = new Vector3(
            _center.x + Mathf.Cos(rad) * _radius,
            _center.y + Mathf.Sin(rad) * _radius,
            0f);

        // Tangent-facing + extra spin
        float tangentAngle = _currentAngle + 90f;
        float spin         = _totalSweep / sweepSpeed * spinSpeed; // spin proportional to time elapsed
        transform.rotation = Quaternion.Euler(0f, 0f, tangentAngle + spin);

        CheckHits();
    }

    void CheckHits()
    {
        if (impactEffect == null) return;

        Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, hitRadius);
        foreach (var col in overlaps)
        {
            var unit = col.GetComponent<UnitParentClass>();
            if (unit == null || !unit.isAlive) continue;
            if (_hitThisLeg.Contains(unit)) continue;

            _hitThisLeg.Add(unit);

            var ctx = new EffectContext
            {
                Caster          = caster,
                CasterTransform = casterTransform,
                Target          = unit,
                TargetPoint     = unit.transform.position,
                OriginAbility   = originAbility,
                OriginTower     = originTower,
                CustomData      = new System.Collections.Generic.Dictionary<string, object>(),
            };
            EffectExecutor.ExecuteEffect(impactEffect, ctx);
        }
    }
}
