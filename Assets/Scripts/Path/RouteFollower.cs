using UnityEngine;

/// <summary>
/// Moves a unit along a Route (list of Vector2 waypoints).
/// Attach to the unit GameObject alongside UnitManager.
/// UnitManager calls follower.Tick() instead of its own Move() once a route is assigned.
/// </summary>
public class RouteFollower : MonoBehaviour
{
    // ── State ─────────────────────────────────────────────────────────
    private Route  _route;
    private int    _index;
    private float  _speed;

    // ── Teleport fade ─────────────────────────────────────────────────
    enum TeleportPhase { None, FadeOut, Hold, FadeIn }
    const float FADE_TIME = 0.35f;
    private TeleportPhase  _tpPhase = TeleportPhase.None;
    private float          _tpTimer;
    private float          _tpDelay;
    private int            _tpArrivalIndex;
    private SpriteRenderer _sr;
    private Color          _tpBaseColor;
    private Collider2D[]   _tpColliders;

    /// <summary>True while the unit is mid-teleport (fading out or in).</summary>
    public bool IsTeleporting => _tpPhase != TeleportPhase.None;

    public bool  HasRoute     => _route != null && !_route.IsEmpty;
    public bool  IsFinished   => !HasRoute || _index >= _route.Waypoints.Count;
    public Route CurrentRoute => _route;

    public Vector2 CurrentDirection
    {
        get
        {
            if (!HasRoute || _index >= _route.Waypoints.Count) return Vector2.right;
            return (_route.Waypoints[_index] - (Vector2)transform.position).normalized;
        }
    }

    /// <summary>
    /// 0–1 progress along the full waypoint list. Useful for sorting enemies
    /// by how far along the path they are (most dangerous = closest to 1).
    /// </summary>
    public float Progress => HasRoute ? (float)_index / _route.Waypoints.Count : 0f;

    // ── API ───────────────────────────────────────────────────────────

    public void StartRoute(Route route, float speed, float progressOffset = 0f)
    {
        _route  = route;
        _speed  = speed;
        _index  = HasRoute ? Mathf.Clamp(Mathf.RoundToInt(progressOffset * _route.Waypoints.Count), 0, _route.Waypoints.Count - 1) : 0;
    }

    public void SetSpeed(float speed) => _speed = speed;

    /// <summary>
    /// Call from UnitManager.Update (or wherever movement is driven).
    /// Returns true when the terminus waypoint has been reached.
    /// </summary>
    public bool Tick()
    {
        if (_tpPhase != TeleportPhase.None) return TickTeleport();
        if (IsFinished) return true;

        Vector2 pos    = transform.position;
        Vector2 target = _route.Waypoints[_index];
        Vector2 delta  = target - pos;

        if (delta.sqrMagnitude < 0.004f) // ~0.063 world units
        {
            // Teleporter waypoint — fade out here, reappear at the next waypoint
            if (_route.TeleportDepartures.TryGetValue(_index, out float tpDelay)
                && _index + 1 < _route.Waypoints.Count)
            {
                BeginTeleport(_index + 1, tpDelay);
                return false;
            }

            _index++;
            return IsFinished;
        }

        transform.position = Vector2.MoveTowards(pos, target, _speed * Time.deltaTime);
        return false;
    }

    // ── Teleport internals ────────────────────────────────────────────

    void BeginTeleport(int arrivalIndex, float delay)
    {
        _tpArrivalIndex = arrivalIndex;
        _tpDelay        = Mathf.Max(0f, delay);
        _tpPhase        = TeleportPhase.FadeOut;
        _tpTimer        = 0f;
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        _tpBaseColor = _sr != null ? _sr.color : Color.white;
    }

    bool TickTeleport()
    {
        _tpTimer += Time.deltaTime;

        if (_tpPhase == TeleportPhase.FadeOut)
        {
            float t = Mathf.Clamp01(_tpTimer / FADE_TIME);
            SetAlpha(1f - t);
            if (t >= 1f)
            {
                transform.position = _route.Waypoints[_tpArrivalIndex];
                _index   = _tpArrivalIndex + 1;
                _tpTimer = 0f;
                if (_tpDelay > 0f)
                {
                    // Vanished — untargetable until it reappears
                    SetCollidersEnabled(false);
                    _tpPhase = TeleportPhase.Hold;
                }
                else
                    _tpPhase = TeleportPhase.FadeIn;
            }
            return false;
        }

        if (_tpPhase == TeleportPhase.Hold)
        {
            if (_tpTimer >= _tpDelay)
            {
                SetCollidersEnabled(true);
                _tpPhase = TeleportPhase.FadeIn;
                _tpTimer = 0f;
            }
            return false;
        }

        // FadeIn
        float tin = Mathf.Clamp01(_tpTimer / FADE_TIME);
        SetAlpha(tin);
        if (tin >= 1f)
        {
            _tpPhase = TeleportPhase.None;
            if (_sr != null) _sr.color = _tpBaseColor;
            // Reapply behavior tints (e.g. invisibility transparency) the fade overwrote
            GetComponent<BehaviorHandler>()?.Refresh();
            return IsFinished;
        }
        return false;
    }

    void SetCollidersEnabled(bool enabled)
    {
        if (_tpColliders == null) _tpColliders = GetComponents<Collider2D>();
        foreach (var col in _tpColliders)
            if (col != null) col.enabled = enabled;
    }

    void SetAlpha(float factor)
    {
        if (_sr == null) return;
        var c = _tpBaseColor;
        c.a *= factor;
        _sr.color = c;
    }
}
