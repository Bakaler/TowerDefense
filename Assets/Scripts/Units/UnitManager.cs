using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitManager : UnitParentClass
{
    public Collider2D myCollider;

    [Tooltip("Must match an id in Resources/Definitions/units.json")]
    public string definitionId;

    public int deathBlow = 1;

    /// <summary>Rotate toward the movement direction (set from UnitDefinition.rotateToMovement).</summary>
    public bool rotateToMovement;
    /// <summary>Extra degrees added to the movement rotation — 180 flips art authored the other way.</summary>
    public float spriteAngleOffset;
    const float RotateSpeed = 540f;   // deg/sec toward the travel direction
    private bool _hasFacing;          // false until the first facing snap after spawn

    // ── Targeting metadata (stamped from UnitDefinition by the factory) ──
    /// <summary>Definition tags ("high_prio", "boss", …) read by tower targeting modes.</summary>
    public string[] tags = System.Array.Empty<string>();
    /// <summary>True when a starting behavior can render this unit invisible (cloakers).</summary>
    public bool canGoInvisible;

    public bool HasTag(string tag)
    {
        if (tags == null) return false;
        for (int i = 0; i < tags.Length; i++)
            if (tags[i] == tag) return true;
        return false;
    }

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

    // ── Speed pipeline ────────────────────────────────────────────────
    // speedCurrent is derived — never write it directly. Behaviors own one
    // multiplier (slows/hastes), external overrides (cast freezes, cleanse
    // bursts) own the other, so neither silently stomps the other's changes.
    private float _behaviorSpeedMult = 1f;
    private float _externalSpeedMult = 1f;

    /// <summary>Set by BehaviorHandler.Recalculate — product of active behavior speed multipliers.</summary>
    public void SetBehaviorSpeedMult(float mult) { _behaviorSpeedMult = mult; RefreshSpeed(); }
    /// <summary>Set by non-behavior overrides (cast pause, cleanse burst). 1 = none.</summary>
    public void SetExternalSpeedMult(float mult) { _externalSpeedMult = mult; RefreshSpeed(); }
    /// <summary>Recomputes speedCurrent — call after changing speedMax.</summary>
    public void RefreshSpeed() => speedCurrent = Mathf.Max(0f, speedMax * _behaviorSpeedMult * _externalSpeedMult);

    // ── Cached components ─────────────────────────────────────────────
    private BehaviorHandler _behaviors;
    /// <summary>Cached BehaviorHandler — added lazily by effects, never removed once added.</summary>
    public BehaviorHandler Behaviors => _behaviors != null ? _behaviors : (_behaviors = GetComponent<BehaviorHandler>());

    // ── Route ─────────────────────────────────────────────────────────
    private RouteFollower _follower;
    /// <summary>Cached RouteFollower (may be null until a route is assigned).</summary>
    public RouteFollower Follower => _follower;

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start()
    {
        if (!string.IsNullOrEmpty(definitionId) && lifeMax <= 0)
            ApplyDefinition(definitionId);

        if (lifeMax <= 0)  lifeMax  = 100f;
        if (speedMax <= 0) speedMax = 3f;

        isAlive      = true;
        lifeCurrent  = lifeMax;
        RefreshSpeed();
        decayTimer   = 0.4f;   // seconds a corpse lingers when no death anim plays

        _follower  = GetComponent<RouteFollower>();
        _sr        = GetComponent<SpriteRenderer>();
        if (_sr != null) _baseColor = _sr.color;

        HealthBar.Attach(gameObject);
    }

    public void ApplyDefinition(string id)
    {
        if (UnitDefinitionLibrary.Instance == null) return;
        if (!UnitDefinitionLibrary.Instance.TryGet(id, out var def)) return;

        definitionId      = def.id;
        lifeMax           = def.life;
        speedMax          = def.speed;
        physicalDefense   = def.physicalDefense;
        elementalDefense  = def.elementalDefense;
        arcanaDefense     = def.arcanaDefense;
        deathBlow         = def.deathBlow;
        tags              = def.tags ?? System.Array.Empty<string>();
        canGoInvisible    = UnitFactory.CanGoInvisible(def);
    }

    // ── Damage override (triggers hit flash) ─────────────────────────

    public override void TakeDamage(float damageAmount, float shieldBonus, float minimum, float maximum, DamageType type)
    {
        // Vulnerability behaviors amplify all incoming damage
        var handler = Behaviors;
        if (handler != null && handler.DamageTakenMult != 1f)
            damageAmount *= handler.DamageTakenMult;

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
        // Restore any behavior tint (e.g. invisibility transparency) the flash overwrote
        Behaviors?.Refresh();
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

            if (lifeCurrent <= 0)
                Die(killedByDamage: true);
        }
        else
        {
            // Let SpriteAnimator handle destruction if a death animation is playing
            if (GetComponent<SpriteAnimator>()?.IsPlayingDeath == true) return;
            if (decayTimer > 0)
                decayTimer -= Time.deltaTime;
            else
                Destroy(gameObject);
        }
    }

    /// <summary>Map-wide slow fraction owned by the (unique) global Aura Tower's slow research.</summary>
    public static float GlobalAuraSlow = 0f;

    void Move()
    {
        if (_follower == null || !_follower.HasRoute)
            return;

        float slowFrac  = Mathf.Clamp01(_auraSlowCount * ModifierSelection.GetFloat("AuraSlowEnemies") + GlobalAuraSlow);
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
            // Art faces left when unflipped, so the unflipped case is offset 180°;
            // spriteAngleOffset (per unit definition) corrects art authored differently
            float target = (_sr != null && _sr.flipX ? ang : ang - 180f) + spriteAngleOffset;
            // First frame after spawn: snap straight to the travel facing so units
            // never appear pointing the wrong way and visibly turning around.
            float z = _hasFacing
                ? Mathf.MoveTowardsAngle(transform.eulerAngles.z, target, RotateSpeed * Time.deltaTime)
                : target;
            _hasFacing = true;
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
        if (!isAlive) return;
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        if (TowerPlacer.Instance != null && TowerPlacer.Instance.IsPlacing) return;

        // A click that grabs gold/orbs must not also select the enemy under it
        if (Camera.main != null &&
            CollectClickGuard.IsOverCollectible(Camera.main.ScreenToWorldPoint(Input.mousePosition)))
            return;

        AudioManager.PlayEvent("select");
        GameHUD.Instance?.SelectEnemy(this);
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

        Behaviors?.TriggerDeathEffects(gameObject);

        var hb = GetComponentInChildren<HealthBar>();
        if (hb != null) hb.gameObject.SetActive(false);

        var anim = GetComponent<SpriteAnimator>();
        if (anim != null)
            anim.PlayDeath();
        else
            Destroy(gameObject);
    }
}
