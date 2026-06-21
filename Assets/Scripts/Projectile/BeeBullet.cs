using UnityEngine;

/// <summary>Homing bullet fired by a BeeUnit. Hits the first enemy it touches.</summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class BeeBullet : MonoBehaviour
{
    public UnitParentClass target;
    public float           moveSpeed  = 14f;
    public float           lifetime   = 0.6f;
    public float           damage     = 6f;
    public GameObject      originTower;

    private bool _hit;

    void Start() => Destroy(gameObject, lifetime);

    void Update()
    {
        if (_hit) return;
        if (target == null || !target.isAlive) { Destroy(gameObject); return; }
        transform.position = Vector2.MoveTowards(
            transform.position, target.transform.position, moveSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_hit) return;
        var unit = other.GetComponent<UnitParentClass>();
        if (unit == null || !unit.isAlive) return;
        if (target != null && unit != target) return;

        _hit = true;
        bool wasAlive = unit.lifeCurrent > 0f;
        unit.TakeDamage(damage, 0f, 0f, damage * 10f, DamageType.Physical);
        if (wasAlive && (unit.lifeCurrent <= 0f || !unit.isAlive))
        {
            originTower?.GetComponent<TowerInfo>()?.RegisterKill();
            float physical = BalanceManager.Instance != null ? BalanceManager.Instance.Physical : 0f;
            if (Random.value <= 0.15f + physical * 0.0025f)
                BountyDrop.Spawn(unit.transform.position, 1);
        }
        Destroy(gameObject);
    }
}
