using UnityEngine;

/// <summary>
/// Shows the shotgun arc when the tower is selected.
/// The turret auto-tracks enemies via Turret like any other tower.
/// </summary>
public class ShotgunOrienter : MonoBehaviour, IFactoryInitializable
{
    private bool         _selected   = false;
    private Transform    _turretPart;
    private LineRenderer _arcLine;
    private float        _arcDegrees = 60f;
    private float        _arcAlpha   = 0f;

    private const int ArcSegs = 24;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("shotgun_orienter", typeof(ShotgunOrienter));

    void Start()
    {
        var turret = GetComponent<Turret>();
        if (turret != null && turret.fireAbility != null)
            _arcDegrees = turret.fireAbility.fireArc;

        var t = GetComponent<Turret>();
        _turretPart = (t != null && t.RotatingPart != null) ? t.RotatingPart : transform;

        BuildArcLine();
        RefreshArc();
        SetArcAlpha(0f);
    }

    void Update()
    {
        if (_turretPart == null)
        {
            var t = GetComponent<Turret>();
            _turretPart = (t != null && t.RotatingPart != null) ? t.RotatingPart : transform;
        }

        if (_selected) RefreshArc();

        float target = _selected ? 0.75f : 0f;
        if (!Mathf.Approximately(_arcAlpha, target))
        {
            _arcAlpha = Mathf.MoveTowards(_arcAlpha, target, Time.deltaTime * 4f);
            ApplyArcAlpha();
        }
    }

    public void SetSelected(bool selected) => _selected = selected;

    void BuildArcLine()
    {
        var go = new GameObject("_ShotgunArc");
        go.transform.SetParent(transform);
        _arcLine = go.AddComponent<LineRenderer>();
        _arcLine.useWorldSpace    = true;
        _arcLine.loop             = false;
        _arcLine.positionCount    = ArcSegs + 3;
        _arcLine.startWidth       = 0.05f;
        _arcLine.endWidth         = 0.05f;
        _arcLine.numCapVertices   = 2;
        _arcLine.sortingLayerName = "Default";
        _arcLine.sortingOrder     = 8;
        _arcLine.sharedMaterial         = RuntimeMaterials.SpriteDefault;
    }

    void RefreshArc()
    {
        if (_arcLine == null) return;
        Vector3 origin   = transform.position;
        float   range    = GetRange();
        float   half     = _arcDegrees * 0.5f;
        Vector2 turretUp = _turretPart != null ? (Vector2)_turretPart.up : Vector2.up;
        float   baseAngle = Mathf.Atan2(turretUp.y, turretUp.x) * Mathf.Rad2Deg;

        _arcLine.positionCount = ArcSegs + 3;
        _arcLine.SetPosition(0, origin);
        for (int i = 0; i <= ArcSegs; i++)
        {
            float t   = (float)i / ArcSegs;
            float ang = ((baseAngle + half) - _arcDegrees * t) * Mathf.Deg2Rad;
            _arcLine.SetPosition(i + 1, origin + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * range);
        }
        _arcLine.SetPosition(ArcSegs + 2, origin);
    }

    void SetArcAlpha(float a) { _arcAlpha = a; ApplyArcAlpha(); }

    void ApplyArcAlpha()
    {
        if (_arcLine == null) return;
        var c = new Color(0.6f, 0.85f, 1f, _arcAlpha);
        _arcLine.startColor = c;
        _arcLine.endColor   = c;
    }

    float GetRange()
    {
        var col = GetComponent<CircleCollider2D>();
        return col != null ? col.radius : 3f;
    }

    [System.Serializable]
    class OrientData { public float arcDegrees = 60f; }

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<OrientData>(dataJson);
        if (d != null) _arcDegrees = d.arcDegrees;
    }
}
