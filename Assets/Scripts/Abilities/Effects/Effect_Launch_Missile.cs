using UnityEngine;

/// <summary>
/// Spawns a homing ProjectileUnit that applies impactEffect on hit.
/// All config comes from effects.json via ApplyData — no prefab, no direct object refs.
/// </summary>
[CreateAssetMenu(fileName = "NewEffect_Launch_Missile", menuName = "Effect/Launch Missile")]
public class Effect_Launch_Missile : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("launch_missile", typeof(Effect_Launch_Missile));

    // ── Data fields (populated from effects.json via ApplyData) ──────
    /// <summary>ID of the Effect to apply on missile impact. Resolved from EffectLibrary.</summary>
    public string impactEffectId = "";

    public float  missileSpeed       = 8f;
    public float  missileScale       = 0.5f;
    public float  missileLifetime    = 7f;
    public string missileSpriteSheet = "";
    public int    missileSpriteIndex = -1;
    public string missileSpritePath  = "";
    public bool   drawLine           = false;
    public bool   faceDirection      = false;
    public bool   homing             = true;    // false = flies to last-known TargetPoint (mortar)
    public bool   arcFlight          = false;   // lob arc visual (mortar)
    public bool   piercing           = false;   // passes through ShieldBubble
    public Color  missileColor       = Color.white;

    // ── Resolved at runtime ───────────────────────────────────────────
    private Effect _impactEffect;

    // Cache loaded sprites so we don't call Resources.LoadAll every shot
    private static Sprite[] _cachedSheet;
    private static string   _cachedSheetPath;
    private static Sprite   _fallbackSprite;

    public override void ApplyData(string dataJson, EffectLibrary library)
    {
        if (!string.IsNullOrEmpty(dataJson))
            JsonUtility.FromJsonOverwrite(dataJson, this);

        if (!string.IsNullOrEmpty(impactEffectId))
            _impactEffect = library.GetEffect(impactEffectId);

        if (_impactEffect == null)
            Debug.LogWarning($"[Effect_Launch_Missile] Could not resolve impactEffectId '{impactEffectId}'.");
    }

    // ── Execute ───────────────────────────────────────────────────────

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        if (_impactEffect == null) { Debug.LogWarning("[Effect_Launch_Missile] impactEffect is null."); return; }
        if (homing && context.Target == null) { Debug.LogWarning("[Effect_Launch_Missile] homing missile needs a target."); return; }

        // Spawn origin: explicit caster transform → caster unit → target position (chain/bounce fallback)
        Transform spawnTransform = context.CasterTransform
            ?? context.Caster?.transform
            ?? context.Target?.transform;
        if (spawnTransform == null) { Debug.LogWarning("[Effect_Launch_Missile] No spawn origin."); return; }

        // ── Build projectile GO ───────────────────────────────────────
        var go = new GameObject("Missile");
        go.transform.position   = spawnTransform.position;
        go.transform.localScale = Vector3.one * missileScale;

        // Rigidbody — kinematic, gravity off
        var rb          = go.AddComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Trigger collider for hit detection
        var col       = go.AddComponent<CircleCollider2D>();
        col.radius    = 0.15f;
        col.isTrigger = true;

        // Sprite
        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sprite           = LoadMissileSprite(context);
        sr.color            = missileColor;
        sr.sortingLayerName = "Units";
        sr.sortingOrder     = 10;

        // ProjectileUnit — the actual movement + impact logic
        var proj                = go.AddComponent<ProjectileUnit>();
        proj.moveSpeed          = missileSpeed;
        proj.lifetime           = missileLifetime;
        proj.impactEffect       = _impactEffect;
        proj.originAbility      = context.OriginAbility;
        proj.caster             = context.Caster;
        proj.casterTransform    = context.CasterTransform ?? context.Caster?.transform;
        proj.target             = homing ? context.Target?.transform : null;
        proj.targetPoint        = context.TargetPoint;
        proj.homing             = homing;
        proj.drawImpactLine     = drawLine;
        proj.faceDirection      = faceDirection;
        proj.originTower        = context.OriginTower;
        proj.arcFlight          = arcFlight;
        proj.piercing           = piercing;
        if (!homing) proj.targetPoint = context.TargetPoint;
    }

    // ── Sprite loading ────────────────────────────────────────────────

    Sprite LoadMissileSprite(EffectContext context)
    {
        // Tiered art: {towerId}_missile_T{tier} counting down to T1
        if (context?.OriginTower != null)
        {
            var info = context.OriginTower.GetComponent<TowerInfo>();
            if (info != null)
            {
                var sp = TowerFactory.ResolveTieredSprite(info.definitionId + "_missile", info.Tier, null);
                if (sp != null) return sp;
            }
        }

        if (!string.IsNullOrEmpty(missileSpritePath))
        {
            var spr = Resources.Load<Sprite>(missileSpritePath);
            if (spr != null) return spr;
        }

        return GetFallbackSprite();
    }

    static Sprite GetFallbackSprite()
    {
        if (_fallbackSprite != null) return _fallbackSprite;
        const int size = 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = size / 2f, r = size / 2f - 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - c + 0.5f, dy = y - c + 0.5f;
                tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _fallbackSprite;
    }
}
