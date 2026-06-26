using System.Collections;
using UnityEngine;

/// <summary>
/// Fires a hitscan shot at the enemy furthest along the path that is currently
/// inside the SniperZone rectangle. Stops shooting while the zone is being repositioned.
///
/// Registered as "sniper_turrent" in ComponentRegistry so TowerFactory can attach it.
/// </summary>
public class SniperTurrent : MonoBehaviour, IFactoryInitializable
{
    // ── Config ────────────────────────────────────────────────────────
    public float damage   = 60f;
    public float cooldown = 2.0f;

    // ── State ─────────────────────────────────────────────────────────
    private SniperZone _zone;
    private float      _cooldownTimer;
    private bool       _ready = true;

    // ── Registry ──────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("sniper_turrent", typeof(SniperTurrent));

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Start() => _zone = GetComponent<SniperZone>();

    void Update()
    {
        if (!_ready)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f) _ready = true;
        }

        if (_ready && (_zone == null || !_zone.IsRepositioning))
            TryFire();
    }

    // ── Firing ────────────────────────────────────────────────────────

    void TryFire()
    {
        if (_zone == null) return;

        UnitManager best          = null;
        float       bestProgress  = -1f;

        foreach (var unit in FindObjectsByType<UnitManager>(FindObjectsSortMode.None))
        {
            if (!unit.isAlive) continue;
            if (!_zone.Contains(unit.transform.position)) continue;

            var follower = unit.GetComponent<RouteFollower>();
            float prog   = follower != null ? follower.Progress : 0f;
            if (prog > bestProgress) { bestProgress = prog; best = unit; }
        }

        if (best == null) return;

        // Hitscan — instant damage
        best.TakeDamage(damage, 0f, 0f, float.MaxValue, DamageType.Piercing);

        // Brief visual flash from tower to target
        StartCoroutine(DrawBeam(transform.position, best.transform.position));

_ready         = false;
        _cooldownTimer = cooldown;
    }

    IEnumerator DrawBeam(Vector3 from, Vector3 to)
    {
        var go = new GameObject("_SniperBeam");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace    = true;
        lr.positionCount    = 2;
        lr.startWidth       = 0.04f;
        lr.endWidth         = 0.01f;
        lr.numCapVertices   = 2;
        lr.sortingLayerName = "Default";
        lr.sortingOrder     = 9;
        lr.material         = new Material(Shader.Find("Sprites/Default"));
        lr.startColor       = new Color(0.9f, 0.95f, 1f, 0.9f);
        lr.endColor         = new Color(0.5f, 0.7f, 1f, 0f);
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);

        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(0.9f, 0f, t / 0.12f);
            lr.startColor = new Color(0.9f, 0.95f, 1f, alpha);
            lr.endColor   = new Color(0.5f, 0.7f, 1f, alpha * 0.5f);
            yield return null;
        }
        Destroy(go);
    }

    // ── IFactoryInitializable ─────────────────────────────────────────

    [System.Serializable]
    private class TurrentData { public float damage = 60f; public float cooldown = 2f; }

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<TurrentData>(dataJson);
        if (d == null) return;
        damage   = d.damage;
        cooldown = d.cooldown;
    }
}
