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
        if (IsFinished) return true;

        Vector2 pos    = transform.position;
        Vector2 target = _route.Waypoints[_index];
        Vector2 delta  = target - pos;

        if (delta.sqrMagnitude < 0.004f) // ~0.063 world units
        {
            _index++;
            return IsFinished;
        }

        transform.position = Vector2.MoveTowards(pos, target, _speed * Time.deltaTime);
        return false;
    }
}
