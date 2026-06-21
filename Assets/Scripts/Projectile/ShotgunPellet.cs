using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Straight-line pellet for the Shotgun Tower. Moves in a fixed direction,
/// deals damage on the first enemy it touches, then self-destructs.
/// Also self-destructs after lifetime expires (misses).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class ShotgunPellet : MonoBehaviour
{
    // ── Set by Effect_Launch_Shotgun ──────────────────────────────────
    public Vector2         direction;
    public float           moveSpeed    = 22f;
    public float           lifetime     = 0.38f;
    public Effect          impactEffect;
    public Ability_Effect  originAbility;
    public UnitParentClass caster;
    public Transform       casterTransform;
    public GameObject      originTower;

    private bool _hit;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (_hit) return;
        transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_hit) return;
        var hitUnit = other.GetComponent<UnitParentClass>();
        if (hitUnit == null || !hitUnit.isAlive) return;

        _hit = true;

        if (impactEffect != null)
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
