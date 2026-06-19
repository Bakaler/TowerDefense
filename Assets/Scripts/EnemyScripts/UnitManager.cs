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

    // ── Route ─────────────────────────────────────────────────────────
    private RouteFollower _follower;

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start()
    {
        // If spawned via UnitFactory, stats are already applied — skip lookup.
        // If spawned directly (legacy), try to load from definition.
        if (!string.IsNullOrEmpty(definitionId) && lifeMax <= 0)
            ApplyDefinition(definitionId);

        // Final fallbacks so unit is never stuck at 0 hp / 0 speed
        if (lifeMax <= 0)  lifeMax  = 100f;
        if (speedMax <= 0) speedMax = 3f;

        isAlive      = true;
        lifeCurrent  = lifeMax;
        speedCurrent = speedMax;
        decayTimer   = 24;

        _follower = GetComponent<RouteFollower>();
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

    // ── Route assignment ──────────────────────────────────────────────

    /// <summary>Called by UnitSpawner after the unit is built.</summary>
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
            return; // no route assigned yet

        // Keep RouteFollower's speed in sync (in case a slow/haste effect changed speedCurrent)
        float speed = speedCurrent > 0 ? speedCurrent : speedMax;
        _follower.SetSpeed(speed);

        bool reachedEnd = _follower.Tick();

        if (reachedEnd)
        {
            // Unit reached the terminus — deal damage to the player's base
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
