using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitManager : UnitParentClass
{
    public Collider2D myCollider;

    [Tooltip("Must match an id in Resources/Definitions/units.json")]
    public string definitionId;

    public int bounty;
    public int deathBlow = 1;

    // isDead compatibility shim — reads from UnitParentClass.isAlive
    public bool isDead => !isAlive;

    // ── Hit flash ────────────────────────────────────────────────────
    private SpriteRenderer _sr;
    private Color          _baseColor = Color.white;
    private Coroutine      _flashCoroutine;

    // ── Route ─────────────────────────────────────────────────────────
    private RouteFollower _follower;

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start()
    {
        if (!string.IsNullOrEmpty(definitionId) && lifeMax <= 0)
            ApplyDefinition(definitionId);

        if (lifeMax <= 0)  lifeMax  = 100f;
        if (speedMax <= 0) speedMax = 3f;

        isAlive      = true;
        lifeCurrent  = lifeMax;
        speedCurrent = speedMax;
        decayTimer   = 24;

        _follower  = GetComponent<RouteFollower>();
        _sr        = GetComponent<SpriteRenderer>();
        if (_sr != null) _baseColor = _sr.color;
    }

    public void ApplyDefinition(string id)
    {
        if (UnitDefinitionLibrary.Instance == null) return;
        if (!UnitDefinitionLibrary.Instance.TryGet(id, out var def)) return;

        definitionId    = def.id;
        lifeMax         = def.life;
        speedMax        = def.speed;
        physicalDefense = def.physicalDefense;
        bounty          = def.bounty;
        deathBlow       = def.deathBlow;
    }

    // ── Damage override (triggers hit flash) ─────────────────────────

    public override void TakeDamage(float damageAmount, float shieldBonus, float minimum, float maximum, DamageType type)
    {
        base.TakeDamage(damageAmount, shieldBonus, minimum, maximum, type);
        if (_sr != null)
        {
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(HitFlashRoutine());
        }
    }

    IEnumerator HitFlashRoutine()
    {
        _sr.color     = Color.red;
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed  += Time.deltaTime;
            _sr.color = Color.Lerp(Color.red, _baseColor, elapsed / 0.2f);
            yield return null;
        }
        _sr.color = _baseColor;
    }


    // ── Route assignment ──────────────────────────────────────────────

    public void AssignRoute(Route route)
    {
        if (_follower == null)
            _follower = gameObject.AddComponent<RouteFollower>();

        float speed = speedCurrent > 0 ? speedCurrent : speedMax;
        _follower.StartRoute(route, speed);
    }

    // ── Update ────────────────────────────────────────────────────────

    protected override void Update()
    {
        if (isAlive)
        {
            Move();

            if (lifeCurrent < 0)
                Die();
        }
        else
        {
            if (decayTimer > 0)
                decayTimer -= 1;
            else
                Destroy(gameObject);
        }
    }

    void Move()
    {
        if (_follower == null || !_follower.HasRoute)
            return;

        float speed = speedCurrent > 0 ? speedCurrent : speedMax;
        _follower.SetSpeed(speed);

        bool reachedEnd = _follower.Tick();

        if (reachedEnd)
        {
            LogicManager logic = GameObject.FindGameObjectWithTag("Logic")?.GetComponent<LogicManager>();
            if (logic != null)
                logic.UpdateLives(deathBlow * -1);

            Die();
        }
    }

    // ── Death ─────────────────────────────────────────────────────────

    public void Die()
    {
        isAlive = false;
        if (myCollider != null)
            myCollider.enabled = false;
    }
}
