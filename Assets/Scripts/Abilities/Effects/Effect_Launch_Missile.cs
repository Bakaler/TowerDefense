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
        if (_impactEffect == null)  { Debug.LogWarning("[Effect_Launch_Missile] impactEffect is null."); return; }
        if (context.Target == null) { Debug.LogWarning("[Effect_Launch_Missile] context.Target is null."); return; }

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
        sr.sprite           = LoadMissileSprite();
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
        proj.target             = context.Target.transform;
        proj.targetPoint        = context.TargetPoint;
        proj.homing             = true;
        proj.drawImpactLine     = drawLine;
        proj.faceDirection      = faceDirection;
        proj.originTower        = context.OriginTower;

        Debug.Log($"[Effect_Launch_Missile] Spawned missile → {context.Target.name}");
    }

    // ── Sprite loading ────────────────────────────────────────────────

    Sprite LoadMissileSprite()
    {
        if (!string.IsNullOrEmpty(missileSpriteSheet) && missileSpriteIndex >= 0)
        {
            if (_cachedSheetPath != missileSpriteSheet)
            {
                _cachedSheet     = Resources.LoadAll<Sprite>(missileSpriteSheet);
                _cachedSheetPath = missileSpriteSheet;
            }
            if (_cachedSheet != null && missileSpriteIndex < _cachedSheet.Length)
                return _cachedSheet[missileSpriteIndex];

            Debug.LogWarning($"[Effect_Launch_Missile] Sheet '{missileSpriteSheet}' index {missileSpriteIndex} not found.");
        }

        if (!string.IsNullOrEmpty(missileSpritePath))
        {
            var spr = Resources.Load<Sprite>(missileSpritePath);
            if (spr != null) return spr;
            Debug.LogWarning($"[Effect_Launch_Missile] Sprite not found at '{missileSpritePath}'.");
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
