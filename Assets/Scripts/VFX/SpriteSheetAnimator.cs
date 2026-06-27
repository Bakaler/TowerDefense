using UnityEngine;

/// <summary>
/// Plays a horizontal sprite sheet (N frames of equal width, single row).
/// Set up and started by BehaviorHandler; also usable standalone.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteSheetAnimator : MonoBehaviour
{
    public string texturePath;
    public int    frameCount;
    public float  fps               = 12f;
    public float  scale             = 1f;
    public bool   loop              = false;
    public bool   destroyOnComplete = true;

    private Sprite[]       _frames;
    private SpriteRenderer _sr;
    private float          _timer;
    private int            _current = -1;
    private bool           _done;

    void Start()
    {
        _sr = GetComponent<SpriteRenderer>();

        var tex = Resources.Load<Texture2D>(texturePath);
        if (tex == null || frameCount <= 0)
        {
            if (destroyOnComplete) Destroy(gameObject);
            return;
        }

        int fw = tex.width / frameCount;
        int fh = tex.height;
        _frames = new Sprite[frameCount];
        for (int i = 0; i < frameCount; i++)
            _frames[i] = Sprite.Create(tex,
                new Rect(i * fw, 0, fw, fh),
                new Vector2(0.5f, 0.5f),
                fw);   // ppu = frame width → 1 unit wide

        if (scale != 1f)
            transform.localScale = Vector3.one * scale;

        ShowFrame(0);
    }

    void Update()
    {
        if (_done || _frames == null || fps <= 0f) return;

        _timer += Time.deltaTime;
        int frame = Mathf.FloorToInt(_timer * fps);

        if (frame >= frameCount)
        {
            if (loop)
            {
                _timer -= frameCount / fps;
                frame   = 0;
            }
            else
            {
                _done = true;
                if (destroyOnComplete) Destroy(gameObject);
                return;
            }
        }

        ShowFrame(frame);
    }

    void ShowFrame(int frame)
    {
        if (frame == _current || _sr == null) return;
        _current    = frame;
        _sr.sprite  = _frames[frame];
    }
}
