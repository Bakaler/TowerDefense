using UnityEngine;

/// <summary>
/// Generic data-driven minion built by MinionFactory from a MinionDefinition (minions.json).
/// State machine: Wander → Engage → Return → Rest.
/// A minion locks onto one target and stays on it until the target is dead
/// (or despawns) — no leash, no forced return. Only then does it fly home and
/// rest for restDuration before picking its next target, so single tough
/// targets are its strength and the downtime between targets is the cost.
/// Attacks fire a projectile (projectiles.json) through ProjectileFactory.
/// </summary>
public class Minion : MonoBehaviour
{
    // ── Set by MinionFactory ──────────────────────────────────────────
    [HideInInspector] public MinionDefinition def;
    public IMinionHost host;
    public float       range;          // engage search radius (usually the tower's range)
    public float       noticeRange;    // wander-state aggro radius
    public float       attackCooldown;
    public float       restDuration;
    public Effect      impactEffect;

    private enum State { Wander, Engage, Return, Rest }
    private State           _state = State.Wander;
    private Vector2         _wanderGoal;
    private float           _wanderTimer;
    private UnitParentClass _target;
    private float           _cooldownTimer;
    private float           _orbitDir;
    private float           _restTimer;
    private float           _noiseSeed;    // decorrelates each minion's swarm drift
    private float           _flipTimer;    // occasional orbit-direction reversal

    Vector2 Home => host != null ? (Vector2)host.HomeTransform.position : (Vector2)transform.position;

    void Awake()
    {
        _orbitDir  = Random.value > 0.5f ? 1f : -1f;
        _noiseSeed = Random.Range(0f, 1000f);
        _flipTimer = Random.Range(2f, 5f);
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
        // Committed to this target: only its death (or despawn) sends us home.
        if (_target == null || !_target.isAlive) { GoReturn(); return; }

        // Swarm feel instead of a clean orbit: each minion's preferred distance
        // breathes, its heading wobbles, and its speed varies — all on smooth
        // per-minion Perlin noise so the cloud stays on target without syncing up.
        float t           = Time.time;
        float desiredDist = def.orbitDist * Mathf.Lerp(0.5f, 1.6f, Mathf.PerlinNoise(_noiseSeed, t * 0.8f));
        float wobble      = (Mathf.PerlinNoise(_noiseSeed + 57.3f, t * 1.3f) - 0.5f) * 140f * Mathf.Deg2Rad;
        float speedMult   = Mathf.Lerp(0.75f, 1.25f, Mathf.PerlinNoise(_noiseSeed + 113f, t * 1.7f));

        // Now and then a bee reverses its circling direction
        _flipTimer -= Time.deltaTime;
        if (_flipTimer <= 0f)
        {
            _orbitDir  = -_orbitDir;
            _flipTimer = Random.Range(2f, 5f);
        }

        Vector2 toEnemy = (Vector2)_target.transform.position - (Vector2)transform.position;
        float   dist    = toEnemy.magnitude;
        Vector2 tangent = new Vector2(-toEnemy.y, toEnemy.x).normalized * _orbitDir;

        Vector2 move;
        if      (dist > desiredDist + 0.1f) move = (toEnemy.normalized * 0.7f + tangent * 0.5f).normalized;
        else if (dist < desiredDist - 0.1f) move = (-toEnemy.normalized * 0.5f + tangent * 0.7f).normalized;
        else                                move = tangent;

        // Rotate the heading by the wobble angle
        float cs = Mathf.Cos(wobble), sn = Mathf.Sin(wobble);
        move = new Vector2(move.x * cs - move.y * sn, move.x * sn + move.y * cs);

        transform.position = (Vector2)transform.position + move * def.engageSpeed * speedMult * Time.deltaTime;

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
        var hits = Physics2D.OverlapCircleAll(Home, searchRange, GameLayers.EnemyMask);
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
        });
    }
}
