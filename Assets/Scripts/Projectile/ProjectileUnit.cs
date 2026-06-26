using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class ProjectileUnit : MonoBehaviour
{
    // ── Set by Effect_Launch_Missile after instantiation ─────────────
    public UnitParentClass  caster;
    public Transform        casterTransform;
    public Ability_Effect   originAbility;
    public Effect           impactEffect;
    public Transform        target;
    public Vector3          targetPoint;
    public bool             homing        = true;
    public bool             drawImpactLine = false;
    public GameObject       originTower;

    // ── Config ────────────────────────────────────────────────────────
    public float moveSpeed     = 8f;
    public float lifetime      = 7f;
    public bool  faceDirection = false;
    public bool  piercing      = false;   // if true, passes through ShieldBubble
    public bool  arcFlight     = false;   // lob arc using sine Y offset (mortar)

    // ── Internal ──────────────────────────────────────────────────────
    private bool    _hit;
    private Vector3 _spawnPos;
    private float   _totalDist;
    private float   _travelledDist;

    void Start()
    {
        _spawnPos     = transform.position;
        _totalDist    = arcFlight ? Vector3.Distance(transform.position, targetPoint) : 0f;
        _travelledDist = 0f;
        var rb        = GetComponent<Rigidbody2D>();
        rb.bodyType   = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (_hit) return;

        if (homing && (target == null || !target.GetComponent<UnitParentClass>()?.isAlive == true))
        {
            Destroy(gameObject);
            return;
        }

        Vector3 destination = (homing && target != null) ? target.position : targetPoint;
        float step = moveSpeed * Time.deltaTime;

        if (arcFlight && _totalDist > 0f)
        {
            _travelledDist += step;
            float t       = Mathf.Clamp01(_travelledDist / _totalDist);
            Vector3 flat  = Vector3.Lerp(_spawnPos, targetPoint, t);
            flat.y       += Mathf.Sin(t * Mathf.PI) * (_totalDist * 0.35f);
            transform.position = flat;
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, step);
        }

        Vector3 dir = destination - transform.position;
        if (faceDirection && dir.sqrMagnitude > 0.0001f)
            transform.up = dir.normalized;

        // Non-homing mortar: detonate when close enough to target point
        if (!homing && Vector3.Distance(transform.position, targetPoint) < 0.25f)
            ImpactPoint();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_hit) return;

        // Shield interception for non-piercing missiles
        if (!piercing)
        {
            var shield = other.GetComponent<ShieldBubble>();
            if (shield != null) { shield.AbsorbHit(10f); _hit = true; Destroy(gameObject); return; }
        }

        if (!homing) return;   // mortar detonates on arrival, not on trigger
        var hitUnit = other.GetComponent<UnitParentClass>();
        if (hitUnit == null) return;
        if (homing && target != null && other.transform != target) return;
        Impact(hitUnit);
    }

    void ImpactPoint()
    {
        _hit = true;
        if (impactEffect != null)
        {
            var ctx = new EffectContext
            {
                Caster          = caster,
                CasterTransform = casterTransform ?? caster?.transform,
                Target          = null,
                TargetPoint     = targetPoint,
                AimOrigin2D     = targetPoint,
                OriginAbility   = originAbility,
                OriginTower     = originTower,
                CustomData      = new Dictionary<string, object>(),
            };
            EffectExecutor.ExecuteEffect(impactEffect, ctx);
        }
        Destroy(gameObject);
    }

    void Impact(UnitParentClass hitUnit)
    {
        _hit = true;

        if (drawImpactLine)
        {
            var go  = new GameObject("[ChainLine]");
            var vis = go.AddComponent<ChainLightningVisual>();
            vis.SetPath(new List<Vector3> { _spawnPos, hitUnit.transform.position });
        }

        if (impactEffect != null && hitUnit != null && hitUnit.isAlive)
        {
            var ctx = new EffectContext
            {
                Caster          = caster,
                CasterTransform = casterTransform ?? caster?.transform,
                Target          = hitUnit,
                TargetPoint     = hitUnit.transform.position,
                OriginAbility   = originAbility,
                OriginTower     = originTower,
                CustomData      = new Dictionary<string, object>(),
            };
            EffectExecutor.ExecuteEffect(impactEffect, ctx);
        }

        Destroy(gameObject);
    }
}
