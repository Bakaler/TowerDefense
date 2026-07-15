using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Yellow bottom panel — the primary interaction hub. Thin coordinator that
/// owns the panel root, header, selection state, and mode switching; the mode
/// bodies live in their own view classes (Assets/Scripts/UI/InfoPanel/):
///   InfoPanelTowerView    — tower stats/actions + per-tower research sub-body
///   InfoPanelEnemyView    — enemy stats + behavior status grid
///   InfoPanelResearchView — tier (T2–T5) unlock buttons
/// Width spans the full screen minus the right column (1620px), height = INFO_H (300px).
/// </summary>
public class HUDInfoPanel : MonoBehaviour
{
    // ── Modes ──────────────────────────────────────────────────────────
    enum Mode { None, Tower, TowerResearch, Enemy, TierResearch }
    Mode _mode = Mode.None;

    // ── Root / header ──────────────────────────────────────────────────
    GameObject _panel;
    Text       _headerTitle;

    // ── Views ──────────────────────────────────────────────────────────
    InfoPanelTowerView    _towerView;
    InfoPanelEnemyView    _enemyView;
    InfoPanelResearchView _tierResView;

    // ── Selection ──────────────────────────────────────────────────────
    TowerInfo _selectedTower;
    public UnitManager SelectedEnemy { get; private set; }

    /// <summary>Raw selection for the views (unlike GetSelectedTower, not mode-gated).</summary>
    internal TowerInfo SelectedTower => _selectedTower;

    const float W     = InfoPanelLayout.W;
    const float H     = InfoPanelLayout.H;
    const float HDR_H = InfoPanelLayout.HDR_H;
    const float PAD   = InfoPanelLayout.PAD;

    // ── Build ──────────────────────────────────────────────────────────

    public void Build(GameObject canvasRoot)
    {
        _panel = new GameObject("InfoPanel");
        _panel.transform.SetParent(canvasRoot.transform, false);
        var rt       = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(W, H);
        _panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.06f, 0.95f);

        BuildHeader();
        _towerView   = new InfoPanelTowerView(this, _panel);
        _enemyView   = new InfoPanelEnemyView(this, _panel);
        _tierResView = new InfoPanelResearchView(_panel);

        SetMode(Mode.None);
        _panel.SetActive(false);
    }

    void BuildHeader()
    {
        var hdr = HUDHelpers.MakeRect("Header", _panel, 0f, H - HDR_H, W, HDR_H);
        hdr.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.04f, 1f);

        _headerTitle = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("Title", hdr, PAD, 0f, W - HDR_H * 3f, HDR_H),
            "", new Color(1f, 0.85f, 0.3f), 17, bold: true);

        // Mode toggle buttons (top right of header)
        float bW = 90f, bH = HDR_H - 6f, bY = 3f;
        float bX = W - PAD - bW * 3f - 6f * 2f;

        var (tb, _) = HUDHelpers.MakeBtn(hdr, "TabTower",  bX,            bY, bW, bH, new Color(0.18f,0.35f,0.18f,1f), "TOWER",    11, true);
        var (eb, _) = HUDHelpers.MakeBtn(hdr, "TabEnemy",  bX + bW + 6f,  bY, bW, bH, new Color(0.35f,0.14f,0.14f,1f), "ENEMY",    11, true);
        var (rb, _) = HUDHelpers.MakeBtn(hdr, "TabResrch", bX + bW*2+12f, bY, bW, bH, new Color(0.12f,0.25f,0.55f,1f), "RESEARCH", 11, true);
        tb.onClick.AddListener(() => { if (_selectedTower != null) SetMode(Mode.Tower); });
        eb.onClick.AddListener(() => { if (SelectedEnemy  != null) SetMode(Mode.Enemy); });
        rb.onClick.AddListener(() => SetMode(Mode.TierResearch));

        // Close button (rightmost)
        var (cb, _) = HUDHelpers.MakeBtn(hdr, "Close", W - HDR_H, 0f, HDR_H, HDR_H, new Color(0.5f,0.12f,0.12f,1f), "×", 22, true);
        cb.onClick.AddListener(Hide);
    }

    // ── Mode switching ─────────────────────────────────────────────────

    void SetMode(Mode mode)
    {
        _mode = mode;
        CloseAllPopups();
        _towerView?.SetTowerActive(mode == Mode.Tower);
        _towerView?.SetResearchActive(mode == Mode.TowerResearch);
        _enemyView?.SetActive(mode == Mode.Enemy);
        _tierResView?.SetActive(mode == Mode.TierResearch);

        switch (mode)
        {
            case Mode.Tower:        SetHeaderTitle("TOWER INFO");    break;
            case Mode.TowerResearch:SetHeaderTitle("TOWER RESEARCH");break;
            case Mode.Enemy:        /* header title set by the enemy view */ break;
            case Mode.TierResearch: SetHeaderTitle("RESEARCH");
                                    _tierResView?.Refresh();         break;
            case Mode.None:         SetHeaderTitle("");              break;
        }

        if (mode != Mode.None)
            _panel?.SetActive(true);
    }

    internal void SetHeaderTitle(string title)
    {
        if (_headerTitle != null) _headerTitle.text = title;
    }

    /// <summary>Called by the tower view's RESEARCH button.</summary>
    internal void ShowTowerResearchMode()
    {
        SetMode(Mode.TowerResearch);
        _towerView?.RefreshResearchBody();
    }

    /// <summary>Called by the tower research sub-body's back button.</summary>
    internal void ShowTowerMode() => SetMode(Mode.Tower);

    internal void CloseAllPopups()
    {
        _towerView?.ClosePopups();
        _enemyView?.HideTooltip();
    }

    // ── Public API ─────────────────────────────────────────────────────

    public void ShowTower(TowerInfo info)
    {
        DeselectCurrentTower();
        if (info == null) { Hide(); return; }
        AudioManager.PlayEvent("select");
        SelectedEnemy = null;
        _selectedTower = info;
        info.GetComponent<SniperZone>()?.SetSelected(true);
        info.GetComponent<ShotgunOrienter>()?.SetSelected(true);
        _towerView?.Refresh(info);
        SetMode(Mode.Tower);
    }

    public void ShowEnemy(UnitManager unit)
    {
        if (unit == null || !unit.isAlive) return;
        DeselectCurrentTower();
        _selectedTower = null;
        SelectedEnemy = unit;
        _enemyView?.Refresh(unit);
        SetMode(Mode.Enemy);
    }

    public void ShowResearch() => SetMode(Mode.TierResearch);

    public void Hide()
    {
        DeselectCurrentTower();
        CloseAllPopups();
        SelectedEnemy = null;
        _mode = Mode.None;
        _towerView?.SetTowerActive(false);
        _towerView?.SetResearchActive(false);
        _enemyView?.SetActive(false);
        _tierResView?.SetActive(false);
        _panel?.SetActive(false);
    }

    public void Reset()
    {
        Hide();
        _panel?.SetActive(false);
    }

    public void OnTowerKill(TowerInfo info)
    {
        if (info == _selectedTower && _mode == Mode.Tower)
            _towerView?.Refresh(info);
    }

    public TowerInfo GetSelectedTower() => _mode == Mode.Tower ? _selectedTower : null;

    void DeselectCurrentTower()
    {
        if (_selectedTower == null) return;
        _selectedTower.GetComponent<SniperZone>()?.SetSelected(false);
        _selectedTower.GetComponent<ShotgunOrienter>()?.SetSelected(false);
        _selectedTower = null;
    }

    // ── Update ─────────────────────────────────────────────────────────

    void Update()
    {
        if (_mode == Mode.Tower && _selectedTower != null)
            _towerView?.Refresh(_selectedTower);

        if (_mode == Mode.Enemy)
        {
            if (SelectedEnemy != null && !SelectedEnemy.isAlive)
            {
                SelectedEnemy = null;
                Hide();
            }
            else
            {
                _enemyView?.Refresh(SelectedEnemy);
            }
        }

        if (_mode == Mode.TierResearch)
            _tierResView?.Refresh();
    }
}
