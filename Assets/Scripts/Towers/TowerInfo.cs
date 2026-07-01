using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attached to every placed tower. Tracks runtime stats and fires an event
/// when the tower is clicked so GameHUD can show the info panel.
/// </summary>
public class TowerInfo : MonoBehaviour
{
    // ── Populated by TowerFactory ─────────────────────────────────────
    public string      definitionId = "";
    public string      displayName  = "";
    public string      description  = "";
    public BalanceType balanceType  = BalanceType.Physical;
    public float       damage       = 0f;
    public float       cooldown     = 0f;
    public int         resourceCost = 0;

    /// <summary>True on the ghost preview — excluded from balance counts.</summary>
    public bool isGhost = false;

    // ── Upgrade ───────────────────────────────────────────────────────
    public int   maxTier              = 1;
    public float upgradeStatMultiplier = 2.25f;
    public int   towerTier            = 1;   // 1=T1, 2=T2, 3=T3 — sets research gate
    public int   Tier                 { get; private set; } = 1;
    public float StatMultiplier       { get; private set; } = 1f;
    public float ExtraMultiplier      { get; set; }         = 1f;

    // Cached — set by TowerBuffHandler.GetOrAdd so it's always available
    [System.NonSerialized] public TowerBuffHandler _buffHandler;

    /// <summary>Total damage multiplier: StatMultiplier × aura × tower buffs.</summary>
    public float EffectiveDamageMult =>
        StatMultiplier * ExtraMultiplier * AuraDamageMultiplier
        * (1f + (_buffHandler != null ? _buffHandler.DamageMult : 0f));

    /// <summary>True when the FullRefund modifier is active (set as a buff at level load).</summary>
    public bool FullRefundActive => _buffHandler != null && _buffHandler.HasBuff("mod_full_refund");

    // ── Aura buffs ────────────────────────────────────────────────────
    private Dictionary<object, (float dmg, float spd)> _auraBuffs;
    public float AuraDamageMultiplier { get; private set; } = 1f;
    public float AuraSpeedMultiplier  { get; private set; } = 1f;

    public void ApplyAura(object source, float dmgMult, float spdMult)
    {
        if (_auraBuffs == null) _auraBuffs = new Dictionary<object, (float, float)>();
        _auraBuffs[source] = (dmgMult, spdMult);
        RecalcAuras();
    }

    public void RemoveAura(object source)
    {
        if (_auraBuffs == null || !_auraBuffs.Remove(source)) return;
        RecalcAuras();
    }

    void RecalcAuras()
    {
        float d = 1f, s = 1f;
        if (_auraBuffs != null)
            foreach (var (dm, sm) in _auraBuffs.Values) { d *= dm; s *= sm; }
        AuraDamageMultiplier = d;
        AuraSpeedMultiplier  = s;
    }

    public int  UpgradeCost => resourceCost * (1 << Tier);   // tier1→2: cost*2, tier2→3: cost*4
    public bool CanUpgrade  => Tier < maxTier;

    // Total gold spent = base + all upgrade costs = resourceCost * (2^Tier - 1)
    public int TotalSpent  => resourceCost * ((1 << Tier) - 1);
    public int SellRefund  => Mathf.RoundToInt(TotalSpent * (FullRefundActive ? 1f : 0.75f));

    public void Sell(ResourceManagerScript rm)
    {
        if (rm != null) rm.ChangeResourceOne(SellRefund);
        OnTowerClickedPublic(null);
        Destroy(gameObject);
    }

    // Research tier required to perform the NEXT upgrade (0 = no requirement)
    public int RequiredResearchTier
    {
        get {
            int next = Tier + 1;
            if (next >= 5) return 5;
            if (next >= 4) return 4;
            switch (towerTier)
            {
                case 1:  return next >= 3 ? 2 : 0;
                case 2:  return next >= 3 ? 3 : 2;
                default: return 3;
            }
        }
    }

    public bool HasResearchForUpgrade
    {
        get {
            int req = RequiredResearchTier;
            if (req == 0) return true;
            var tm = TechManager.Instance;
            if (tm == null) return true;
            switch (req)
            {
                case 2: return tm.T2Unlocked;
                case 3: return tm.T3Unlocked;
                case 4: return tm.T4Unlocked;
                case 5: return tm.T5Unlocked;
                default: return true;
            }
        }
    }

    // ── Tier rings ────────────────────────────────────────────────────
    private const int   RING_SEGMENTS = 32;
    private const float RING_WIDTH    = 0.018f;
    private const float RING_BASE_R   = 0.22f;
    private const float RING_SPACING  = 0.10f;

    // Gold T2, cyan T3, purple T4, hot-pink T5 — alpha ramps up with each tier
    private static readonly Color[] RING_COLORS =
    {
        new Color(1.00f, 0.82f, 0.20f, 0.70f),   // tier 2 — gold
        new Color(0.45f, 0.95f, 1.00f, 0.48f),   // tier 3 — cyan
        new Color(0.75f, 0.20f, 1.00f, 0.28f),   // tier 4 — purple
        new Color(1.00f, 0.15f, 0.55f, 0.12f),   // tier 5 — hot pink
    };

    private LineRenderer[] _tierRings = new LineRenderer[0];

    void RebuildTierRings()
    {
        // Destroy existing rings
        foreach (var lr in _tierRings)
            if (lr != null) Destroy(lr.gameObject);

        int ringCount = Tier - 1;   // tier 1 = 0 rings, tier 2 = 1 ring, tier 3 = 2 rings
        _tierRings = new LineRenderer[ringCount];

        for (int i = 0; i < ringCount; i++)
        {
            var go = new GameObject($"TierRing{i + 1}");
            go.transform.SetParent(transform, false);

            float radius = RING_BASE_R + i * RING_SPACING;
            Color col    = i < RING_COLORS.Length ? RING_COLORS[i] : Color.white;

            var lr               = go.AddComponent<LineRenderer>();
            lr.loop              = true;
            lr.positionCount     = RING_SEGMENTS;
            lr.startWidth        = RING_WIDTH;
            lr.endWidth          = RING_WIDTH;
            lr.useWorldSpace     = false;
            lr.sortingLayerName  = "Units";
            lr.sortingOrder      = 19;
            lr.material          = new Material(Shader.Find("Sprites/Default"));
            lr.startColor        = col;
            lr.endColor          = col;

            for (int s = 0; s < RING_SEGMENTS; s++)
            {
                float angle = s / (float)RING_SEGMENTS * Mathf.PI * 2f;
                lr.SetPosition(s, new Vector3(Mathf.Cos(angle) * radius,
                                              Mathf.Sin(angle) * radius, 0f));
            }

            lr.gameObject.SetActive(_selected == this);
            _tierRings[i] = lr;
        }
    }

    // Set by TowerFactory after reading the definition
    [HideInInspector] public float rangePerTier       = 0f;
    [HideInInspector] public float balanceMultiplier  = 1f;   // doubles each upgrade tier
    private float _baseRange = 0f;

    public void SetBaseRange(float r) => _baseRange = r;

    public void RefreshSprite()
    {
        if (TowerFactory.Instance == null) return;
        if (!TowerDefinitionLibrary.Instance.TryGet(definitionId, out var def)) return;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            var sp = TowerFactory.ResolveTieredSprite(def.id, Tier, def.spritePath);
            if (sp != null) { sr.sprite = sp; sr.color = def.tintColor; }
        }

        var turretGO = transform.Find("Turret");
        if (turretGO != null)
        {
            var tsr = turretGO.GetComponent<SpriteRenderer>();
            if (tsr != null)
            {
                var sp = TowerFactory.ResolveTieredSprite(def.id + "_turret", Tier, def.turretSpritePath);
                if (sp != null) tsr.sprite = sp;
            }
        }
    }

    public bool TryUpgrade(ResourceManagerScript rm)
    {
        if (!CanUpgrade) return false;
        if (!HasResearchForUpgrade) return false;
        int cost = UpgradeCost;
        if (rm == null || rm.resourceOne < cost) return false;

        rm.ChangeResourceOne(-cost);
        Tier++;
        StatMultiplier    = Mathf.Pow(upgradeStatMultiplier, Tier - 1);
        ObjectiveTracker.NotifyUpgrade(definitionId);
        balanceMultiplier *= 2f;   // each tier doubles this tower's balance contribution
        RebuildTierRings();
        RefreshSprite();

        // Grow range for towers that have rangePerTier set (e.g. slow, root)
        if (rangePerTier > 0f)
        {
            float newRange = _baseRange + rangePerTier * (Tier - 1);
            var col = GetComponent<CircleCollider2D>();
            if (col != null) col.radius = newRange;
            SetupRangeCircle(newRange);
        }

        return true;
    }

    // ── Runtime ───────────────────────────────────────────────────────
    public int KillCount { get; private set; }

    public float FireRate => cooldown > 0f ? 1f / cooldown : 0f;

    // ── Event ─────────────────────────────────────────────────────────
    public static event Action<TowerInfo> OnTowerClicked;
    public static event Action<TowerInfo> OnTowerKill;

    public static void OnTowerClickedPublic(TowerInfo info) => OnTowerClicked?.Invoke(info);

    // ── Selection range circle ────────────────────────────────────────
    private static TowerInfo _selected;
    private LineRenderer     _rangeCircle;
    private const int        CIRCLE_SEGMENTS = 64;
    private const float      CIRCLE_WIDTH    = 0.04f;

    public void SetupRangeCircle(float radius)
    {
        if (radius <= 0f || isGhost) return;

        if (_rangeCircle == null)
        {
            var go = new GameObject("RangeCircle");
            go.transform.SetParent(transform, false);

            _rangeCircle                  = go.AddComponent<LineRenderer>();
            _rangeCircle.loop             = true;
            _rangeCircle.positionCount    = CIRCLE_SEGMENTS;
            _rangeCircle.startWidth       = CIRCLE_WIDTH;
            _rangeCircle.endWidth         = CIRCLE_WIDTH;
            _rangeCircle.useWorldSpace    = false;
            _rangeCircle.sortingLayerName = "Units";
            _rangeCircle.sortingOrder     = 20;
            _rangeCircle.material         = new Material(Shader.Find("Sprites/Default"));
            _rangeCircle.startColor       = new Color(0.2f, 1f, 0.35f, 0.65f);
            _rangeCircle.endColor         = new Color(0.2f, 1f, 0.35f, 0.65f);
            _rangeCircle.gameObject.SetActive(false);
        }

        // Compensate for tower's local scale so the circle matches world-space range
        float localRadius = radius / Mathf.Max(0.01f, transform.localScale.x);
        for (int i = 0; i < CIRCLE_SEGMENTS; i++)
        {
            float angle = i / (float)CIRCLE_SEGMENTS * Mathf.PI * 2f;
            _rangeCircle.SetPosition(i, new Vector3(Mathf.Cos(angle) * localRadius,
                                                     Mathf.Sin(angle) * localRadius, 0f));
        }
    }

    public void RegisterKill()
    {
        KillCount++;
        OnTowerKill?.Invoke(this);
    }

    // ── Tower registry ────────────────────────────────────────────────
    public static readonly System.Collections.Generic.HashSet<TowerInfo> All
        = new System.Collections.Generic.HashSet<TowerInfo>();

    void OnEnable()
    {
        OnTowerClicked += HandleTowerClicked;
        if (!isGhost) All.Add(this);
    }

    void OnDisable()
    {
        OnTowerClicked -= HandleTowerClicked;
        All.Remove(this);
    }

    void HandleTowerClicked(TowerInfo clicked)
    {
        bool isSelected = clicked == this;
        if (isSelected) _selected = this;
        else if (_selected == this) _selected = null;

        if (_rangeCircle != null) _rangeCircle.gameObject.SetActive(isSelected);
        foreach (var lr in _tierRings)
            if (lr != null) lr.gameObject.SetActive(isSelected);
    }

    // World-space click radius — used by GameHUD.Update() for manual proximity picking
    public float ClickRadius => Mathf.Max(0.5f, transform.localScale.x * 0.6f);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, ClickRadius);
    }
}
