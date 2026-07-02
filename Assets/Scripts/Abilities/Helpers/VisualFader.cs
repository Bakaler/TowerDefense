using UnityEngine;

/// <summary>
/// Fades out and destroys the attached GameObject over a given duration.
/// Optionally cycles through a sprite sheet at a given fps while fading.
/// </summary>
public class VisualFader : MonoBehaviour
{
    private float    _duration;
    private float    _elapsed;
    private SpriteRenderer _sr;

    private Sprite[] _frames;
    private float    _fps;
    private float    _frameTimer;
    private int      _frameIdx;

    public void Setup(float duration, Sprite[] frames = null, float fps = 0f)
    {
        _duration = Mathf.Max(0.01f, duration);
        _sr       = GetComponent<SpriteRenderer>();
        _frames   = frames;
        _fps      = fps;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        // Animate frames
        if (_frames != null && _frames.Length > 1 && _fps > 0f)
        {
            _frameTimer += Time.deltaTime;
            float frameDuration = 1f / _fps;
            while (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _frameIdx = (_frameIdx + 1) % _frames.Length;
            }
            if (_sr != null) _sr.sprite = _frames[_frameIdx];
        }

        // Fade alpha
        if (_sr != null)
        {
            var c = _sr.color;
            c.a       = 1f - Mathf.Clamp01(_elapsed / _duration);
            _sr.color = c;
        }

        if (_elapsed >= _duration)
            Destroy(gameObject);
    }
}
