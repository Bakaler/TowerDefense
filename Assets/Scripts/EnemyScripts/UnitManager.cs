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

    /// <summary>Rotate toward the movement direction (set from UnitDefinition.rotateToMovement).</summary>
    public bool rotateToMovement;
    const float RotateSpeed = 540f;   // deg/sec toward the travel direction

    // isDead compatibility shim — reads from UnitParentClass.isAlive
    public bool isDead => !isAlive;

    // ── Hit flash ────────────────────────────────────────────────────
    private SpriteRenderer _sr;
    private Color          _baseColor = Color.white;
    private Coroutine      _flashCoroutine;

    // ── Drop config (stamped by UnitSpawner at spawn time) ───────────
    public DropConfig SpawnDropConfig { get; set; } = new DropConfig();
    public int        SpawnIndex      { get; set; }

    // ── Aura slow ─────────────────────────────────────────────────────
    private int _auraSlowCount;
    public void AddAuraSlow()    { _auraSlowCount++; }
    public void RemoveAuraSlow() { _auraSlowCount = Mathf.Max(0, _auraSlowCount - 1); }

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

        HealthBar.Attach(gameObject);
    }

    public string displayName;
    public string description;

    public void ApplyDefinition(string id)
    {
        if (UnitDefinitionLibrary.Instance == null) return;
        if (!UnitDefinitionLibrary.Instance.TryGet(id, out var def)) return;

        definitionId      = def.id;
        displayName       = def.displayName;
        description       = def.description;
        lifeMax           = def.life;
        speedMax          = def.speed;
        physicalDefense   = def.physicalDefense;
        elementalDefense  = def.elementalDefense;
        arcanaDefense     = def.arcanaDefense;
        bounty            = def.bounty;
        deathBlow         = def.deathBlow;
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
                Die(killedByDamage: true);
        }
        else
        {
            // Let SpriteAnimator handle destruction if a death animation is playing
            if (GetComponent<SpriteAnimator>()?.IsPlayingDeath == true) return;
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

        float slowFrac  = Mathf.Clamp01(_auraSlowCount * ModifierSelection.GetFloat("AuraSlowEnemies"));
        float baseSpeed = (speedMax <= 0f) ? speedCurrent : Mathf.Max(0f, speedCurrent);
        float speed     = baseSpeed * (1f - slowFrac);
        _follower.SetSpeed(speed);

        // Flip sprite to face movement direction
        var dir = _follower.CurrentDirection;
        if (_sr != null && Mathf.Abs(dir.x) > 0.01f)
            _sr.flipX = dir.x > 0f;

        // Optional: rotate toward the movement direction (rotateToMovement flag).
        // The flip above still decides mirroring; rotation is applied on top.
        if (rotateToMovement && dir.sqrMagnitude > 0.0001f)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // Art faces left when unflipped, so the unflipped case is offset 180°
            float target = _sr != null && _sr.flipX ? ang : ang - 180f;
            float z = Mathf.MoveTowardsAngle(transform.eulerAngles.z, target, RotateSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, z);
        }

        bool reachedEnd = _follower.Tick();

        if (reachedEnd)
        {
            if (WaveManager.ForgiveNextLeak)
            {
                WaveManager.ForgiveNextLeak = false;
            }
            else
            {
                LogicManager logic = LogicManager.Instance
                    ?? GameObject.FindGameObjectWithTag("Logic")?.GetComponent<LogicManager>();
                if (logic != null)
                {
                    int dmg = Mathf.Max(1, deathBlow - (int)ModifierSelection.GetFloat("LeakDamageReduction"));
                    logic.UpdateLives(dmg * -1);
                }
            }
            Die();
        }
    }

    // ── Click ─────────────────────────────────────────────────────────

    void OnMouseDown()
    {
        if (isAlive)
        {
            AudioManager.PlayEvent("select");
            GameHUD.Instance?.SelectEnemy(this);
        }
    }

    // ── Death ─────────────────────────────────────────────────────────

    /// <summary>Fired synchronously inside Die(), before any destruction —
    /// death-reaction components (splitting, etc.) must use this, not poll
    /// isAlive in Update: a no-death-anim unit is destroyed the same frame.</summary>
    public event System.Action OnDied;

    public void Die(bool killedByDamage = false)
    {
        isAlive = false;
        if (myCollider != null)
            myCollider.enabled = false;

        OnDied?.Invoke();

        if (killedByDamage)
        {
            if (UnitDefinitionLibrary.Instance != null &&
                UnitDefinitionLibrary.Instance.TryGet(definitionId, out var def) &&
                !string.IsNullOrEmpty(def.deathSoundId))
                AudioManager.Play(def.deathSoundId);
            else
                AudioManager.PlayEvent("enemy_death");
        }

        if (killedByDamage)
            ObjectiveTracker.NotifyKill(definitionId);

        GetComponent<BehaviorHandler>()?.TriggerDeathEffects(gameObject);

        var hb = GetComponentInChildren<HealthBar>();
        if (hb != null) hb.gameObject.SetActive(false);

        var anim = GetComponent<SpriteAnimator>();
        if (anim != null)
            anim.PlayDeath();
        else
            Destroy(gameObject);
    }
}
