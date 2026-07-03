using UnityEngine;

/// <summary>
/// Small segmented world-space health bar, sits tight above the unit sprite.
/// Units with a shield pool (hasShields) get a thin shield bar bordering the top edge.
/// </summary>
public class HealthBar : MonoBehaviour
{
    private UnitParentClass  _unit;
    private SpriteRenderer[] _segs;
    private int              _segCount;
    private SpriteRenderer   _shieldBar;

    private const float SEG_H    = 0.022f;
    private const float SEG_GAP  = 0.008f;
    private const float Y_OFF    = 0.22f;
    private const float TOTAL_W  = 0.28f;
    private const float SHIELD_H = SEG_H;   // same height as HP row so it reads at game zoom
    private const float SHIELD_Y = SEG_H * 0.5f + SEG_GAP + SHIELD_H * 0.5f;

    private static readonly Color C_Empty  = new Color(0.12f, 0.12f, 0.12f, 0.75f);
    private static readonly Color C_Shield = new Color(0.25f, 0.85f, 1f, 1f);

    public static HealthBar Attach(GameObject target)
    {
        var go   = new GameObject("HealthBar");
        go.transform.SetParent(target.transform, false);
        go.transform.localPosition = new Vector3(0f, Y_OFF, 0f);

        var hb   = go.AddComponent<HealthBar>();
        hb._unit = target.GetComponent<UnitParentClass>();
        return hb;
    }

    void Start()
    {
        if (_unit == null) return;

        // Segment count scales with unit toughness, capped at 10
        _segCount = Mathf.Clamp(Mathf.CeilToInt(_unit.lifeMax / 25f), 3, 10);

        float segW = (TOTAL_W - SEG_GAP * (_segCount - 1)) / _segCount;
        float startX = -TOTAL_W * 0.5f;

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
            track.transform.localScale    = new Vector3(TOTAL_W, SHIELD_H, 1f);

            var tsr              = track.AddComponent<SpriteRenderer>();
            tsr.sprite           = GetPixel();
            tsr.color            = C_Empty;
            tsr.sortingLayerName = "Units";
            tsr.sortingOrder     = 30;

            var go = new GameObject("ShieldBar");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, SHIELD_Y, -0.02f);
            go.transform.localScale    = new Vector3(TOTAL_W, SHIELD_H, 1f);

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

        float pct   = _unit.lifeMax > 0f ? Mathf.Clamp01(_unit.lifeCurrent / _unit.lifeMax) : 0f;
        float filled = pct * _segCount;

        Color fillColor = pct > 0.5f
            ? Color.Lerp(Color.yellow, Color.green,  (pct - 0.5f) * 2f)
            : Color.Lerp(Color.red,    Color.yellow, pct * 2f);

        for (int i = 0; i < _segCount; i++)
            _segs[i].color = i < Mathf.CeilToInt(filled) ? fillColor : C_Empty;

        // Shield bar drains left-to-right; the dark track stays as the outline
        if (_shieldBar != null)
        {
            float spct = _unit.shieldMax > 0f ? Mathf.Clamp01(_unit.shieldCurrent / _unit.shieldMax) : 0f;
            _shieldBar.enabled = spct > 0f;
            if (spct > 0f)
            {
                float w = TOTAL_W * spct;
                _shieldBar.transform.localScale    = new Vector3(w, SHIELD_H, 1f);
                _shieldBar.transform.localPosition = new Vector3(-TOTAL_W * 0.5f + w * 0.5f, SHIELD_Y, -0.02f);
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
