using UnityEngine;

/// <summary>
/// Spawns a short-lived text label in world space that floats upward and fades out.
/// </summary>
public class FloatingText : MonoBehaviour
{
    private TextMesh _tm;
    private float    _timer;
    private Color    _startColor;

    private const float Duration = 1.2f;
    private const float RiseSpeed = 1.2f;

    public static void Spawn(string text, Vector3 worldPos, Color color)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = worldPos + Vector3.up * 0.2f;

        var ft      = go.AddComponent<FloatingText>();
        ft._tm      = go.AddComponent<TextMesh>();
        ft._tm.text = text;
        ft._tm.color = color;
        ft._startColor = color;
        ft._tm.fontSize       = 28;
        ft._tm.anchor         = TextAnchor.MiddleCenter;
        ft._tm.alignment      = TextAlignment.Center;
        ft._tm.characterSize  = 0.07f;
        ft._tm.fontStyle      = FontStyle.Bold;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sortingLayerName = "Units";
        mr.sortingOrder     = 35;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        transform.position += Vector3.up * RiseSpeed * Time.deltaTime;

        var c = _startColor;
        c.a = Mathf.Clamp01(1f - (_timer / Duration));
        _tm.color = c;

        if (_timer >= Duration) Destroy(gameObject);
    }
}
