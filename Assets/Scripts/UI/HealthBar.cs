using UnityEngine;

/// <summary>
/// Small segmented world-space health bar, sits tight above the unit sprite.
/// Bar width tracks the unit's on-screen sprite size (with sane min/max) and
/// renders at constant world proportions regardless of the unit's transform
/// scale. Units with a shield pool (hasShields) get a thin shield bar
/// bordering the top edge.
/// </summary>
public class HealthBar : MonoBehaviour
{
    private UnitParentClass  _unit;
    private SpriteRenderer[] _segs;
    private int              _segCount;
    private SpriteRenderer   _shieldBar;
    private SpriteRenderer   _shieldTrack;
    private Vector3          _worldOffset;
    private float            _barW = 0.42f;
    private float            _alpha = ALPHA_HEALTHY;

    private const float SEG_H    = 0.055f;
    private const float SEG_GAP  = 0.012f;
    private const float Y_OFF    = 0.26f;
    private const float SHIELD_H = SEG_H;   // same height as HP row so it reads at game zoom
    private const float SHIELD_Y = SEG_H * 0.5f + SEG_GAP + SHIELD_H * 0.5f;

    // Untouched units show a ghosted bar so the artwork isn't buried under
    // solid green; any damage fades the bar up to full visibility.
    // Set ALPHA_HEALTHY to 0 to hide full-HP bars entirely.
    private const float ALPHA_HEALTHY = 0f;
    private const float ALPHA_DAMAGED = 1f;
    private const float FADE_SPEED    = 5f;   // alpha units per second

    private static readonly Color C_Empty  = new Color(0.12f, 0.12f, 0.12f, 0.75f);
    private static readonly Color C_Shield = new Color(0.25f, 0.85f, 1f, 1f);

    public static HealthBar Attach(GameObject target)
    {
        var go = new GameObject("HealthBar");
        go.transform.SetParent(target.transform, false);

        // The bar is a child, so counter the unit's scale — geometry below is
        // in true world units whether the unit is a 2x boss or a 0.5x mini.
        float s = Mathf.Max(0.0001f, Mathf.Abs(target.transform.lossyScale.x));
        go.transform.localScale = new Vector3(1f / s, 1f / s, 1f);

        var hb   = go.AddComponent<HealthBar>();
        hb._unit = target.GetComponent<UnitParentClass>();

        // Size and position from the sprite's actual world footprint
        float topWorld = Y_OFF;
        var sr = target.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            topWorld  = sr.sprite.bounds.extents.y * s + 0.08f;
            hb._barW  = Mathf.Clamp(sr.sprite.bounds.size.x * s * 0.9f, 0.35f, 1.2f);
        }

        hb._worldOffset = new Vector3(0f, topWorld, 0f);
        go.transform.position = target.transform.position + hb._worldOffset;
        return hb;
    }

    void Start()
    {
        if (_unit == null) return;

        // Segment count scales with unit toughness, capped at 10
        _segCount = Mathf.Clamp(Mathf.CeilToInt(_unit.lifeMax / 25f), 3, 10);

        float segW = (_barW - SEG_GAP * (_segCount - 1)) / _segCount;
        float startX = -_barW * 0.5f;

        _segs = new SpriteRenderer[_segCount];
        for (int i = 0; i < _segCount; i++)
        {
            var go  = new GameObject($"Seg{i}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(startX + (segW + SEG_GAP) * i + segW * 0.5f, 0f, -0.01f);
            go.transform.localScale    = new Vector3(segW, SEG_H, 1f);

            var sr              = go.AddComponent<SpriteRenderer>();
            sr.sprite           = GetPixel();
            sr.color            = Color.green;
            sr.sortingLayerName = "Units";
            sr.sortingOrder     = 30;
            _segs[i] = sr;
        }

        // Shield bar — bordering the top of the HP segments, with a dark track
        // behind the fill so current vs max shield stays readable.
        if (_unit.hasShields && _unit.shieldMax > 0f)
        {
            var track = new GameObject("ShieldTrack");
            track.transform.SetParent(transform, false);
            track.transform.localPosition = new Vector3(0f, SHIELD_Y, -0.01f);
            track.transform.localScale    = new Vector3(_barW, SHIELD_H, 1f);

            _shieldTrack                  = track.AddComponent<SpriteRenderer>();
            _shieldTrack.sprite           = GetPixel();
            _shieldTrack.color            = C_Empty;
            _shieldTrack.sortingLayerName = "Units";
            _shieldTrack.sortingOrder     = 30;

            var go = new GameObject("ShieldBar");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, SHIELD_Y, -0.02f);
            go.transform.localScale    = new Vector3(_barW, SHIELD_H, 1f);

            _shieldBar                  = go.AddComponent<SpriteRenderer>();
            _shieldBar.sprite           = GetPixel();
            _shieldBar.color            = C_Shield;
            _shieldBar.sortingLayerName = "Units";
            _shieldBar.sortingOrder     = 31;
        }
    }

    void LateUpdate()
    {
        if (_unit == null || _segs == null) return;

        // Stay upright and pinned above the unit even when it rotates to its movement direction
        transform.rotation = Quaternion.identity;
        transform.position = _unit.transform.position + _worldOffset;

        float pct   = _unit.lifeMax > 0f ? Mathf.Clamp01(_unit.lifeCurrent / _unit.lifeMax) : 0f;
        float filled = pct * _segCount;

        // Fade: ghosted while untouched, fully visible once anything is damaged
        bool pristine = pct >= 0.999f &&
                        (!_unit.hasShields || _unit.shieldMax <= 0f ||
                         _unit.shieldCurrent >= _unit.shieldMax - 0.001f);
        _alpha = Mathf.MoveTowards(_alpha, pristine ? ALPHA_HEALTHY : ALPHA_DAMAGED,
                                   Time.deltaTime * FADE_SPEED);

        Color fillColor = pct > 0.5f
            ? Color.Lerp(Color.yellow, Color.green,  (pct - 0.5f) * 2f)
            : Color.Lerp(Color.red,    Color.yellow, pct * 2f);

        for (int i = 0; i < _segCount; i++)
        {
            Color c = i < Mathf.CeilToInt(filled) ? fillColor : C_Empty;
            c.a *= _alpha;
            _segs[i].color = c;
        }

        // Shield bar drains left-to-right; the dark track stays as the outline
        if (_shieldBar != null)
        {
            float spct = _unit.shieldMax > 0f ? Mathf.Clamp01(_unit.shieldCurrent / _unit.shieldMax) : 0f;
            _shieldBar.enabled = spct > 0f;
            if (_shieldTrack != null)
            {
                Color tc = C_Empty;
                tc.a *= _alpha;
                _shieldTrack.color = tc;
            }
            if (spct > 0f)
            {
                Color sc = C_Shield;
                sc.a *= _alpha;
                _shieldBar.color = sc;

                float w = _barW * spct;
                _shieldBar.transform.localScale    = new Vector3(w, SHIELD_H, 1f);
                _shieldBar.transform.localPosition = new Vector3(-_barW * 0.5f + w * 0.5f, SHIELD_Y, -0.02f);
            }
        }
    }

    static Sprite _pixel;
    static Sprite GetPixel()
    {
        if (_pixel != null) return _pixel;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        return _pixel;
    }
}
