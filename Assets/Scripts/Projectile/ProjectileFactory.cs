using UnityEngine;

/// <summary>Per-shot launch context passed to ProjectileFactory.Spawn.</summary>
public struct ProjectileSpawnArgs
{
    public Vector3         origin;
    public Vector2         direction;     // straight / orbit
    public UnitParentClass targetUnit;    // homing
    public Vector3         targetPoint;   // arc
    public Effect          impactEffect;
    public UnitParentClass caster;
    public Transform       casterTransform;
    public Ability_Effect  originAbility;
    public GameObject      originTower;
    /// <summary>>0 replaces the impact effect's base damage.</summary>
    public float damageOverride;
    /// <summary>Per-shot multipliers for jitter/overrides. 0 means "unset" and is treated as 1.</summary>
    public float speedMultiplier;
    public float lifetimeMultiplier;
    public float scaleMultiplier;
}

/// <summary>
/// Builds projectile GameObjects entirely in code from ProjectileDefinitions
/// (projectiles.json) — no prefabs. Mirrors the Tower/Unit factory pattern.
/// </summary>
public static class ProjectileFactory
{
    public static Projectile Spawn(string projectileId, ProjectileSpawnArgs args)
    {
        var def = ProjectileLibrary.Get(projectileId);
        if (def == null)
        {
            Debug.LogWarning($"[ProjectileFactory] No projectile definition for id '{projectileId}'.");
            return null;
        }
        return Spawn(def, args);
    }

    public static Projectile Spawn(ProjectileDefinition def, ProjectileSpawnArgs args)
    {
        if (def == null) return null;

        var movement = ParseMovement(def.movement);

        var go = new GameObject(string.IsNullOrEmpty(def.displayName) ? (def.id ?? "Projectile") : def.displayName);
        go.transform.position   = args.origin;
        go.transform.localScale = Vector3.one * (def.scale * Mult(args.scaleMultiplier));

        // Straight/homing/arc use trigger collisions; orbit scans overlaps itself.
        if (movement != Projectile.MovementMode.Orbit)
        {
            var rb          = go.AddComponent<Rigidbody2D>();
            rb.bodyType     = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            var col       = go.AddComponent<CircleCollider2D>();
            col.radius    = def.hitRadius > 0f ? def.hitRadius : 0.15f;
            col.isTrigger = true;
        }

        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sprite           = ResolveSprite(def, args.originTower);
        sr.color            = def.color;
        sr.sortingLayerName = string.IsNullOrEmpty(def.sortingLayer) ? "Units" : def.sortingLayer;
        sr.sortingOrder     = def.sortingOrder;

        // Looping flight animation when the definition asks for it and frames exist
        if (def.animFps > 0f)
        {
            var frames = ResolveAnimFrames(def, args.originTower);
            if (frames.Length > 1)
            {
                sr.sprite = frames[0];
                go.AddComponent<SpriteAnimator>().Setup(frames, def.animFps);
            }
        }

        var proj             = go.AddComponent<Projectile>();
        proj.def             = def;
        proj.movement        = movement;
        proj.speed           = def.speed    * Mult(args.speedMultiplier);
        proj.lifetime        = def.lifetime * Mult(args.lifetimeMultiplier);
        proj.impactEffect    = args.impactEffect;
        proj.caster          = args.caster;
        proj.casterTransform = args.casterTransform ?? args.caster?.transform;
        proj.originAbility   = args.originAbility;
        proj.originTower     = args.originTower;
        proj.targetUnit      = args.targetUnit;
        proj.targetPoint     = args.targetPoint;
        proj.direction       = args.direction;
        proj.damageOverride  = args.damageOverride;
        proj.maxHits         = ResolveMaxHits(def, args.originTower);
        proj.Launch();

        return proj;
    }

    public static Projectile.MovementMode ParseMovement(string movement) => movement switch
    {
        "homing" => Projectile.MovementMode.Homing,
        "arc"    => Projectile.MovementMode.Arc,
        "orbit"  => Projectile.MovementMode.Orbit,
        _        => Projectile.MovementMode.Straight,
    };

    static float Mult(float m) => m > 0f ? m : 1f;

    /// <summary>Hit budget: def.maxHits (0 = unlimited) plus maxHitsPerTier per tower upgrade.</summary>
    static int ResolveMaxHits(ProjectileDefinition def, GameObject originTower)
    {
        if (def.maxHits <= 0) return 0;
        int hits = def.maxHits;
        if (def.maxHitsPerTier > 0 && originTower != null)
        {
            var info = originTower.GetComponent<TowerInfo>();
            if (info != null) hits += def.maxHitsPerTier * (info.Tier - 1);
        }
        return hits;
    }

    /// <summary>
    /// Sprite resolution order: tiered tower art ({towerId}_missile_T{tier}) →
    /// definition spritePath/spriteSheet → procedural circle fallback.
    /// </summary>
    static Sprite ResolveSprite(ProjectileDefinition def, GameObject originTower)
    {
        if (originTower != null)
        {
            var info = originTower.GetComponent<TowerInfo>();
            if (info != null)
            {
                var tiered = TowerFactory.ResolveTieredSprite(info.definitionId + "_missile", info.Tier, null);
                if (tiered != null) return tiered;
            }
        }

        var sprite = RuntimeSprites.Resolve(def.spritePath, def.spriteSheet, def.spriteIndex);
        return sprite != null ? sprite : RuntimeSprites.Circle();
    }

    /// <summary>
    /// Animation frames: tiered tower art ({towerId}_missile_T{tier} as a sliced sheet)
    /// wins, then the definition's spritePath as a sheet. Empty when neither has frames.
    /// </summary>
    static Sprite[] ResolveAnimFrames(ProjectileDefinition def, GameObject originTower)
    {
        if (originTower != null)
        {
            var info = originTower.GetComponent<TowerInfo>();
            if (info != null)
            {
                string tieredPath = TowerFactory.ResolveTieredPath(info.definitionId + "_missile", info.Tier, null);
                if (tieredPath != null)
                {
                    var tiered = RuntimeSprites.LoadSheet(tieredPath);
                    if (tiered.Length > 1) return tiered;
                }
            }
        }
        return RuntimeSprites.LoadSheet(def.spritePath);
    }
}
