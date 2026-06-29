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
    private string        _selectedId;
    private GameObject    _ghost;
    private LineRenderer  _footprintCircle;
    private float         _footprintRadius;
    private float         _ghostRotation;
    public bool           IsPlacing => !string.IsNullOrEmpty(_selectedId);

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

        // Rotate ghost with Q/E
        if (Input.GetKey(KeyCode.Q)) _ghostRotation += rotateSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) _ghostRotation -= rotateSpeed * Time.deltaTime;

        // Move + orient ghost
        if (_ghost != null)
        {
            _ghost.transform.position = worldPos;
            _ghost.transform.rotation = Quaternion.Euler(0f, 0f, _ghostRotation);
        }

        // Update footprint circle color: red = blocked, green = clear
        if (_footprintCircle != null)
        {
            bool blocked = placementZones != null && !placementZones.Overlaps(worldPos, _footprintRadius);
            if (!blocked)
            {
                var nearby = Physics2D.OverlapCircleAll(worldPos, _footprintRadius);
                foreach (var col in nearby)
                    if (!col.isTrigger && col.GetComponent<TowerInfo>() != null) { blocked = true; break; }
            }
            Color fc = blocked ? new Color(1f, 0.15f, 0.15f, 0.7f) : new Color(0.15f, 1f, 0.15f, 0.7f);
            _footprintCircle.startColor = fc;
            _footprintCircle.endColor   = fc;
        }

        // Place on left-click (ignore clicks on UI)
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            PlaceTower(worldPos);
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
            !TowerDefinitionLibrary.Instance.TryGet(definitionId, out _))
        {
            Debug.LogWarning($"[TowerPlacer] Unknown tower id '{definitionId}'.");
            return;
        }

        _selectedId = definitionId;
        SpawnGhost(definitionId);
    }

    public void Cancel()
    {
        _selectedId      = null;
        _footprintCircle = null;
        _ghostRotation   = 0f;
        if (_ghost != null) { Destroy(_ghost); _ghost = null; }
    }

    // ── Internal ──────────────────────────────────────────────────────

    void PlaceTower(Vector2 worldPos)
    {
        if (TowerDefinitionLibrary.Instance == null ||
            !TowerDefinitionLibrary.Instance.TryGet(_selectedId, out var def))
        {
            Cancel(); return;
        }

        // Cost check
        var rm = FindFirstObjectByType<ResourceManagerScript>();
        if (rm != null && rm.resourceOne < def.resourceCost)
        {
            Debug.Log($"[TowerPlacer] Not enough resources. Need {def.resourceCost}, have {rm.resourceOne}.");
            return; // keep placement mode active so player can wait / rethink
        }

        float checkRadius = def.placementRadius > 0f ? def.placementRadius : 0.4f;

        // Zone check — must be within a painted placement zone (if asset assigned)
        if (placementZones != null && !placementZones.Overlaps(worldPos, checkRadius))
        {
            Debug.Log("[TowerPlacer] Can't place here — outside painted placement zones.");
            return;
        }

        // Tower count cap
        var bm = BalanceManager.Instance;
        if (bm != null && bm.TowerCount >= bm.MaxTowers)
        {
            Debug.Log($"[TowerPlacer] Tower cap reached ({bm.TowerCount}/{bm.MaxTowers}). Diversify your balance to expand.");
            return;
        }

        // Overlap check — no other tower body within footprint
        var nearby = Physics2D.OverlapCircleAll(worldPos, checkRadius);
        foreach (var col in nearby)
        {
            if (!col.isTrigger && col.GetComponent<TowerInfo>() != null)
            {
                Debug.Log("[TowerPlacer] Can't place here — too close to another tower.");
                return;
            }
        }

        // Build real tower
        var go = TowerFactory.Instance.Build(_selectedId, worldPos, _ghostRotation);
        if (go == null) { Cancel(); return; }

        // Deduct cost
        if (rm != null)
            rm.ChangeResourceOne(-def.resourceCost);

        // Exit placement mode — one click, one tower
        Cancel();
    }

    void SpawnGhost(string definitionId)
    {
        if (TowerFactory.Instance == null) return;

        // Build off-screen as ghost preview, then strip it down to visuals only
        _ghost = TowerFactory.Instance.Build(definitionId, Vector3.one * 9999f);
        if (_ghost == null) return;
        _ghost.name = $"[Ghost] {_ghost.name}";

        // Mark as ghost so balance counts ignore it
        var ghostInfo = _ghost.GetComponent<TowerInfo>();
        if (ghostInfo != null) ghostInfo.isGhost = true;

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
