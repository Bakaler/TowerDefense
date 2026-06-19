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

    // ── State ─────────────────────────────────────────────────────────
    private string      _selectedId;
    private GameObject  _ghost;
    public bool         IsPlacing => !string.IsNullOrEmpty(_selectedId);

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

        // Move ghost
        if (_ghost != null)
            _ghost.transform.position = worldPos;

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
        _selectedId = null;
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

        // Build real tower
        var go = TowerFactory.Instance.Build(_selectedId, worldPos);
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
