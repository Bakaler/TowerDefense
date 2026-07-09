using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds all alive units within a radius and executes an effect on each.
/// Configurable from the inspector (areas list) or from effects.json via ApplyData.
///
/// JSON data fields (creates one area):
///   effectId        — effect to run on each found unit
///   radius          — search radius in world units (default 4)
///   maxTargets      — cap on targets hit; -1 = unlimited (default -1)
///   horizontalArc   — arc in degrees, 360 = full circle (default 360)
///   startingDepth   — minimum distance from origin to count (default 0)
///   searchFromTarget — if true, search originates from Target position
///                      instead of Caster position (use for chain/bounce effects)
/// </summary>
public class Effect_Search_Area : Effect
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register() => EffectRegistry.Register("search_area", typeof(Effect_Search_Area));

    // ── Inspector-configured areas (used when not driven from JSON) ───
    public List<Area> areas = new List<Area>();
    public SearchFilters searchFilters;

    // ── JSON-driven single area (populated by ApplyData) ─────────────
    private bool _searchFromTarget;
    private bool _excludePrimaryTarget = true;
    /// <summary>Beam stops at the first unit carrying a barrier (ShieldBubble):
    /// that unit and everything beyond it takes no damage, and the visual is clamped.</summary>
    private bool  _blockedByBarriers;
    /// <summary>Damage dealt to the barrier that blocks this effect.</summary>
    private float _barrierAbsorb = 20f;

    public override void ApplyData(string dataJson, EffectLibrary library)
    {
        if (string.IsNullOrEmpty(dataJson)) return;

        var d = JsonUtility.FromJson<SearchAreaData>(dataJson);
        if (d == null) return;

        _searchFromTarget       = d.searchFromTarget;
        _excludePrimaryTarget   = d.excludePrimaryTarget;
        _blockedByBarriers      = d.blockedByBarriers;
        _barrierAbsorb          = d.barrierAbsorb;
        _visualSpritePath    = d.visualSpritePath;
        _visualSpriteSheet   = d.visualSpriteSheet;
        _visualSpriteIndex   = d.visualSpriteIndex;
        _visualColor         = d.visualColor;
        _visualWidth         = d.visualWidth;
        _visualLength        = d.visualLength;
        _visualFadeDuration  = d.visualFadeDuration;
        _visualAnimFps          = d.visualAnimFps;
        _visualRotationOffset   = d.visualRotationOffset;

        var effect = library.GetEffect(d.effectId);
        if (effect == null)
        {
            Debug.LogWarning($"[Effect_Search_Area] Could not resolve effectId '{d.effectId}'.");
            return;
        }

        // Build the single area from JSON fields
        areas.Clear();
        areas.Add(new Area
        {
            radius       = d.radius,
            maxTargets   = d.maxTargets,
            horizontalArc = d.horizontalArc,
            startingDepth = d.startingDepth,
            effect       = effect,
        });
    }

    public override void Execute(EffectContext context)
    {
        if (!PassesValidators(context)) return;
        if (areas == null || areas.Count == 0) return;

        // Origin: target position for bounce/chain, caster position otherwise
        Vector2 origin2D;
        if (_searchFromTarget && context.Target != null)
            origin2D = context.Target.transform.position;
        else if (context.AimOrigin2D.HasValue)
            origin2D = context.AimOrigin2D.Value;
        else if (context.Caster != null)
            origin2D = context.Caster.transform.position;
        else
            return;

        Vector2 forward2D = context.AimDirection2D ?? Vector2.up;

        foreach (var area in areas)
        {
            if (area?.effect == null) continue;

            Vector2 offsetOrigin = origin2D + forward2D.normalized * area.castOffset;
            float   finalRadius  = area.radius + area.radiusBonus;

            Collider2D[] hits      = Physics2D.OverlapCircleAll(offsetOrigin, finalRadius);
            var          alreadyHit = new HashSet<UnitParentClass>();

            if (_excludePrimaryTarget && context.Target != null)
                alreadyHit.Add(context.Target);

            // Collect valid candidates first so barrier-blocked beams can process nearest-first
            var candidates = new List<(UnitParentClass unit, float distance)>();

            foreach (var hit in hits)
            {
                var candidate = hit.GetComponentInParent<UnitParentClass>();
                if (candidate == null || alreadyHit.Contains(candidate)) continue;
                if (searchFilters != null && !SearchFilterUtility.PassesFilters(context.Caster, candidate, searchFilters.Flags)) continue;

                Vector2 toTarget = (Vector2)candidate.transform.position - offsetOrigin;
                float   distance = toTarget.magnitude;

                if (distance < area.startingDepth || distance > finalRadius) continue;

                if (area.horizontalArc < 360f)
                {
                    float angle = Vector2.SignedAngle(forward2D, toTarget) - area.facingAdjustment;
                    if (Mathf.Abs(angle) > area.horizontalArc * 0.5f) continue;
                }

                alreadyHit.Add(candidate);
                candidates.Add((candidate, distance));
            }

            if (_blockedByBarriers)
                candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            int   targetsHit    = 0;
            float blockedLength = -1f;   // <0 = beam ran its full length

            foreach (var (candidate, distance) in candidates)
            {
                if (area.maxTargets >= 0 && targetsHit >= area.maxTargets) break;

                if (_blockedByBarriers)
                {
                    var barrier = candidate.GetComponentInChildren<ShieldBubble>();
                    if (barrier != null)
                    {
                        // Beam stops here: the barrier soaks the hit, its carrier and
                        // everything further along the beam take no damage.
                        barrier.AbsorbHit(_barrierAbsorb);
                        blockedLength = distance;
                        break;
                    }
                }

                var jumpCtx = context.CloneForNewTarget(candidate);
                // When searching from the hit unit, subsequent missiles should
                // spawn from that unit's position, not the original tower.
                if (_searchFromTarget && context.Target != null)
                    jumpCtx.CasterTransform = context.Target.transform;
                area.effect.Execute(jumpCtx);
                targetsHit++;
            }

            context.UnitsAffected += targetsHit;

            if (!string.IsNullOrEmpty(_visualSpritePath) || !string.IsNullOrEmpty(_visualSpriteSheet))
                SpawnVisual(offsetOrigin, forward2D, finalRadius, area.horizontalArc, blockedLength);
        }
    }

    // ── Optional beam/circle visual ──────────────────────────────────
    private string   _visualSpritePath   = "";
    private string   _visualSpriteSheet  = "";
    private int      _visualSpriteIndex  = -1;
    private Color    _visualColor        = Color.white;
    private float    _visualWidth        = 0.18f;
    private float    _visualLength       = 0f;
    private float    _visualFadeDuration = 0.3f;
    private float    _visualAnimFps           = 0f;
    private float    _visualRotationOffset    = 0f;

    void SpawnVisual(Vector2 origin, Vector2 forward, float radius, float arc, float clampLength = -1f)
    {
        Sprite[] allFrames = LoadVisualSheet();
        if (allFrames == null || allFrames.Length == 0)
        {
            Debug.LogWarning($"[Effect_Search_Area] Visual sprite not found — path:'{_visualSpritePath}' sheet:'{_visualSpriteSheet}'");
            return;
        }

        var go = new GameObject("[SearchAreaVisual]");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite           = allFrames[0];
        sr.color            = _visualColor;
        sr.sortingLayerName = "Units";
        sr.sortingOrder     = 9;

        // Natural world size of the sprite (accounts for pixelsPerUnit)
        var   s0       = allFrames[0];
        float naturalW = s0.rect.width  / s0.pixelsPerUnit;
        float naturalH = s0.rect.height / s0.pixelsPerUnit;

        if (arc < 30f)
        {
            // Beam: bottom edge at tower origin, extends along forward direction.
            // Divide by natural size so localScale maps to exact world units.
            float length = _visualLength > 0f ? _visualLength : radius;
            if (clampLength >= 0f) length = Mathf.Min(length, clampLength);
            float angle  = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - 90f + _visualRotationOffset;
            go.transform.position   = (Vector3)(origin + forward.normalized * length * 0.5f);
            go.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(_visualWidth / naturalW, length / naturalH, 1f);
        }
        else
        {
            // Circle: uniform scale so inscribed circle matches radius
            float diameter = radius * 2f;
            go.transform.position   = origin;
            go.transform.localScale = new Vector3(diameter / naturalW, diameter / naturalH, 1f);
        }

        go.AddComponent<VisualFader>().Setup(_visualFadeDuration, allFrames, _visualAnimFps);
    }

    Sprite LoadVisualSprite()
    {
        if (!string.IsNullOrEmpty(_visualSpritePath))
            return Resources.Load<Sprite>(_visualSpritePath);

        if (!string.IsNullOrEmpty(_visualSpriteSheet) && _visualSpriteIndex >= 0)
        {
            var sheet = Resources.LoadAll<Sprite>(_visualSpriteSheet);
            if (sheet != null && _visualSpriteIndex < sheet.Length)
                return sheet[_visualSpriteIndex];
        }

        return null;
    }

    // Returns all frames from the sheet, or a single-element array for a path sprite.
    Sprite[] LoadVisualSheet()
    {
        if (!string.IsNullOrEmpty(_visualSpriteSheet))
        {
            var sheet = Resources.LoadAll<Sprite>(_visualSpriteSheet);
            if (sheet != null && sheet.Length > 0) return sheet;
        }

        var single = LoadVisualSprite();
        return single != null ? new[] { single } : null;
    }

    [Serializable]
    class SearchAreaData
    {
        public string effectId          = "";
        public float  radius            = 4f;
        public int    maxTargets        = -1;
        public float  horizontalArc     = 360f;
        public float  startingDepth     = 0f;
        public bool   searchFromTarget      = false;
        public bool   excludePrimaryTarget  = true;
        public bool   blockedByBarriers     = false;
        public float  barrierAbsorb         = 20f;
        public string visualSpritePath  = "";
        public string visualSpriteSheet = "";
        public int    visualSpriteIndex = -1;
        public Color  visualColor        = new Color(1f, 1f, 1f, 1f);
        public float  visualWidth        = 0.18f;
        public float  visualLength       = 0f;
        public float  visualFadeDuration = 0.3f;
        public float  visualAnimFps           = 0f;
        public float  visualRotationOffset    = 0f;
    }
}
