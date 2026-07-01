using System.Collections.Generic;
using UnityEngine;

/// <summary>Homing bullet fired by a Drone. Hits the first enemy it touches.</summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class BeeBullet : MonoBehaviour
{
    public UnitParentClass target;
    public float           moveSpeed  = 14f;
    public float           lifetime   = 0.6f;
    public float           damage     = 6f;
    public Effect          impactEffect;
    public GameObject      originTower;

    private bool _hit;

    void Start() => Destroy(gameObject, lifetime);

    void Update()
    {
        if (_hit) return;
        if (target == null || !target.isAlive) { Destroy(gameObject); return; }
        Vector2 dir = (Vector2)(target.transform.position - transform.position);
        transform.position = Vector2.MoveTowards(transform.position, target.transform.position, moveSpeed * Time.deltaTime);
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_hit) return;
        var unit = other.GetComponent<UnitParentClass>();
        if (unit == null || !unit.isAlive) return;
        if (target != null && unit != target) return;

        _hit = true;

        if (impactEffect != null)
        {
            var context = new EffectContext
            {
                Target          = unit,
                OriginTower     = originTower,
                CasterTransform = transform,
                DamageOverride  = damage,
                CustomData      = new Dictionary<string, object>(),
            };
            EffectExecutor.ExecuteEffect(impactEffect, context);
        }
        else
        {
            // Fallback: direct damage if effect not set
            bool wasAlive = unit.lifeCurrent > 0f;
            unit.TakeDamage(damage, 0f, 0f, damage * 10f, DamageType.Physical);
            if (wasAlive && (unit.lifeCurrent <= 0f || !unit.isAlive))
            {
                originTower?.GetComponent<TowerInfo>()?.RegisterKill();
                BountyDrop.TrySpawn(unit.transform.position, unit as UnitManager);
            }
        }

        Destroy(gameObject);
    }
}
