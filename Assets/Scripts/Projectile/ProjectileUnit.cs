using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Homing projectile unit. Built entirely in code by Effect_Launch_Missile — no prefab.
/// Moves toward its target each frame; when the trigger collider overlaps the target
/// it fires impactEffect and destroys itself.
/// Mirrors Eternal Labyrinth's ProjectileUnit pattern adapted for 2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class ProjectileUnit : MonoBehaviour
{
    // ── Set by Effect_Launch_Missile after instantiation ─────────────
    public UnitParentClass  caster;
    public Ability_Effect   originAbility;
    public Effect           impactEffect;
    public Transform        target;
    public Vector3          targetPoint;
    public bool             homing     = true;

    // ── Config ────────────────────────────────────────────────────────
    public float moveSpeed = 8f;
    public float lifetime  = 7f;

    // ── Internal ──────────────────────────────────────────────────────
    private bool _hit;

    void Start()
    {
        var rb      = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (_hit) return;

        // If homing missile loses its target, self-destruct
        if (homing && (target == null || !target.GetComponent<UnitParentClass>()?.isAlive == true))
        {
            Destroy(gameObject);
            return;
        }

        Vector3 destination = (homing && target != null)
            ? target.position
            : targetPoint;

        transform.position = Vector3.MoveTowards(
            transform.position, destination, moveSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_hit) return;

        var hitUnit = other.GetComponent<UnitParentClass>();
        if (hitUnit == null) return;

        // If homing, only accept collision with the designated target
        if (homing && target != null && other.transform != target) return;

        Impact(hitUnit);
    }

    void Impact(UnitParentClass hitUnit)
    {
        _hit = true;

        if (impactEffect != null && hitUnit != null && hitUnit.isAlive)
        {
            var ctx = new EffectContext
            {
                Caster          = caster,
                CasterTransform = caster != null ? caster.transform : null,
                Target          = hitUnit,
                TargetPoint     = hitUnit.transform.position,
                OriginAbility   = originAbility,
                CustomData      = new Dictionary<string, object>(),
            };
            EffectExecutor.ExecuteEffect(impactEffect, ctx);
        }

        Destroy(gameObject);
    }
}
