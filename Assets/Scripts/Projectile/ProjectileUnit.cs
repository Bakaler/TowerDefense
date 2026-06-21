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

    // ── Internal ──────────────────────────────────────────────────────
    private bool    _hit;
    private Vector3 _spawnPos;

    void Start()
    {
        _spawnPos = transform.position;
        var rb      = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
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
        Vector3 dir = destination - transform.position;
        if (faceDirection && dir.sqrMagnitude > 0.0001f)
            transform.up = dir.normalized;
        transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_hit) return;
        var hitUnit = other.GetComponent<UnitParentClass>();
        if (hitUnit == null) return;
        if (homing && target != null && other.transform != target) return;
        Impact(hitUnit);
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
