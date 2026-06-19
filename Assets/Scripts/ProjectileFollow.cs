using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileFollow : MonoBehaviour
{
    public float moveSpeed;
    public GameObject target;

    // Effect pipeline fields — set by Effect_Launch_Missile
    public Effect impactEffect;
    public EffectContext originContext;

    private const float HitThreshold = 0.15f;

    void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position, target.transform.position, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.transform.position) <= HitThreshold)
            OnReachTarget();
    }

    void OnReachTarget()
    {
        if (impactEffect != null && originContext != null)
        {
            UnitParentClass hitUnit = target.GetComponent<UnitParentClass>();
            if (hitUnit != null)
            {
                var ctx = originContext.CloneForNewTarget(hitUnit);
                EffectExecutor.ExecuteEffect(impactEffect, ctx);
            }
        }
        Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer != 10) return;

        UnitParentClass unit = collision.gameObject.GetComponent<UnitParentClass>();
        if (unit == null) return;

        if (impactEffect != null && originContext != null)
        {
            var ctx = originContext.CloneForNewTarget(unit);
            EffectExecutor.ExecuteEffect(impactEffect, ctx);
        }
        Destroy(gameObject);
    }
}
