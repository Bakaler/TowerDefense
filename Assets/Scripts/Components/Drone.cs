using UnityEngine;

/// <summary>
/// Individual drone spawned by DroneSwarm.
/// State machine: Wander → Engage → Return → Rest → Wander
/// Drones can only be away from the hive for maxAwayTime seconds,
/// then must fly back and rest for restDuration before re-deploying.
/// </summary>
public class Drone : MonoBehaviour
{
    public DroneSwarm swarm;
    public float      range;
    public float      cooldown;
    public float      damage;
    public Effect     impactEffect;
    public float      bulletSpeed;
    public float      bulletLifetime;
    public Color      bulletColor;
    public Sprite     bulletSprite;

    public float moveSpeed    = 2.8f;
    public float engageSpeed  = 4.2f;
    public float returnSpeed  = 5.5f;
    public float noticeRange  = 2.5f;
    public float orbitDist    = 0.6f;
    public float wanderRadius = 1.1f;
    public float maxAwayTime  = 6f;
    public float restDuration = 1f;

    private enum State { Wander, Engage, Return, Rest }
    private State           _state = State.Wander;
    private Vector2         _wanderGoal;
    private float           _wanderTimer;
    private UnitParentClass _target;
    private float           _cooldownTimer;
    private float           _orbitDir;
    private float           _awayTimer;
    private float           _restTimer;

    void Awake()
    {
        _orbitDir = Random.value > 0.5f ? 1f : -1f;
        PickWanderGoal();
    }

    void Update()
    {
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
        Vector2 goal = (Vector2)swarm.transform.position + _wanderGoal;
        transform.position = Vector2.MoveTowards(pos, goal, moveSpeed * Time.deltaTime);

        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f || Vector2.Distance(pos, goal) < 0.15f)
            PickWanderGoal();
    }

    void PickWanderGoal()
    {
        float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(0.2f, wanderRadius);
        _wanderGoal  = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        _wanderTimer = Random.Range(0.8f, 2f);
    }

    // ── Engage ────────────────────────────────────────────────────────

    void UpdateEngage()
    {
        _awayTimer += Time.deltaTime;
        if (_awayTimer >= maxAwayTime) { GoReturn(); return; }

        if (_target == null || !_target.isAlive ||
            Vector2.Distance(swarm.transform.position, _target.transform.position) > range)
        {
            _target = FindNearest(range);
            if (_target == null) { _state = State.Wander; PickWanderGoal(); return; }
        }

        Vector2 toEnemy = (Vector2)_target.transform.position - (Vector2)transform.position;
        float   dist    = toEnemy.magnitude;
        Vector2 tangent = new Vector2(-toEnemy.y, toEnemy.x).normalized * _orbitDir;
        Vector2 move    = dist > orbitDist + 0.1f
            ? (toEnemy.normalized * 0.7f + tangent * 0.5f).normalized
            : tangent;

        transform.position = (Vector2)transform.position + move * engageSpeed * Time.deltaTime;

        if (_cooldownTimer <= 0f) { Fire(_target); _cooldownTimer = cooldown; }
    }

    // ── Return ────────────────────────────────────────────────────────

    void GoReturn()
    {
        _target = null;
        _state  = State.Return;
    }

    void UpdateReturn()
    {
        Vector2 hive = swarm.transform.position;
        transform.position = Vector2.MoveTowards(transform.position, hive, returnSpeed * Time.deltaTime);

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
        var hits = Physics2D.OverlapCircleAll(swarm.transform.position, searchRange);
        UnitParentClass best = null; float bestDist = float.MaxValue;
        foreach (var h in hits)
        {
            var u = h.GetComponent<UnitParentClass>();
            if (u == null || !u.isAlive) continue;
            float d = Vector2.Distance(swarm.transform.position, u.transform.position);
            if (d < bestDist) { bestDist = d; best = u; }
        }
        return best;
    }

    void Fire(UnitParentClass target)
    {
        var go = new GameObject("DroneBullet");
        go.transform.position   = transform.position;
        go.transform.localScale = Vector3.one * 0.5f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic; rb.gravityScale = 0f;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.15f; col.isTrigger = true;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = BulletSprite(); sr.color = bulletColor;
        sr.sortingLayerName = "Units"; sr.sortingOrder = 10;

        var b = go.AddComponent<BeeBullet>();
        b.target        = target;
        b.moveSpeed     = bulletSpeed;
        b.lifetime      = bulletLifetime;
        b.damage        = swarm != null ? swarm.GetDamage() : damage;
        b.impactEffect  = impactEffect;
        b.originTower   = swarm != null ? swarm.gameObject : null;
    }

    Sprite BulletSprite()
    {
        if (bulletSprite != null) return bulletSprite;
        // Fallback: generated circle
        const int S = 6;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        float c = S / 2f, r = S / 2f - 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x - c + 0.5f, dy = y - c + 0.5f;
                tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
            }
        tex.Apply(); tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }
}
