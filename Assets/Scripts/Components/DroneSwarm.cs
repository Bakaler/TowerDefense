using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic component: spawns N drones that wander the tower radius and attack nearby enemies.
/// JSON keys: droneCount, range, cooldown, damage, bulletSpeed, bulletLifetime,
///            droneColorR/G/B, bulletColorR/G/B
/// </summary>
public class DroneSwarm : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("drone_swarm", typeof(DroneSwarm));

    public int   droneCount     = 4;
    public float range          = 5f;
    public float cooldown       = 0.8f;
    public float damage         = 6f;
    public float bulletSpeed    = 14f;
    public float bulletLifetime = 0.6f;
    public float maxAwayTime    = 6f;
    public float restDuration   = 1f;
    public Color droneColor     = new Color(1f, 0.85f, 0.1f, 1f);
    public Color bulletColor    = new Color(1f, 0.95f, 0.3f, 1f);

    private TowerInfo            _info;
    private readonly List<Drone> _drones = new();
    private Effect               _impactEffect;
    private string               _pendingEffectId;

    public string droneSpritePath  = "";
    public string bulletSpritePath = "";
    public float  droneScale       = 1f;
    public float  droneAnimFps     = 12f;

    [System.Serializable]
    class Data
    {
        public int    droneCount      = 4;
        public float  range           = 5f;
        public float  cooldown        = 0.8f;
        public float  damage          = 6f;
        public float  bulletSpeed     = 14f;
        public float  bulletLifetime  = 0.6f;
        public float  maxAwayTime     = 6f;
        public float  restDuration    = 1f;
        public float  droneColorR     = 1f;
        public float  droneColorG     = 0.85f;
        public float  droneColorB     = 0.1f;
        public float  bulletColorR    = 1f;
        public float  bulletColorG    = 0.95f;
        public float  bulletColorB    = 0.3f;
        public string effectId        = "drone_impact";
        public string droneSpritePath  = "";
        public string bulletSpritePath = "";
        public float  droneScale       = 1f;
        public float  droneAnimFps     = 12f;
    }

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<Data>(dataJson);
        if (d == null) return;
        droneCount     = d.droneCount;
        range          = d.range;
        cooldown       = d.cooldown;
        damage         = d.damage;
        bulletSpeed    = d.bulletSpeed;
        bulletLifetime = d.bulletLifetime;
        maxAwayTime    = d.maxAwayTime;
        restDuration   = d.restDuration;
        droneColor     = new Color(d.droneColorR,  d.droneColorG,  d.droneColorB,  1f);
        bulletColor    = new Color(d.bulletColorR, d.bulletColorG, d.bulletColorB, 1f);
        droneSpritePath  = d.droneSpritePath;
        bulletSpritePath = d.bulletSpritePath;
        droneScale       = d.droneScale;
        droneAnimFps     = d.droneAnimFps;
        _pendingEffectId = d.effectId;
        if (!string.IsNullOrEmpty(d.effectId) && EffectLibrary.Instance != null)
            _impactEffect = EffectLibrary.Instance.GetEffect(d.effectId);
    }

    void Awake() => _info = GetComponent<TowerInfo>();

    void Start()
    {
        if (_impactEffect == null && !string.IsNullOrEmpty(_pendingEffectId) && EffectLibrary.Instance != null)
            _impactEffect = EffectLibrary.Instance.GetEffect(_pendingEffectId);
        SpawnDrones();
    }

    void SpawnDrones()
    {
        Sprite[] frames = DroneFrames();
        for (int i = 0; i < droneCount; i++)
        {
            float a  = i * (360f / droneCount) * Mathf.Deg2Rad;
            var   go = new GameObject($"Drone_{i}");
            go.transform.position   = (Vector2)transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f;
            go.transform.localScale = Vector3.one * droneScale;
            go.transform.SetParent(transform);

            var sr              = go.AddComponent<SpriteRenderer>();
            sr.sprite           = frames.Length > 0 ? frames[0] : DroneSprite();
            sr.sortingLayerName = "Towers";
            sr.sortingOrder     = 100;

            if (frames.Length > 1)
            {
                float offset = i * (1f / droneAnimFps / droneCount);
                var anim = go.AddComponent<SpriteAnimator>();
                anim.Setup(frames, droneAnimFps);
                anim.OffsetTime(offset);
            }

            var drone            = go.AddComponent<Drone>();
            drone.swarm          = this;
            drone.range          = range;
            drone.cooldown       = cooldown + i * 0.2f;
            drone.damage         = damage;
            drone.impactEffect   = _impactEffect;
            drone.bulletSpeed    = bulletSpeed;
            drone.bulletLifetime = bulletLifetime;
            drone.bulletColor    = bulletColor;
            drone.bulletSprite   = BulletSprite();
            drone.wanderRadius   = 0.8f;
            drone.noticeRange    = range;
            drone.maxAwayTime    = maxAwayTime + i * 0.5f;  // stagger returns so not all leave at once
            drone.restDuration   = restDuration;

            _drones.Add(drone);
        }
    }

    // Returns raw damage; scaling is applied by Effect_Damage via OriginTower
    public float GetDamage() => damage;

    Sprite[] DroneFrames()
    {
        if (!string.IsNullOrEmpty(droneSpritePath))
        {
            var frames = Resources.LoadAll<Sprite>(droneSpritePath);
            if (frames != null && frames.Length > 0) return frames;
        }
        return new Sprite[] { FallbackDroneSprite() };
    }

    Sprite DroneSprite() => DroneFrames()[0];

    Sprite FallbackDroneSprite()
    {
        const int S = 10;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        float cx = S / 2f, cy = S / 2f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
                float d  = (dx * dx) / (cx * cx * 0.7f) + (dy * dy) / (cy * cy);
                tex.SetPixel(x, y, d <= 1f ? Color.white : Color.clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }

    Sprite BulletSprite()
    {
        if (!string.IsNullOrEmpty(bulletSpritePath))
        {
            var loaded = Resources.Load<Sprite>(bulletSpritePath);
            if (loaded != null) return loaded;
        }
        return null;
    }
}
