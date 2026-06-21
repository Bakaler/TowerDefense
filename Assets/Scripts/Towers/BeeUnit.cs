using UnityEngine;

/// <summary>
/// A bee that wanders randomly within the tower radius.
/// When an enemy enters notice range it flies toward them, circling while attacking.
/// </summary>
public class BeeUnit : MonoBehaviour
{
    public BeeTower tower;
    public float    range;
    public float    cooldown;
    public float    damage;
    public float    bulletSpeed;
    public float    bulletLifetime;
    public Color    bulletColor;

    // Movement
    public float moveSpeed     = 2.8f;
    public float engageSpeed   = 4.2f;
    public float noticeRange   = 2.5f;   // switches from wander → engage
    public float orbitDist     = 0.6f;   // how close bees circle around enemy
    public float wanderRadius  = 1.1f;   // max wander distance from tower center

    private enum State { Wander, Engage }
    private State           _state      = State.Wander;
    private Vector2         _wanderGoal;
    private float           _wanderTimer;
    private UnitParentClass _target;
    private float           _cooldownTimer;
    private float           _orbitDir;   // +1 or -1 — randomised per bee

    void Awake()
    {
        // Each bee orbits its target in a random direction so they swarm naturally
        _orbitDir = Random.value > 0.5f ? 1f : -1f;
        PickNewWanderGoal();
    }

    void Update()
    {
        _cooldownTimer -= Time.deltaTime;

        switch (_state)
        {
            case State.Wander: UpdateWander(); break;
            case State.Engage: UpdateEngage(); break;
        }
    }

    // ── Wander ────────────────────────────────────────────────────────

    void UpdateWander()
    {
        // Check for nearby enemy
        var nearest = FindNearest(noticeRange);
        if (nearest != null)
        {
            _target = nearest;
            _state  = State.Engage;
            return;
        }

        // Drift toward goal
        Vector2 pos  = transform.position;
        Vector2 goal = (Vector2)tower.transform.position + _wanderGoal;
        transform.position = Vector2.MoveTowards(pos, goal, moveSpeed * Time.deltaTime);

        // Wobble path slightly
        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f || Vector2.Distance(pos, goal) < 0.15f)
            PickNewWanderGoal();
    }

    void PickNewWanderGoal()
    {
        float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(0.2f, wanderRadius);
        _wanderGoal  = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        _wanderTimer = Random.Range(0.8f, 2.0f);
    }

    // ── Engage ────────────────────────────────────────────────────────

    void UpdateEngage()
    {
        // Drop target if dead or out of range
        if (_target == null || !_target.isAlive ||
            Vector2.Distance(tower.transform.position, _target.transform.position) > range)
        {
            _target = FindNearest(range);
            if (_target == null) { _state = State.Wander; PickNewWanderGoal(); return; }
        }

        Vector2 pos       = transform.position;
        Vector2 toEnemy   = (Vector2)_target.transform.position - pos;
        float   dist      = toEnemy.magnitude;

        // Tangent direction around enemy (perpendicular to toEnemy, signed by orbitDir)
        Vector2 tangent   = new Vector2(-toEnemy.y, toEnemy.x).normalized * _orbitDir;

        Vector2 move;
        if (dist > orbitDist + 0.1f)
        {
            // Close in while circling
            move = (toEnemy.normalized * 0.7f + tangent * 0.5f).normalized;
        }
        else
        {
            // In orbit range — circle around
            move = tangent;
        }

        transform.position = (Vector2)transform.position + move * engageSpeed * Time.deltaTime;

        // Fire when cooled down
        if (_cooldownTimer <= 0f)
        {
            Fire(_target);
            _cooldownTimer = cooldown;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    UnitParentClass FindNearest(float searchRange)
    {
        var hits = Physics2D.OverlapCircleAll(tower.transform.position, searchRange);
        UnitParentClass best     = null;
        float           bestDist = float.MaxValue;
        foreach (var h in hits)
        {
            var u = h.GetComponent<UnitParentClass>();
            if (u == null || !u.isAlive) continue;
            float d = Vector2.Distance(tower.transform.position, u.transform.position);
            if (d < bestDist) { bestDist = d; best = u; }
        }
        return best;
    }

    void Fire(UnitParentClass target)
    {
        var go = new GameObject("BeeBullet");
        go.transform.position   = transform.position;
        go.transform.localScale = Vector3.one * 0.22f;

        var rb          = go.AddComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var col       = go.AddComponent<CircleCollider2D>();
        col.radius    = 0.15f;
        col.isTrigger = true;

        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sprite           = BulletSprite();
        sr.color            = bulletColor;
        sr.sortingLayerName = "Units";
        sr.sortingOrder     = 10;

        var proj           = go.AddComponent<BeeBullet>();
        proj.target        = target;
        proj.moveSpeed     = bulletSpeed;
        proj.lifetime      = bulletLifetime;
        proj.damage        = tower != null ? tower.GetDamage() : damage;
        proj.originTower   = tower != null ? tower.gameObject : null;
    }

    static Sprite _bulletSprite;
    static Sprite BulletSprite()
    {
        if (_bulletSprite != null) return _bulletSprite;
        const int S = 6;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        float c = S / 2f, r = S / 2f - 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x - c + 0.5f, dy = y - c + 0.5f;
                tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        _bulletSprite  = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return _bulletSprite;
    }
}
