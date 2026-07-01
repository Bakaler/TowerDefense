using UnityEngine;

/// <summary>
/// Fires N pellets in a cone toward the target. Each pellet moves in a straight line
/// and deals damage on hit. Pellets that miss expire after a short lifetime.
/// </summary>
[CreateAssetMenu(fileName = "NewEffect_Launch_Shotgun", menuName = "Effect/Launch Shotgun")]
public class Effect_Launch_Shotgun : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("launch_shotgun", typeof(Effect_Launch_Shotgun));

    public string impactEffectId  = "";
    public int    pelletCount     = 5;
    public float  spreadAngle     = 28f;
    public float  missileSpeed    = 22f;
    public float  missileScale    = 0.28f;
    public float  missileLifetime = 0.38f;
    public string missileSpriteSheet = "";
    public int    missileSpriteIndex = -1;
    public string missileSpritePath  = "";
    public Color  missileColor    = Color.white;

    private Effect _impactEffect;

    private static Sprite[] _cachedSheet;
    private static string   _cachedSheetPath;
    private static Sprite   _cachedSingleSprite;
    private static string   _cachedSinglePath;
    private static Sprite   _fallbackSprite;

    public override void ApplyData(string dataJson, EffectLibrary library)
    {
        if (!string.IsNullOrEmpty(dataJson))
            JsonUtility.FromJsonOverwrite(dataJson, this);

        if (!string.IsNullOrEmpty(impactEffectId))
            _impactEffect = library.GetEffect(impactEffectId);

        if (_impactEffect == null)
            Debug.LogWarning($"[Effect_Launch_Shotgun] Could not resolve impactEffectId '{impactEffectId}'.");
    }

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        if (_impactEffect == null) { Debug.LogWarning("[Effect_Launch_Shotgun] impactEffect is null."); return; }
        if (context.Target == null) { Debug.LogWarning("[Effect_Launch_Shotgun] context.Target is null."); return; }

        Transform spawnTransform = context.CasterTransform ?? context.Caster?.transform;
        if (spawnTransform == null) { Debug.LogWarning("[Effect_Launch_Shotgun] No spawn origin."); return; }

        Vector2 toTarget = (context.Target.transform.position - spawnTransform.position).normalized;

        int   count = Mathf.Max(1, pelletCount + (int)ModifierSelection.GetFloat("BonusShotgunBullets"));
        float half  = spreadAngle * 0.5f;

        for (int i = 0; i < count; i++)
        {
            // Pure random angle — weighted toward center with two samples averaged
            float a = Random.Range(-half, half);
            float b = Random.Range(-half, half);
            float offset = (a + b) * 0.5f;

            float speed    = missileSpeed    * Random.Range(0.6f, 1.4f);
            float lifetime = missileLifetime * Random.Range(0.5f, 1.5f);
            float scale    = missileScale    * Random.Range(0.7f, 1.4f);

            SpawnPellet(spawnTransform.position, Rotate(toTarget, offset), speed, lifetime, scale, context);
        }
    }

    void SpawnPellet(Vector3 origin, Vector2 dir, float speed, float lifetime, float scale, EffectContext context)
    {
        var go = new GameObject("ShotgunPellet");
        go.transform.position   = origin;
        go.transform.localScale = Vector3.one * scale;

        var rb          = go.AddComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var col       = go.AddComponent<CircleCollider2D>();
        col.radius    = 0.15f;
        col.isTrigger = true;

        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sprite           = LoadSprite(context);
        sr.color            = missileColor;
        sr.sortingLayerName = "Units";
        sr.sortingOrder     = 10;

        var pellet             = go.AddComponent<ShotgunPellet>();
        pellet.direction       = dir;
        pellet.moveSpeed       = speed;
        pellet.lifetime        = lifetime;
        pellet.impactEffect    = _impactEffect;
        pellet.originAbility   = context.OriginAbility;
        pellet.caster          = context.Caster;
        pellet.casterTransform = context.CasterTransform ?? context.Caster?.transform;
        pellet.originTower     = context.OriginTower;
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    Sprite LoadSprite(EffectContext context)
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
            if (_cachedSinglePath != missileSpritePath)
            {
                _cachedSingleSprite = Resources.Load<Sprite>(missileSpritePath);
                _cachedSinglePath   = missileSpritePath;
            }
            if (_cachedSingleSprite != null) return _cachedSingleSprite;
        }

        if (!string.IsNullOrEmpty(missileSpriteSheet) && missileSpriteIndex >= 0)
        {
            if (_cachedSheetPath != missileSpriteSheet)
            {
                _cachedSheet     = Resources.LoadAll<Sprite>(missileSpriteSheet);
                _cachedSheetPath = missileSpriteSheet;
            }
            if (_cachedSheet != null && missileSpriteIndex < _cachedSheet.Length)
                return _cachedSheet[missileSpriteIndex];
        }

        if (_fallbackSprite != null) return _fallbackSprite;
        const int sz = 6;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        float c = sz / 2f, r = sz / 2f - 0.5f;
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float dx = x - c + 0.5f, dy = y - c + 0.5f;
                tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz);
        return _fallbackSprite;
    }
}
