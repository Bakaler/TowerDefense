using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds tower GameObjects entirely in code — no prefabs.
/// All configuration comes from TowerDefinition (loaded from towers.json).
///
/// Build order:
///   1. new GameObject — no prefab
///   2. Physics: Rigidbody2D (kinematic static) + CircleCollider2D trigger (range detector)
///   3. SpriteRenderer — sprite from Resources via def.spritePath
///   4. AbilityManager
///   5. Turrent — range + fireAbility set from definition
///   6. Resolve + add extra components via ComponentRegistry
///   7. Second pass: Initialize(dataJson) on all IFactoryInitializable components
/// </summary>
public class TowerFactory : MonoBehaviour
{
    public static TowerFactory Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────

    public GameObject Build(string definitionId, Vector3 position)
    {
        if (TowerDefinitionLibrary.Instance == null)
        {
            Debug.LogError("[TowerFactory] TowerDefinitionLibrary not in scene.");
            return null;
        }

        if (!TowerDefinitionLibrary.Instance.TryGet(definitionId, out var def))
        {
            Debug.LogError($"[TowerFactory] No definition found for id '{definitionId}'.");
            return null;
        }

        return BuildFromDefinition(def, position);
    }

    public GameObject BuildFromDefinition(TowerDefinition def, Vector3 position)
    {
        if (def == null) return null;

        // ── 1. Create GameObject ───────────────────────────────────
        var go = new GameObject(def.displayName ?? def.id);
        go.transform.position = position;

        // ── 2. Physics ────────────────────────────────────────────
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Range detection trigger — seeded from def.range; Turrent.Start() may refine it
        // if the tower has a fireAbility with its own range value.
        var rangeCol       = go.AddComponent<CircleCollider2D>();
        rangeCol.radius    = def.range > 0f ? def.range : 1f;
        rangeCol.isTrigger = true;

        // Small solid collider used by TowerPlacer overlap check to prevent stacking.
        // Divide by scale so the world-space radius matches def.placementRadius regardless of tower scale.
        float towerScale   = def.scale > 0f ? def.scale : 1f;
        var bodyCol        = go.AddComponent<CircleCollider2D>();
        bodyCol.radius     = (def.placementRadius > 0f ? def.placementRadius : 0.4f) / towerScale;
        bodyCol.isTrigger  = false;

        // ── 3. Visual ─────────────────────────────────────────────
        var sr = go.AddComponent<SpriteRenderer>();

        if (def.animFps > 0f)
        {
            var frames = Resources.LoadAll<Sprite>(ResolveTieredPath(def.id, 1, def.spritePath));
            if (frames != null && frames.Length > 0)
            {
                sr.sprite = frames[0];
                sr.color  = def.tintColor;
                var anim = go.AddComponent<SpriteAnimator>();
                anim.Setup(frames, def.animFps);
            }
            else { sr.sprite = MakePlaceholderSprite(); sr.color = def.debugColor; }
        }
        else
        {
            var baseSprite = ResolveTieredSprite(def.id, 1, def.spritePath);
            if (baseSprite != null) { sr.sprite = baseSprite; sr.color = def.tintColor; }
            else                    { sr.sprite = MakePlaceholderSprite(); sr.color = def.debugColor; }
        }
        sr.sortingLayerName = "Towers";

        float s = def.scale > 0f ? def.scale : 1f;
        go.transform.localScale = new Vector3(s, s, 1f);

        // ── 3b. Turret child (optional rotating layer) ────────────
        var turretSprite = ResolveTieredSprite(def.id + "_turret", 1, def.turretSpritePath);
        if (turretSprite != null)
        {
            var turretGO = new GameObject("Turret");
            turretGO.transform.SetParent(go.transform, false);
            turretGO.transform.localPosition = Vector3.zero;
            turretGO.transform.localScale    = Vector3.one;
            var tsr              = turretGO.AddComponent<SpriteRenderer>();
            tsr.sprite           = turretSprite;
            tsr.color            = def.tintColor;
            tsr.sortingLayerName = "Towers";
            tsr.sortingOrder     = 1;
        }

        // ── 4. AbilityManager ─────────────────────────────────────
        var abilityManager = go.AddComponent<AbilityManager>();

        // ── 5. Turrent ────────────────────────────────────────────
        var turrent = go.AddComponent<Turrent>();
        // definitionId is the only thing the factory sets on Turrent.
        // Turrent.Start() resolves fireAbilityId → Ability_Effect from the libraries itself.
        turrent.definitionId = def.id;

        // ── 5b. TowerInfo ─────────────────────────────────────────
        var info          = go.AddComponent<TowerInfo>();
        info.definitionId = def.id;
        info.displayName  = def.displayName ?? def.id;
        info.description  = def.description ?? "";
        info.resourceCost = def.resourceCost;
        info.cooldown     = def.displayCooldown > 0f ? def.displayCooldown : ResolveCooldown(def.fireAbilityId);
        info.damage       = def.displayDamage   > 0f ? def.displayDamage   : ResolveDamage(def.fireAbilityId);
        if (System.Enum.TryParse<BalanceType>(def.balanceType, true, out var bt))
            info.balanceType = bt;
        info.rangePerTier = def.rangePerTier;
        info.SetBaseRange(def.range);
        info.maxTier               = def.maxTier > 0 ? def.maxTier : 1;
        info.upgradeStatMultiplier = def.upgradeStatMultiplier > 0f ? def.upgradeStatMultiplier : 2.25f;
        info.towerTier             = def.towerTier > 0 ? def.towerTier : 1;

        // ── 5c. Range circle ──────────────────────────────────────────
        // Use def.range as the initial radius. Turrent.Start() calls SetupRangeCircle
        // again with ability.range once the ability resolves, refining it to the exact value.
        if (def.range > 0f)
            info.SetupRangeCircle(def.range);

        // ── 6. Extra components ───────────────────────────────────
        var orderedKeys   = ResolveOrder(def.components);
        var dataOverrides = BuildDataMap(def.components);

        foreach (var key in orderedKeys)
        {
            if (!ComponentRegistry.TryGet(key, out var type))
            {
                Debug.LogWarning($"[TowerFactory] Component key '{key}' not registered. Skipping.");
                continue;
            }
            if (go.GetComponent(type) == null)
                go.AddComponent(type);
        }

        // ── 7. Initialize ─────────────────────────────────────────
        foreach (var init in go.GetComponents<IFactoryInitializable>())
        {
            string key  = ComponentRegistry.KeyFor(init.GetType());
            string data = key != null && dataOverrides.TryGetValue(key, out var d) ? d : null;
            init.Initialize(data);
        }

        return go;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Looks for Art/Towers/{baseId}_T{tier} counting down to T1,
    /// then falls back to fallbackPath, then returns null.
    /// </summary>
    public static string ResolveTieredPath(string baseId, int tier, string fallbackPath)
    {
        for (int t = tier; t >= 1; t--)
        {
            string path = $"Art/Towers/{baseId}_T{t}";
            if (Resources.Load<Sprite>(path) != null) return path;
        }
        return !string.IsNullOrEmpty(fallbackPath) ? fallbackPath : null;
    }

    public static Sprite ResolveTieredSprite(string baseId, int tier, string fallbackPath)
    {
        for (int t = tier; t >= 1; t--)
        {
            var sp = Resources.Load<Sprite>($"Art/Towers/{baseId}_T{t}");
            if (sp != null) return sp;
        }
        if (!string.IsNullOrEmpty(fallbackPath))
        {
            var sp = Resources.Load<Sprite>(fallbackPath);
            if (sp != null) return sp;
        }
        return null;
    }

    /// <summary>Creates a small white square sprite used as a tower placeholder.</summary>
    private static Sprite MakePlaceholderSprite()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        // Diamond shape so towers look distinct from the circle units
        float center = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center + 0.5f), dy = Mathf.Abs(y - center + 0.5f);
                tex.SetPixel(x, y, dx + dy <= center - 1f ? Color.white : Color.clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static float ResolveCooldown(string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId)) return 0f;
        if (AbilityLibrary.Instance == null) return 0f;
        var ab = AbilityLibrary.Instance.GetAbility(abilityId);
        return ab != null ? ab.cost.cooldownDuration : 0f;
    }

    private static float ResolveDamage(string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId)) return 0f;
        if (AbilityLibrary.Instance == null || EffectLibrary.Instance == null) return 0f;
        var ab = AbilityLibrary.Instance.GetAbility(abilityId);
        if (ab == null) return 0f;
        return FindFirstDamage(ab.effectId, 0);
    }

    // Walk the effect tree up to 4 levels deep looking for the first damage value
    private static float FindFirstDamage(string effectId, int depth)
    {
        if (depth > 4 || string.IsNullOrEmpty(effectId)) return 0f;
        var effect = EffectLibrary.Instance?.GetEffect(effectId);
        return effect != null ? FindFirstDamage(effect, depth) : 0f;
    }

    private static float FindFirstDamage(Effect effect, int depth)
    {
        if (depth > 4 || effect == null) return 0f;
        if (effect is Effect_Damage dmg) return dmg.damageBase;
        if (effect is Effect_Launch_Missile missile) return FindFirstDamage(missile.impactEffectId, depth + 1);
        if (effect is Effect_Launch_Shotgun shotgun) return FindFirstDamage(shotgun.impactEffectId, depth + 1);
        if (effect is Effect_Search_Area area && area.areas.Count > 0 && area.areas[0].effect != null)
            return FindFirstDamage(area.areas[0].effect, depth + 1);
        if (effect is Effect_Set set)
            foreach (var id in set.EffectIds)
            {
                float v = FindFirstDamage(id, depth + 1);
                if (v > 0f) return v;
            }
        return 0f;
    }

    private static List<string> ResolveOrder(ComponentEntry[] entries)
    {
        var visited = new HashSet<string>();
        var result  = new List<string>();
        if (entries == null) return result;
        foreach (var entry in entries)
            if (!string.IsNullOrEmpty(entry.key))
                Visit(entry.key, visited, result);
        return result;
    }

    private static void Visit(string key, HashSet<string> visited, List<string> result)
    {
        if (!visited.Add(key)) return;
        foreach (var dep in ComponentRegistry.GetRequires(key))
            Visit(dep, visited, result);
        result.Add(key);
    }

    private static Dictionary<string, string> BuildDataMap(ComponentEntry[] entries)
    {
        var map = new Dictionary<string, string>();
        if (entries == null) return map;
        foreach (var entry in entries)
            if (!string.IsNullOrEmpty(entry.key) && !string.IsNullOrEmpty(entry.data))
                map[entry.key] = entry.data;
        return map;
    }
}
