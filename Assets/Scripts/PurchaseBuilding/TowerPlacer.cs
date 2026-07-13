using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles tower placement flow:
///   1. A UI button calls SelectTower(definitionId)
///   2. A ghost preview follows the mouse
///   3. Left-click places the tower via TowerFactory (deducts resourceCost)
///   4. Right-click / Escape cancels
///
/// Attach this to a persistent scene GameObject (e.g. the manager object).
/// Wire each tower button's OnClick to SelectTower() with the tower id string.
/// </summary>
public class TowerPlacer : MonoBehaviour
{
    public static TowerPlacer Instance { get; private set; }

    /// <summary>Frame a tower was last placed — lets click-selection ignore the placing click.</summary>
    public static int LastPlacementFrame { get; private set; } = -1;

    [Header("Ghost preview")]
    [Tooltip("Opacity of the ghost preview sprite while placing.")]
    [Range(0.1f, 1f)]
    public float ghostAlpha = 0.5f;

    [Header("Placement zones")]
    [Tooltip("Painted placement zones asset. If null, placement is allowed anywhere.")]
    public PlacementZones placementZones;

    [Header("Rotation")]
    public float rotateSpeed = 120f;

    // ── State ─────────────────────────────────────────────────────────
    private string          _selectedId;
    private TowerDefinition _selectedDef;
    private GameObject      _ghost;
    private LineRenderer    _footprintCircle;
    private float           _footprintRadius;
    private float           _ghostRotation;
    public bool             IsPlacing => !string.IsNullOrEmpty(_selectedId);

    // Pair placement ("pair" placementMode): first click plants post A,
    // second click plants post B and builds the tower.
    private const float MIN_PAIR_SPAN = 1f;
    private bool         _hasPairFirst;
    private Vector2      _pairFirst;
    private GameObject   _pairMarker;
    private LineRenderer _pairPreviewLine;
    private bool IsPairMode => _selectedDef != null && _selectedDef.placementMode == "pair";

    // Tracks whether the free-first-tower modifiers have been consumed this level
    private bool _freeBasicTowerUsed;
    private bool _freeIncomeTowerUsed;
    public void ResetFreeTowerGrants() => _freeBasicTowerUsed = _freeIncomeTowerUsed = false;

    // ── Lifecycle ─────────────────────────────────────────────────────


    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (!IsPlacing) return;

        Vector2 worldPos = GetMouseWorldPos();

        // Rotate ghost with Q/E (pair towers orient via their two posts instead)
        if (!IsPairMode)
        {
            if (Input.GetKey(KeyCode.Q)) _ghostRotation += rotateSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) _ghostRotation -= rotateSpeed * Time.deltaTime;
        }

        // Move + orient ghost
        if (_ghost != null)
        {
            _ghost.transform.position = worldPos;

            if (IsPairMode && _hasPairFirst)
            {
                // Preview the final fence orientation: marker (post A) and ghost
                // (post B) turn their right sides toward each other, same lean
                // FenceLine.SetEndpoint applies on placement.
                Vector2 dir = worldPos - _pairFirst;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    _ghost.transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f + FenceLine.PostAngleOffset);
                    if (_pairMarker != null)
                        _pairMarker.transform.rotation = Quaternion.Euler(0f, 0f, angle + FenceLine.PostAngleOffset);
                }
            }
            else
                _ghost.transform.rotation = Quaternion.Euler(0f, 0f, _ghostRotation);
        }

        // Update footprint circle color: red = blocked, green = clear
        bool blocked = IsSpotBlocked(worldPos, _footprintRadius);
        if (IsPairMode && _hasPairFirst && !IsSpanValid(worldPos)) blocked = true;
        if (_footprintCircle != null)
        {
            Color fc = blocked ? new Color(1f, 0.15f, 0.15f, 0.7f) : new Color(0.15f, 1f, 0.15f, 0.7f);
            _footprintCircle.startColor = fc;
            _footprintCircle.endColor   = fc;
        }

        // Pair preview line: post A → mouse, tinted by validity
        if (_pairPreviewLine != null)
        {
            _pairPreviewLine.SetPosition(0, new Vector3(_pairFirst.x, _pairFirst.y, 0f));
            _pairPreviewLine.SetPosition(1, new Vector3(worldPos.x, worldPos.y, 0f));
            Color lc = blocked ? new Color(1f, 0.15f, 0.15f, 0.5f) : new Color(0.15f, 1f, 0.15f, 0.5f);
            _pairPreviewLine.startColor = lc;
            _pairPreviewLine.endColor   = lc;
        }

        // Place on left-click (ignore clicks on UI)
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            if (IsPairMode) HandlePairClick(worldPos);
            else            PlaceTower(worldPos);
            return;
        }

        // Cancel on right-click or Escape
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            Cancel();
    }

    // ── Public API (wire to UI buttons) ──────────────────────────────

    /// <summary>
    /// Call this from a UI button's OnClick event.
    /// E.g. SelectTower("basic_tower") or SelectTower("income_tower")
    /// </summary>
    public void SelectTower(string definitionId)
    {
        Cancel(); // clear any existing selection first

        if (TowerDefinitionLibrary.Instance == null ||
            !TowerDefinitionLibrary.Instance.TryGet(definitionId, out var def))
        {
            Debug.LogWarning($"[TowerPlacer] Unknown tower id '{definitionId}'.");
            return;
        }

        _selectedId  = definitionId;
        _selectedDef = def;
        SpawnGhost(definitionId);
    }

    public void Cancel()
    {
        _selectedId      = null;
        _selectedDef     = null;
        _footprintCircle = null;
        _ghostRotation   = 0f;
        _hasPairFirst    = false;
        _pairPreviewLine = null;
        if (_ghost      != null) { Destroy(_ghost);      _ghost      = null; }
        if (_pairMarker != null) { Destroy(_pairMarker); _pairMarker = null; }
    }

    // ── Internal ──────────────────────────────────────────────────────

    void PlaceTower(Vector2 worldPos)
    {
        if (TowerDefinitionLibrary.Instance == null ||
            !TowerDefinitionLibrary.Instance.TryGet(_selectedId, out var def))
        {
            Cancel(); return;
        }

        // Cost check (free-first-tower modifiers bypass the cost)
        bool freeThisPlace =
            (!_freeBasicTowerUsed  && _selectedId == "basic_tower"
                && ModifierSelection.HasEffect("FreeFirstBasicTower")) ||
            (!_freeIncomeTowerUsed && _selectedId == "income_tower"
                && ModifierSelection.HasEffect("FreeFirstIncomeTower"));

        var rm = ResourceManagerScript.Instance;
        if (!freeThisPlace && rm != null && rm.resourceOne < def.resourceCost)
        {
            Debug.Log($"[TowerPlacer] Not enough resources. Need {def.resourceCost}, have {rm.resourceOne}.");
            return; // keep placement mode active so player can wait / rethink
        }

        float checkRadius = def.placementRadius > 0f ? def.placementRadius : 0.4f;

        // Zone + overlap check
        if (IsSpotBlocked(worldPos, checkRadius))
        {
            Debug.Log("[TowerPlacer] Can't place here — outside placement zones or too close to another tower.");
            return;
        }

        // Tower count cap
        if (IsTowerCapReached()) return;

        // Unique towers: only one may exist at a time
        if (UniqueAlreadyPlaced(def)) return;

        // Build real tower
        var go = TowerFactory.Instance.Build(_selectedId, worldPos, _ghostRotation);
        if (go == null) { Cancel(); return; }
        LastPlacementFrame = Time.frameCount;

        // Tower-specific place sound wins; generic event is the fallback
        if (!string.IsNullOrEmpty(def.placeSoundId))
            AudioManager.Play(def.placeSoundId);
        else
            AudioManager.PlayEvent("tower_place");

        // Deduct cost
        if (freeThisPlace)
        {
            if (_selectedId == "basic_tower") _freeBasicTowerUsed  = true;
            else                              _freeIncomeTowerUsed = true;
        }
        else if (rm != null)
            rm.ChangeResourceOne(-def.resourceCost);

        ObjectiveTracker.NotifyBuild(_selectedId);
        RunStats.NotifyTowerBuilt(def.balanceType);

        // Exit placement mode — one click, one tower
        Cancel();
    }

    // ── Pair placement ────────────────────────────────────────────────

    void HandlePairClick(Vector2 worldPos)
    {
        if (_selectedDef == null) { Cancel(); return; }
        float checkRadius = _selectedDef.placementRadius > 0f ? _selectedDef.placementRadius : 0.4f;

        if (IsSpotBlocked(worldPos, checkRadius))
        {
            Debug.Log("[TowerPlacer] Can't place a post here — outside placement zones or too close to another tower.");
            return;
        }

        // ── First click: plant post A ─────────────────────────────
        if (!_hasPairFirst)
        {
            // Fail early on cost so the player doesn't line up a fence they can't afford
            var rmEarly = ResourceManagerScript.Instance;
            if (rmEarly != null && rmEarly.resourceOne < _selectedDef.resourceCost)
            {
                Debug.Log($"[TowerPlacer] Not enough resources. Need {_selectedDef.resourceCost}, have {rmEarly.resourceOne}.");
                return;
            }
            if (IsTowerCapReached()) return;
            if (UniqueAlreadyPlaced(_selectedDef)) return;

            _hasPairFirst = true;
            _pairFirst    = worldPos;
            SpawnPairMarker(worldPos);
            CreatePairPreviewLine();
            return;
        }

        // ── Second click: plant post B and build ──────────────────
        if (!IsSpanValid(worldPos))
        {
            float span = Vector2.Distance(_pairFirst, worldPos);
            Debug.Log($"[TowerPlacer] Posts must be between {MIN_PAIR_SPAN} and {_selectedDef.pairMaxSpan} apart (span {span:0.0}).");
            return;
        }

        var rm = ResourceManagerScript.Instance;
        if (rm != null && rm.resourceOne < _selectedDef.resourceCost)
        {
            Debug.Log($"[TowerPlacer] Not enough resources. Need {_selectedDef.resourceCost}, have {rm.resourceOne}.");
            return;
        }
        if (IsTowerCapReached()) return;

        var go = TowerFactory.Instance.Build(_selectedId, _pairFirst, 0f);
        if (go == null) { Cancel(); return; }
        LastPlacementFrame = Time.frameCount;

        var fence = go.GetComponent<FenceLine>();
        if (fence != null) fence.SetEndpoint(worldPos);

        if (!string.IsNullOrEmpty(_selectedDef.placeSoundId))
            AudioManager.Play(_selectedDef.placeSoundId);
        else
            AudioManager.PlayEvent("tower_place");

        if (rm != null) rm.ChangeResourceOne(-_selectedDef.resourceCost);

        ObjectiveTracker.NotifyBuild(_selectedId);
        RunStats.NotifyTowerBuilt(_selectedDef.balanceType);

        Cancel();
    }

    /// <summary>True (and logs) when def.unique and a live copy already stands.</summary>
    static bool UniqueAlreadyPlaced(TowerDefinition def)
    {
        if (def == null || !def.unique) return false;
        foreach (var t in TowerInfo.All)
        {
            if (t == null || t.isGhost || t.definitionId != def.id) continue;
            Debug.Log($"[TowerPlacer] Only one {def.displayName} can exist at a time.");
            return true;
        }
        return false;
    }

    void SpawnPairMarker(Vector2 worldPos)
    {
        _pairMarker = new GameObject("[PairMarker]");
        _pairMarker.transform.position   = worldPos;
        _pairMarker.transform.localScale = _ghost != null ? _ghost.transform.localScale : Vector3.one;

        var ghostSr = _ghost != null ? _ghost.GetComponent<SpriteRenderer>() : null;
        if (ghostSr != null)
        {
            var sr              = _pairMarker.AddComponent<SpriteRenderer>();
            sr.sprite           = ghostSr.sprite;
            sr.color            = ghostSr.color;
            sr.sortingLayerName = ghostSr.sortingLayerName;
            sr.sortingOrder     = ghostSr.sortingOrder;
        }
    }

    void CreatePairPreviewLine()
    {
        var go = new GameObject("[PairPreviewLine]");
        go.transform.SetParent(_pairMarker != null ? _pairMarker.transform : null, true);

        _pairPreviewLine                  = go.AddComponent<LineRenderer>();
        _pairPreviewLine.positionCount    = 2;
        _pairPreviewLine.useWorldSpace    = true;
        _pairPreviewLine.startWidth       = 0.08f;
        _pairPreviewLine.endWidth         = 0.08f;
        _pairPreviewLine.sortingLayerName = "Units";
        _pairPreviewLine.sortingOrder     = 21;
        _pairPreviewLine.material         = new Material(Shader.Find("Sprites/Default"));
    }

    bool IsSpanValid(Vector2 candidateB)
    {
        float span = Vector2.Distance(_pairFirst, candidateB);
        float max  = _selectedDef != null && _selectedDef.pairMaxSpan > 0f ? _selectedDef.pairMaxSpan : 4f;
        return span >= MIN_PAIR_SPAN && span <= max;
    }

    // ── Shared validation ─────────────────────────────────────────────

    /// <summary>Zone + tower-overlap check. GetComponentInParent so child body
    /// colliders (e.g. a fence's post B) also block placement.</summary>
    bool IsSpotBlocked(Vector2 worldPos, float radius)
    {
        if (placementZones != null && !placementZones.Overlaps(worldPos, radius))
            return true;

        var nearby = Physics2D.OverlapCircleAll(worldPos, radius);
        foreach (var col in nearby)
            if (!col.isTrigger && col.GetComponentInParent<TowerInfo>() != null)
                return true;
        return false;
    }

    bool IsTowerCapReached()
    {
        var bm = BalanceManager.Instance;
        if (bm != null && bm.TowerCount >= bm.MaxTowers)
        {
            Debug.Log($"[TowerPlacer] Tower cap reached ({bm.TowerCount}/{bm.MaxTowers}). Diversify your balance to expand.");
            return true;
        }
        return false;
    }

    void SpawnGhost(string definitionId)
    {
        if (TowerFactory.Instance == null) return;

        // Build off-screen as ghost preview (isGhost=true so buffs are skipped inside factory)
        _ghost = TowerFactory.Instance.Build(definitionId, Vector3.one * 9999f, 0f, isGhost: true);
        if (_ghost == null) return;
        _ghost.name = $"[Ghost] {_ghost.name}";

        // Disable all logic components — ghost is visual only
        // (SpriteRenderer is a Renderer, not MonoBehaviour, so it's unaffected here)
        foreach (var mb in _ghost.GetComponents<MonoBehaviour>())
            mb.enabled = false;

        // Make semi-transparent
        var sr = _ghost.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            var c = sr.color;
            c.a = ghostAlpha;
            sr.color = c;
        }

        // No collider on ghost
        foreach (var col in _ghost.GetComponents<Collider2D>())
            col.enabled = false;

        // Draw range circle using ability range (the collider is still at placeholder radius 1)
        if (TowerDefinitionLibrary.Instance.TryGet(definitionId, out var ghostDef))
        {
            float circleRange = ghostDef.range;
            if (!string.IsNullOrEmpty(ghostDef.fireAbilityId) &&
                AbilityLibrary.Instance != null &&
                AbilityLibrary.Instance.TryGet(ghostDef.fireAbilityId, out var ghostAbility) &&
                ghostAbility.range > 0f)
                circleRange = ghostAbility.range;

            if (circleRange > 0f)
                AddRangeCircle(_ghost, circleRange);

            // Footprint debug circle — shows placement collision area, green/red feedback
            _footprintRadius = ghostDef.placementRadius > 0f ? ghostDef.placementRadius : 0.4f;
            _footprintCircle = AddFootprintCircle(_ghost, _footprintRadius);
        }
    }

    static LineRenderer AddFootprintCircle(GameObject go, float radius)
    {
        const int   SEGMENTS = 48;
        const float LINE_W   = 0.05f;

        float scale       = Mathf.Max(0.01f, go.transform.localScale.x);
        float localRadius = radius / scale;

        var child            = new GameObject("[FootprintCircle]");
        child.transform.SetParent(go.transform, false);
        var lr               = child.AddComponent<LineRenderer>();
        lr.loop              = true;
        lr.positionCount     = SEGMENTS;
        lr.startWidth        = LINE_W;
        lr.endWidth          = LINE_W;
        lr.useWorldSpace     = false;
        lr.sortingLayerName  = "Units";
        lr.sortingOrder      = 21;
        lr.material          = new Material(Shader.Find("Sprites/Default"));

        for (int i = 0; i < SEGMENTS; i++)
        {
            float angle = i / (float)SEGMENTS * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * localRadius,
                                          Mathf.Sin(angle) * localRadius, 0f));
        }
        return lr;
    }

    static void AddRangeCircle(GameObject go, float radius)
    {
        const int   SEGMENTS = 64;
        const float LINE_W   = 0.04f;

        // Compensate for tower scale so local-space positions match world-space range
        float scale       = Mathf.Max(0.01f, go.transform.localScale.x);
        float localRadius = radius / scale;

        var lr               = go.AddComponent<LineRenderer>();
        lr.loop              = true;
        lr.positionCount     = SEGMENTS;
        lr.startWidth        = LINE_W;
        lr.endWidth          = LINE_W;
        lr.useWorldSpace     = false;
        lr.sortingLayerName  = "Units";
        lr.sortingOrder      = 20;

        var mat       = new Material(Shader.Find("Sprites/Default"));
        lr.material   = mat;
        lr.startColor = new Color(1f, 1f, 1f, 0.35f);
        lr.endColor   = new Color(1f, 1f, 1f, 0.35f);

        for (int i = 0; i < SEGMENTS; i++)
        {
            float angle = i / (float)SEGMENTS * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * localRadius,
                                          Mathf.Sin(angle) * localRadius, 0f));
        }
    }

    static Vector2 GetMouseWorldPos()
    {
        var cam = Camera.main;
        if (cam == null) return Vector2.zero;
        Vector3 mp = Input.mousePosition;
        mp.z = Mathf.Abs(cam.transform.position.z);
        return cam.ScreenToWorldPoint(mp);
    }
}
