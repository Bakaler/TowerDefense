using UnityEngine;

[CreateAssetMenu(fileName = "NewEffect_Launch_Boomerang", menuName = "Effect/Launch Boomerang")]
public class Effect_Launch_Boomerang : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("launch_boomerang", typeof(Effect_Launch_Boomerang));

    public string impactEffectId      = "";
    public float  arcRadius           = 4f;
    public float  sweepSpeed          = 180f;
    public float  hitRadius           = 0.4f;
    public float  boomerangScale      = 0.6f;
    public string boomerangSpritePath = "";
    public string boomerangSpriteSheet = "";
    public int    boomerangSpriteIndex = -1;
    public Color  boomerangColor      = Color.white;
    public float  spinSpeed           = 0f;   // extra degrees/s spin on the sprite

    private Effect _impactEffect;

    private static Sprite[] _cachedSheet;
    private static string   _cachedSheetPath;

    public override void ApplyData(string dataJson, EffectLibrary library)
    {
        if (!string.IsNullOrEmpty(dataJson))
            JsonUtility.FromJsonOverwrite(dataJson, this);

        if (!string.IsNullOrEmpty(impactEffectId))
            _impactEffect = library.GetEffect(impactEffectId);

        if (_impactEffect == null)
            Debug.LogWarning($"[Effect_Launch_Boomerang] Could not resolve impactEffectId '{impactEffectId}'.");
    }

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        if (_impactEffect == null) return;

        Transform origin = context.CasterTransform ?? context.Caster?.transform;
        if (origin == null) return;

        Vector2 targetDir = context.Target != null
            ? ((Vector2)context.Target.transform.position - (Vector2)origin.position).normalized
            : Vector2.up;

        var go = new GameObject("Boomerang");
        go.transform.localScale = Vector3.one * boomerangScale;

        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sprite           = LoadSprite();
        sr.color            = boomerangColor;
        sr.sortingLayerName = "Units";
        sr.sortingOrder     = 15;

        var proj               = go.AddComponent<BoomerangProjectile>();
        proj.caster            = context.Caster;
        proj.casterTransform   = origin;
        proj.originAbility     = context.OriginAbility;
        proj.impactEffect      = _impactEffect;
        proj.originTower       = context.OriginTower;
        proj.arcRadius         = arcRadius;
        proj.sweepSpeed        = sweepSpeed;
        proj.hitRadius         = hitRadius;
        proj.spinSpeed         = spinSpeed;
        proj.Launch((Vector2)origin.position, targetDir);
    }

    Sprite LoadSprite()
    {
        // Single-sprite path takes priority
        if (!string.IsNullOrEmpty(boomerangSpritePath))
        {
            var s = Resources.Load<Sprite>(boomerangSpritePath);
            if (s != null) return s;
        }
        // Sheet fallback
        if (!string.IsNullOrEmpty(boomerangSpriteSheet) && boomerangSpriteIndex >= 0)
        {
            if (_cachedSheetPath != boomerangSpriteSheet)
            {
                _cachedSheet     = Resources.LoadAll<Sprite>(boomerangSpriteSheet);
                _cachedSheetPath = boomerangSpriteSheet;
            }
            if (_cachedSheet != null && boomerangSpriteIndex < _cachedSheet.Length)
                return _cachedSheet[boomerangSpriteIndex];
        }
        return MakeFallbackSprite();
    }

    static Sprite MakeFallbackSprite()
    {
        const int size = 12;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        // Draw a simple arc/crescent shape
        float cx = size / 2f, cy = size / 2f;
        float outerR = size / 2f - 0.5f, innerR = size / 2f - 3f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                bool inOuter = dist <= outerR;
                bool inInner = dist <= innerR;
                // crescent: in outer ring, but only on one side
                bool onArcSide = dy >= -1f;
                tex.SetPixel(x, y, (inOuter && !inInner && onArcSide) ? Color.white : Color.clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
