using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds enemy unit GameObjects entirely in code — no prefabs.
/// All configuration comes from UnitDefinition (loaded from units.json).
///
/// Build order:
///   1. new GameObject — no prefab involved
///   2. Physics setup: Rigidbody2D (kinematic) + CircleCollider2D (trigger)
///   3. SpriteRenderer — sprite loaded from Resources via def.spritePath
///   4. UnitManager — stats applied from definition
///   5. Resolve + add extra components via ComponentRegistry
///   6. Second pass: Initialize(dataJson) on all IFactoryInitializable components
///
/// Enemy layer is set to def.layer (defaults to "Enemy" layer, int 10).
/// </summary>
public class UnitFactory : MonoBehaviour
{
    public static UnitFactory Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────

    public GameObject Build(string definitionId, Vector3 position)
    {
        if (UnitDefinitionLibrary.Instance == null)
        {
            Debug.LogError("[UnitFactory] UnitDefinitionLibrary not in scene.");
            return null;
        }

        if (!UnitDefinitionLibrary.Instance.TryGet(definitionId, out var def))
        {
            Debug.LogError($"[UnitFactory] No definition found for id '{definitionId}'.");
            return null;
        }

        return BuildFromDefinition(def, position);
    }

    public GameObject BuildFromDefinition(UnitDefinition def, Vector3 position)
    {
        if (def == null) return null;

        // ── 1. Create GameObject ───────────────────────────────────
        var go = new GameObject(def.displayName ?? def.id);
        go.transform.position = position;
        go.layer = def.layer > 0 ? def.layer : LayerMask.NameToLayer("Enemy");
        if (go.layer < 0) go.layer = 10; // fallback to layer index 10

        // ── 2. Physics ────────────────────────────────────────────
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType     = RigidbodyType2D.Kinematic; // movement driven by RouteFollower

        // colliderRadius is world-space — divide by scale so Unity's scaling
        // doesn't inflate it (CircleCollider2D scales with the transform).
        float unitScale = def.scale > 0f ? def.scale : 1f;
        float worldRadius = def.colliderRadius > 0f ? def.colliderRadius : 0.3f;
        var col = go.AddComponent<CircleCollider2D>();
        col.radius    = worldRadius / unitScale;
        col.isTrigger = true;

        // ── 3. Visual ─────────────────────────────────────────────
        var sr = go.AddComponent<SpriteRenderer>();

        // Sprite sheet takes priority over single spritePath
        if (!string.IsNullOrEmpty(def.spriteSheet) && def.spriteIndex >= 0)
        {
            var sheet = Resources.LoadAll<Sprite>(def.spriteSheet);
            if (sheet != null && def.spriteIndex < sheet.Length)
                { sr.sprite = sheet[def.spriteIndex]; sr.color = Color.white; }
            else
                Debug.LogWarning($"[UnitFactory] Sheet '{def.spriteSheet}' index {def.spriteIndex} not found for '{def.id}'.");
        }
        else if (!string.IsNullOrEmpty(def.spritePath))
        {
            var sprite = Resources.Load<Sprite>(def.spritePath);
            if (sprite != null) { sr.sprite = sprite; sr.color = Color.white; }
            else Debug.LogWarning($"[UnitFactory] Sprite not found at '{def.spritePath}' for '{def.id}'.");
        }

        if (sr.sprite == null && string.IsNullOrEmpty(def.animSheet))
        {
            sr.sprite = MakePlaceholderSprite(def.scale > 0f ? def.scale : 1f);
            sr.color  = def.debugColor;
        }
        else if (def.tintColor != Color.white && def.tintColor.a > 0f)
        {
            sr.color = def.tintColor;
        }
        sr.sortingLayerName = "Units";

        // Scale — applied after sprite so placeholder doesn't override def.scale
        float s = def.scale > 0f ? def.scale : 1f;
        go.transform.localScale = new Vector3(s, s, 1f);

        // ── 3b. Animation ─────────────────────────────────────────
        if (!string.IsNullOrEmpty(def.animSheet))
        {
            var frames = Resources.LoadAll<Sprite>(def.animSheet);
            if (frames != null && frames.Length > 0)
            {
                if (def.animReverse) System.Array.Reverse(frames);
                sr.sprite = frames[0];
                sr.color  = def.tintColor.a > 0f ? def.tintColor : Color.white;
                Sprite[] deathFrames = null;
                if (!string.IsNullOrEmpty(def.animDeathSheet))
                {
                    deathFrames = Resources.LoadAll<Sprite>(def.animDeathSheet);
                    if (def.animReverse && deathFrames != null && deathFrames.Length > 0)
                        System.Array.Reverse(deathFrames);
                }
                var anim = go.AddComponent<SpriteAnimator>();
                anim.Setup(frames, def.animFps, deathFrames, def.animDeathFps);
            }
            else
                Debug.LogWarning($"[UnitFactory] Animation sheet '{def.animSheet}' not found for '{def.id}'.");
        }

        // Never spawn an invisible unit — placeholder circle if nothing resolved
        if (sr.sprite == null)
        {
            sr.sprite = MakePlaceholderSprite(s);
            sr.color  = def.debugColor;
        }

        // ── 4. UnitManager ────────────────────────────────────────
        var unit = go.AddComponent<UnitManager>();
        unit.myCollider      = col;
        unit.definitionId    = def.id;
        float hp             = def.life * LevelSelection.EnemyHpMult;
        unit.lifeMax         = hp;
        unit.lifeCurrent     = hp;
        if (def.shield > 0f)
        {
            // Shields scale with difficulty like life does
            float sh           = def.shield * LevelSelection.EnemyHpMult;
            unit.hasShields    = true;
            unit.shieldMax     = sh;
            unit.shieldCurrent = sh;
        }
        float spd            = def.speed * LevelSelection.EnemySpeedMult;
        unit.speedMax        = spd;
        unit.speedCurrent    = spd;
        unit.physicalDefense  = def.physicalDefense;
        unit.elementalDefense = def.elementalDefense;
        unit.arcanaDefense    = def.arcanaDefense;
        unit.deathBlow       = def.deathBlow;
        unit.rotateToMovement  = def.rotateToMovement;
        unit.spriteAngleOffset = def.spriteAngleOffset;
        unit.tags            = def.tags ?? System.Array.Empty<string>();
        unit.canGoInvisible  = CanGoInvisible(def);
        unit.isAlive         = true;

        // ── 5. Extra components ───────────────────────────────────
        var orderedKeys   = ResolveOrder(def.components);
        var dataOverrides = BuildDataMap(def.components);

        foreach (var key in orderedKeys)
        {
            if (!ComponentRegistry.TryGet(key, out var type))
            {
                Debug.LogWarning($"[UnitFactory] Component key '{key}' not registered. Skipping.");
                continue;
            }
            if (go.GetComponent(type) == null)
                go.AddComponent(type);
        }

        // ── 6. Initialize ─────────────────────────────────────────
        foreach (var init in go.GetComponents<IFactoryInitializable>())
        {
            string key  = ComponentRegistry.KeyFor(init.GetType());
            string data = key != null && dataOverrides.TryGetValue(key, out var d) ? d : null;
            init.Initialize(data);
        }

        // ── 7. Starting behaviors (permanent, immune, auras) ──────
        if (def.startingBehaviors != null && def.startingBehaviors.Length > 0)
        {
            if (BehaviorLibrary.Instance != null)
            {
                var bh = go.GetComponent<BehaviorHandler>() ?? go.AddComponent<BehaviorHandler>();
                foreach (var bId in def.startingBehaviors)
                {
                    if (BehaviorLibrary.Instance.TryGet(bId, out var bDef))
                        bh.ApplyPermanent(bDef);
                    else
                        Debug.LogWarning($"[UnitFactory] Starting behavior '{bId}' not found in BehaviorLibrary.");
                }
            }
        }

        // ── 8. Abilities cast on cooldown (cleanses, barriers, zaps) ──
        if (def.abilities != null && def.abilities.Length > 0)
            go.AddComponent<UnitAbilityCaster>().Setup(def.abilities);

        return go;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// True when any starting behavior can render the unit invisible — either the
    /// behavior itself (Invisible type) or one it grants on tick (cloak cycles).
    /// Feeds the "Invisible" tower targeting mode.
    /// </summary>
    public static bool CanGoInvisible(UnitDefinition def)
    {
        if (def.startingBehaviors == null || BehaviorLibrary.Instance == null) return false;
        foreach (var bId in def.startingBehaviors)
        {
            if (!BehaviorLibrary.Instance.TryGet(bId, out var b)) continue;
            if (b.behaviorType == BehaviorType.Invisible) return true;
            if (!string.IsNullOrEmpty(b.tickBehaviorId)
                && BehaviorLibrary.Instance.TryGet(b.tickBehaviorId, out var tickDef)
                && tickDef.behaviorType == BehaviorType.Invisible) return true;
        }
        return false;
    }

    /// <summary>Creates a small circle sprite used as a unit placeholder. Scale is factored into PPU so world size stays ~0.4 units regardless of def.scale.</summary>
    private static Sprite MakePlaceholderSprite(float scale)
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float r = size / 2f - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center + 0.5f, dy = y - center + 0.5f;
                tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        // Target ~0.4 world units: world_size = (size/PPU)*scale → PPU = size*scale/0.4
        int ppu = Mathf.RoundToInt(size * scale / 0.4f);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
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
