using UnityEngine;

/// <summary>
/// Small segmented world-space health bar, sits tight above the unit sprite.
/// </summary>
public class HealthBar : MonoBehaviour
{
    private UnitParentClass  _unit;
    private SpriteRenderer[] _segs;
    private int              _segCount;

    private const float SEG_H    = 0.022f;
    private const float SEG_GAP  = 0.008f;
    private const float Y_OFF    = 0.22f;
    private const float TOTAL_W  = 0.28f;

    private static readonly Color C_Empty = new Color(0.12f, 0.12f, 0.12f, 0.75f);

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
