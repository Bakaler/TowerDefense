using System.Collections;
using UnityEngine;

public class SpriteAnimator : MonoBehaviour
{
    private Sprite[]       _walkFrames;
    private Sprite[]       _deathFrames;
    private float          _walkFps;
    private float          _deathFps;
    private float          _timer;
    private int            _frame;
    private SpriteRenderer _sr;
    private bool           _playingDeath;
    public bool            IsPlayingDeath => _playingDeath;

    public void Setup(Sprite[] walkFrames, float walkFps, Sprite[] deathFrames = null, float deathFps = 8f)
    {
        _walkFrames  = walkFrames;
        _deathFrames = deathFrames;
        _walkFps     = walkFps > 0f ? walkFps : 8f;
        _deathFps    = deathFps > 0f ? deathFps : 8f;
        _sr          = GetComponent<SpriteRenderer>();
        _frame       = 0;
        _timer       = 0f;
        if (_walkFrames != null && _walkFrames.Length > 0 && _sr != null)
            _sr.sprite = _walkFrames[0];
    }

    public void OffsetTime(float seconds) { _timer = seconds % (1f / (_walkFps > 0f ? _walkFps : 8f)); }

    /// <summary>Starts the walk cycle at a random frame and timer phase so
    /// groups spawned together (splitter minis) don't animate in unison.</summary>
    public void RandomizeWalkPhase()
    {
        if (_walkFrames == null || _walkFrames.Length < 2) return;
        _frame = Random.Range(0, _walkFrames.Length);
        _timer = Random.Range(0f, 1f / _walkFps);
        if (_sr != null) _sr.sprite = _walkFrames[_frame];
    }

    public void PlayDeath()
    {
        if (_deathFrames == null || _deathFrames.Length == 0) { Destroy(gameObject); return; }
        _playingDeath = true;
        _frame        = 0;
        _timer        = 0f;
        _sr.sprite    = _deathFrames[0];
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        float interval = 1f / _deathFps;
        for (int i = 1; i < _deathFrames.Length; i++)
        {
            yield return new WaitForSeconds(interval);
            if (_sr != null) _sr.sprite = _deathFrames[i];
        }
        yield return new WaitForSeconds(interval);
        Destroy(gameObject);
    }

    void Update()
    {
        if (_playingDeath || _walkFrames == null || _walkFrames.Length < 2 || _sr == null) return;
        _timer += Time.deltaTime;
        if (_timer >= 1f / _walkFps)
        {
            _timer -= 1f / _walkFps;
            _frame  = (_frame + 1) % _walkFrames.Length;
            _sr.sprite = _walkFrames[_frame];
        }
    }
}
