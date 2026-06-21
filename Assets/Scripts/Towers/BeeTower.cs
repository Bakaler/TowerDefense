using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns 4 orbiting bees that each independently target and shoot the nearest enemy.
/// </summary>
public class BeeTower : MonoBehaviour, IFactoryInitializable
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => ComponentRegistry.Register("bee_tower", typeof(BeeTower));

    public int   beeCount       = 4;
    public float beeRange       = 5f;
    public float beeCooldown    = 0.8f;
    public float beeDamage      = 6f;
    public float bulletSpeed    = 14f;
    public float bulletLifetime = 0.6f;
    public Color beeColor       = new Color(1f, 0.85f, 0.1f, 1f);
    public Color bulletColor    = new Color(1f, 0.95f, 0.3f, 1f);

    private TowerInfo _info;
    private readonly List<BeeUnit> _bees = new();

    [System.Serializable]
    class Data
    {
        public int   beeCount       = 4;
        public float beeRange       = 5f;
        public float beeCooldown    = 0.8f;
        public float beeDamage      = 6f;
        public float bulletSpeed    = 14f;
        public float bulletLifetime = 0.6f;
    }

    public void Initialize(string dataJson)
    {
        if (string.IsNullOrEmpty(dataJson)) return;
        var d = JsonUtility.FromJson<Data>(dataJson);
        if (d == null) return;
        beeCount       = d.beeCount;
        beeRange       = d.beeRange;
        beeCooldown    = d.beeCooldown;
        beeDamage      = d.beeDamage;
        bulletSpeed    = d.bulletSpeed;
        bulletLifetime = d.bulletLifetime;
    }

    void Awake()
    {
        _info = GetComponent<TowerInfo>();
    }

    void Start()
    {
        SpawnBees();
    }

    void SpawnBees()
    {
        for (int i = 0; i < beeCount; i++)
        {
            var go = new GameObject($"Bee_{i}");
            float a = i * (360f / beeCount) * Mathf.Deg2Rad;
            go.transform.position   = (Vector2)transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f;
            go.transform.localScale = Vector3.one * 0.125f;
            go.transform.SetParent(transform);

            var sr              = go.AddComponent<SpriteRenderer>();
            sr.sprite           = MakeBeeSprite();
            sr.color            = beeColor;
            sr.sortingLayerName = "Units";
            sr.sortingOrder     = 15;

            var bee            = go.AddComponent<BeeUnit>();
            bee.tower          = this;
            bee.range          = beeRange;
            bee.cooldown       = beeCooldown + i * 0.2f;  // stagger fire
            bee.damage         = beeDamage;
            bee.bulletSpeed    = bulletSpeed;
            bee.bulletLifetime = bulletLifetime;
            bee.bulletColor    = bulletColor;
            bee.wanderRadius   = 0.8f;
            bee.noticeRange    = beeRange * 0.55f;

            _bees.Add(bee);
        }
    }

    public float GetDamage()  => beeDamage  * (_info != null ? _info.StatMultiplier * _info.ExtraMultiplier : 1f);
    public TowerInfo Info     => _info;

    static Sprite _beeSprite;
    static Sprite MakeBeeSprite()
    {
        if (_beeSprite != null) return _beeSprite;
        const int S = 10;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        float cx = S / 2f, cy = S / 2f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x - cx + 0.5f, dy = y - cy + 0.5f;
                // Slightly oval body
                float d = (dx * dx) / (cx * cx * 0.7f) + (dy * dy) / (cy * cy);
                tex.SetPixel(x, y, d <= 1f ? Color.white : Color.clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        _beeSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return _beeSprite;
    }
}
