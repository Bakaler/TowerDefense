using UnityEngine;

/// <summary>
/// Generic data-driven minion built by MinionFactory from a MinionDefinition (minions.json).
/// State machine: Wander → Engage → Return → Rest.
/// Minions can only be away from the hive for maxAwayTime seconds, then must fly
/// back and rest for restDuration before re-deploying. Attacks fire a projectile
/// (projectiles.json) through ProjectileFactory.
/// </summary>
public class Minion : MonoBehaviour
{
    // ── Set by MinionFactory ──────────────────────────────────────────
    [HideInInspector] public MinionDefinition def;
    public IMinionHost host;
    public float       range;          // engage search radius (usually the tower's range)
    public float       noticeRange;    // wander-state aggro radius
    public float       attackCooldown;
    public float       maxAwayTime;
    public float       restDuration;
    public Effect      impactEffect;

    private enum State { Wander, Engage, Return, Rest }
    private State           _state = State.Wander;
    private Vector2         _wanderGoal;
    private float           _wanderTimer;
    private UnitParentClass _target;
    private float           _cooldownTimer;
    private float           _orbitDir;
    private float           _awayTimer;
    private float           _restTimer;

    Vector2 Home => host != null ? (Vector2)host.HomeTransform.position : (Vector2)transform.position;

    void Awake()
    {
        _orbitDir = Random.value > 0.5f ? 1f : -1f;
    }

    void Start()
    {
        PickWanderGoal();
    }

    void Update()
    {
        if (def == null) return;
        _cooldownTimer -= Time.deltaTime;

        switch (_state)
        {
            case State.Wander: UpdateWander(); break;
            case State.Engage: UpdateEngage(); break;
            case State.Return: UpdateReturn(); break;
            case State.Rest:   UpdateRest();   break;
        }
    }

    // ── Wander ────────────────────────────────────────────────────────

    void UpdateWander()
    {
        _awayTimer += Time.deltaTime;
        if (_awayTimer >= maxAwayTime) { GoReturn(); return; }

        var nearest = FindNearest(noticeRange);
        if (nearest != null) { _target = nearest; _state = State.Engage; return; }

        Vector2 pos  = transform.position;
        Vector2 goal = Home + _wanderGoal;
        transform.position = Vector2.MoveTowards(pos, goal, def.moveSpeed * Time.deltaTime);

        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f || Vector2.Distance(pos, goal) < 0.15f)
            PickWanderGoal();
    }

    void PickWanderGoal()
    {
        float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(0.2f, def.wanderRadius);
        _wanderGoal  = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        _wanderTimer = Random.Range(0.8f, 2f);
    }

    // ── Engage ────────────────────────────────────────────────────────

    void UpdateEngage()
    {
        _awayTimer += Time.deltaTime;
        if (_awayTimer >= maxAwayTime) { GoReturn(); return; }

        if (_target == null || !_target.isAlive ||
            Vector2.Distance(Home, _target.transform.position) > range)
        {
            _target = FindNearest(range);
            if (_target == null) { _state = State.Wander; PickWanderGoal(); return; }
        }

        Vector2 toEnemy = (Vector2)_target.transform.position - (Vector2)transform.position;
        float   dist    = toEnemy.magnitude;
        Vector2 tangent = new Vector2(-toEnemy.y, toEnemy.x).normalized * _orbitDir;
        Vector2 move    = dist > def.orbitDist + 0.1f
            ? (toEnemy.normalized * 0.7f + tangent * 0.5f).normalized
            : tangent;

        transform.position = (Vector2)transform.position + move * def.engageSpeed * Time.deltaTime;

        if (_cooldownTimer <= 0f) { Fire(_target); _cooldownTimer = attackCooldown; }
    }

    // ── Return ────────────────────────────────────────────────────────

    void GoReturn()
    {
        _target = null;
        _state  = State.Return;
    }

    void UpdateReturn()
    {
        Vector2 hive = Home;
        transform.position = Vector2.MoveTowards(transform.position, hive, def.returnSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, hive) < 0.1f)
        {
            transform.position = hive;
            _awayTimer = 0f;
            _restTimer = restDuration;
            _state     = State.Rest;
        }
    }

    // ── Rest ──────────────────────────────────────────────────────────

    void UpdateRest()
    {
        _restTimer -= Time.deltaTime;
        if (_restTimer <= 0f)
        {
            _state = State.Wander;
            PickWanderGoal();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    UnitParentClass FindNearest(float searchRange)
    {
        var hits = Physics2D.OverlapCircleAll(Home, searchRange);
        UnitParentClass best = null; float bestDist = float.MaxValue;
        foreach (var h in hits)
        {
            var u = h.GetComponent<UnitParentClass>();
            if (u == null || !u.isAlive) continue;
            float d = Vector2.Distance(Home, u.transform.position);
            if (d < bestDist) { bestDist = d; best = u; }
        }
        return best;
    }

    void Fire(UnitParentClass target)
    {
        ProjectileFactory.Spawn(def.projectileId, new ProjectileSpawnArgs
        {
            origin          = transform.position,
            targetUnit      = target,
            impactEffect    = impactEffect,
            casterTransform = transform,
            originTower     = host?.HostObject,
            damageOverride  = host != null ? host.GetMinionDamage() : 0f,
        });
    }
}
