using UnityEngine;

/// <summary>
/// Transient LineRenderer that draws the railgun beam and fades out.
/// Spawned by Effect_Railgun; self-destructs after fade completes.
/// </summary>
public class RailgunBeam : MonoBehaviour
{
    private LineRenderer _lr;
    private float        _fadeDuration;
    private float        _elapsed;
    private Color        _startColor;

    public void Setup(Vector2 from, Vector2 to, Color color, float width, float fadeDuration)
    {
        _fadeDuration = fadeDuration;
        _startColor   = color;

        _lr                  = gameObject.AddComponent<LineRenderer>();
        _lr.positionCount    = 2;
        _lr.useWorldSpace    = true;
        _lr.startWidth       = width * 2f;
        _lr.endWidth         = width * 0.5f;
        _lr.sortingLayerName = "Units";
        _lr.sortingOrder     = 30;
        _lr.material         = new Material(Shader.Find("Sprites/Default"));
        _lr.startColor       = color;
        _lr.endColor         = new Color(color.r, color.g, color.b, 0f);
        _lr.SetPosition(0, from);
        _lr.SetPosition(1, to);

        Destroy(gameObject, fadeDuration + 0.05f);
    }

    void Update()
    {
        if (_lr == null) return;
        _elapsed += Time.deltaTime;
        float t   = Mathf.Clamp01(_elapsed / _fadeDuration);
        float a   = Mathf.Lerp(1f, 0f, t);
        _lr.startColor = new Color(_startColor.r, _startColor.g, _startColor.b, a);
        _lr.endColor   = new Color(_startColor.r, _startColor.g, _startColor.b, 0f);
    }
}
