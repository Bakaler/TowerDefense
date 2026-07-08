using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Yellow bottom panel — the primary interaction hub.
/// Modes: Tower info · Enemy info · Tier research (T2-T5 unlock) · Tower research upgrades.
/// Width spans the full screen minus the right column (1620px), height = INFO_H (300px).
/// </summary>
public class HUDInfoPanel : MonoBehaviour
{
    // ── Modes ──────────────────────────────────────────────────────────
    enum Mode { None, Tower, TowerResearch, Enemy, TierResearch }
    Mode _mode = Mode.None;

    // ── Root ───────────────────────────────────────────────────────────
    GameObject _panel;
    GameObject _towerBody;
    GameObject _towerResBody;
    GameObject _enemyBody;
    GameObject _tierResBody;

    // ── Header ─────────────────────────────────────────────────────────
    Text   _headerTitle;

    // ── Tower mode ─────────────────────────────────────────────────────
    Text    _tpBalance;
    Text    _tpBalanceDist;
    Text    _tpDamage;
    Text    _tpShieldBonus;
    Text    _tpFireRate;
    Text    _tpKills;
    Text    _tpAura;
    Button  _tpUpgradeBtn;
    Text    _tpUpgradeBtnLabel;
    Button  _tpSellBtn;
    Text    _tpSellBtnLabel;
    Button  _tpMoveZoneBtn;
    Text    _tpMoveZoneLabel;
    Button[] _tpTargetBtns;
    Image[]  _tpTargetImgs;
    TowerInfo _selectedTower;

    // ── Enemy mode ─────────────────────────────────────────────────────
    Text _epName, _epHp, _epShield, _epSpeed, _epDeathBlow;
    Text _epArmor, _epResistance, _epFortitude, _epDescription;
    public UnitManager SelectedEnemy { get; private set; }

    // ── Tier research mode ─────────────────────────────────────────────
    Button _resT2Btn, _resT3Btn, _resT4Btn, _resT5Btn;
    Text   _resT2Lbl, _resT3Lbl, _resT4Lbl, _resT5Lbl;

    // ── Tower research sub-panel ───────────────────────────────────────
    List<(Button btn, Text lbl, ResearchDefinition def)> _trButtons = new();

    ResourceManagerScript _rm;

    // ── Layout constants ───────────────────────────────────────────────
    const float W      = HUDHelpers.INFO_W;
    const float H      = HUDHelpers.INFO_H;
    const float HDR_H  = 36f;
    const float PAD    = 14f;
    const float BODY_H = H - HDR_H;

    // 4 content columns
    const float COL_GAP = 12f;
    const float COL_W   = (W - PAD * 2f - COL_GAP * 3f) / 4f;
    const float C0      = PAD;
    const float C1      = C0 + COL_W + COL_GAP;
    const float C2      = C1 + COL_W + COL_GAP;
    const float C3      = C2 + COL_W + COL_GAP;

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
        BuildTowerBody();
        BuildTowerResBody();
        BuildEnemyBody();
        BuildTierResBody();

        SetMode(Mode.None);
        _panel.SetActive(false);
    }

    // ── Header ─────────────────────────────────────────────────────────

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

    // ── Tower body ─────────────────────────────────────────────────────

    void BuildTowerBody()
    {
        _towerBody = new GameObject("TowerBody");
        _towerBody.transform.SetParent(_panel.transform, false);
        var rt = _towerBody.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(W, H);

        const float ROW  = 26f;
        const float BTNH = 34f;
        const float TPAD = 8f;   // tighter internal padding
        float topY = H - HDR_H - TPAD - ROW;

        float y = topY;
        _tpBalance     = HUDHelpers.MakeText(HUDHelpers.MakeRect("Balance",     _towerBody, C0, y, COL_W, ROW), "", new Color(0.6f, 0.9f, 0.6f),   14); y -= ROW;
        _tpBalanceDist = HUDHelpers.MakeText(HUDHelpers.MakeRect("BalanceDist", _towerBody, C0, y, COL_W, ROW), "", new Color(0.75f, 0.75f, 0.85f), 13); y -= ROW;
        _tpKills       = HUDHelpers.MakeText(HUDHelpers.MakeRect("Kills",       _towerBody, C0, y, COL_W, ROW), "", new Color(0.95f, 0.5f, 0.5f),   13);
        _tpAura        = HUDHelpers.MakeText(HUDHelpers.MakeRect("Aura",        _towerBody, C0, y - ROW, COL_W, ROW), "", new Color(0.3f, 1f, 0.45f), 11);

        // C1: Damage / FireRate / MoveZone
        y = topY;
        _tpDamage      = HUDHelpers.MakeText(HUDHelpers.MakeRect("Damage",      _towerBody, C1, y, COL_W, ROW), "", new Color(0.9f, 0.9f, 0.9f), 14); y -= ROW;
        _tpShieldBonus = HUDHelpers.MakeText(HUDHelpers.MakeRect("ShieldBonus", _towerBody, C1, y, COL_W, ROW), "", new Color(0.35f, 0.75f, 1f), 13); y -= ROW;
        _tpFireRate    = HUDHelpers.MakeText(HUDHelpers.MakeRect("FireRate",    _towerBody, C1, y, COL_W, ROW), "", new Color(0.9f, 0.9f, 0.9f), 14); y -= ROW + 4f;

        var mzGO  = HUDHelpers.MakeRect("MoveZoneBtn", _towerBody, C1, y - 4f, COL_W, 30f);
        var mzImg = mzGO.AddComponent<Image>(); mzImg.color = new Color(0.2f, 0.45f, 0.65f, 1f);
        _tpMoveZoneBtn = mzGO.AddComponent<Button>(); _tpMoveZoneBtn.targetGraphic = mzImg;
        _tpMoveZoneLabel = HUDHelpers.MakeText(HUDHelpers.MakeRect("L", mzGO, 0f, 0f, COL_W, 30f), "MOVE ZONE", Color.white, 11, bold: true);
        _tpMoveZoneLabel.alignment = TextAnchor.MiddleCenter;
        _tpMoveZoneBtn.onClick.AddListener(OnMoveZoneClicked);
        mzGO.SetActive(false);

        // C2: Upgrade / Research / Sell buttons (stacked from top)
        y = topY;
        var (upgBtn, upgLbl) = HUDHelpers.MakeBtn(_towerBody, "UpgradeBtn", C2, y - BTNH + ROW, COL_W, BTNH, new Color(0.15f,0.55f,0.25f,1f), "UPGRADE", 12, true);
        _tpUpgradeBtn = upgBtn; _tpUpgradeBtnLabel = upgLbl;
        // Two-line label (cost + damage preview) needs to shrink to fit
        _tpUpgradeBtnLabel.resizeTextForBestFit = true;
        _tpUpgradeBtnLabel.resizeTextMinSize    = 8;
        _tpUpgradeBtnLabel.resizeTextMaxSize    = 12;
        var uc = _tpUpgradeBtn.colors; uc.highlightedColor = new Color(0.2f,0.7f,0.3f); uc.disabledColor = new Color(0.22f,0.22f,0.22f); _tpUpgradeBtn.colors = uc;
        _tpUpgradeBtn.onClick.AddListener(OnUpgradeClicked);

        float resBY = y - BTNH + ROW - BTNH - 5f;
        var (resBtn, _) = HUDHelpers.MakeBtn(_towerBody, "TwrResBtn", C2, resBY, COL_W, BTNH, new Color(0.15f,0.30f,0.60f,1f), "RESEARCH", 12, true);
        var rc = resBtn.colors; rc.highlightedColor = new Color(0.2f,0.4f,0.8f); resBtn.colors = rc;
        resBtn.onClick.AddListener(ShowTowerResBody);

        float sellBY = resBY - BTNH - 5f;
        var (sellBtn, sellLbl) = HUDHelpers.MakeBtn(_towerBody, "SellBtn", C2, sellBY, COL_W, BTNH, new Color(0.55f,0.10f,0.10f,1f), "SELL", 12, true);
        _tpSellBtn = sellBtn; _tpSellBtnLabel = sellLbl;
        var sc = _tpSellBtn.colors; sc.highlightedColor = new Color(0.75f,0.15f,0.15f); _tpSellBtn.colors = sc;
        _tpSellBtn.onClick.AddListener(OnSellClicked);

        // C3: Targeting
        float tgtBtnY = TPAD;
        HUDHelpers.MakeText(
            HUDHelpers.MakeRect("TargetLabel", _towerBody, C3, tgtBtnY + 30f + 5f, COL_W, 20f),
            "TARGET", new Color(0.6f, 0.6f, 0.7f), 10, bold: true);

        string[] modes = { "Furthest", "Closest", "Lowest" };
        float mBtnW = (COL_W - 8f) / 3f;
        _tpTargetBtns = new Button[3];
        _tpTargetImgs = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            float xOff = C3 + i * (mBtnW + 4f);
            var bGO  = HUDHelpers.MakeRect($"Target_{modes[i]}", _towerBody, xOff, tgtBtnY, mBtnW, 30f);
            var bImg = bGO.AddComponent<Image>(); bImg.color = new Color(0.18f, 0.18f, 0.28f, 1f);
            var btn  = bGO.AddComponent<Button>(); btn.targetGraphic = bImg;
            HUDHelpers.MakeText(HUDHelpers.MakeRect("L", bGO, 0f, 0f, mBtnW, 30f), modes[i], Color.white, 10, bold: true)
                .alignment = TextAnchor.MiddleCenter;
            int idx = i;
            btn.onClick.AddListener(() => SetTargetingMode((TargetingMode)idx));
            _tpTargetBtns[i] = btn;
            _tpTargetImgs[i] = bImg;
        }
    }

    // ── Tower research sub-body ────────────────────────────────────────

    void BuildTowerResBody()
    {
        _towerResBody = new GameObject("TowerResBody");
        _towerResBody.transform.SetParent(_panel.transform, false);
        var rt = _towerResBody.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(W, H);

        var (backBtn, _) = HUDHelpers.MakeBtn(_towerResBody, "BackBtn",
            PAD, H - HDR_H - PAD - 40f, 110f, 40f,
            new Color(0.25f, 0.25f, 0.35f, 1f), "← Back", 13, bold: true);
        backBtn.onClick.AddListener(() => SetMode(Mode.Tower));
    }

    // ── Enemy body ─────────────────────────────────────────────────────

    void BuildEnemyBody()
    {
        _enemyBody = new GameObject("EnemyBody");
        _enemyBody.transform.SetParent(_panel.transform, false);
        var rt = _enemyBody.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(W, H);

        // Enemy name goes in the header title; stats fill two columns
        float colW = (W - PAD * 3f) * 0.5f;
        const float ROW = 26f;
        float y = H - HDR_H - 8f - ROW;

        _epName = null;   // shown in header title instead

        _epHp         = HUDHelpers.MakeText(HUDHelpers.MakeRect("HP",     _enemyBody, PAD,              y, colW, ROW), "", Color.white, 14);
        _epArmor      = HUDHelpers.MakeText(HUDHelpers.MakeRect("Armor",  _enemyBody, PAD + colW + PAD, y, colW, ROW), "", new Color(0.8f,0.8f,0.8f), 14); y -= ROW + 3f;
        _epShield     = HUDHelpers.MakeText(HUDHelpers.MakeRect("Shield", _enemyBody, PAD,              y, colW, ROW), "", new Color(0.35f,0.75f,1f), 14);
        _epResistance = HUDHelpers.MakeText(HUDHelpers.MakeRect("Res",    _enemyBody, PAD + colW + PAD, y, colW, ROW), "", new Color(0.4f,0.85f,1f), 14); y -= ROW + 3f;
        _epSpeed      = HUDHelpers.MakeText(HUDHelpers.MakeRect("Spd",    _enemyBody, PAD,              y, colW, ROW), "", Color.white, 14);
        _epFortitude  = HUDHelpers.MakeText(HUDHelpers.MakeRect("Fort",   _enemyBody, PAD + colW + PAD, y, colW, ROW), "", new Color(0.85f,0.6f,1f), 14); y -= ROW + 3f;
        _epDeathBlow  = HUDHelpers.MakeText(HUDHelpers.MakeRect("DB",     _enemyBody, PAD,              y, colW, ROW), "", new Color(0.95f,0.4f,0.4f), 14); y -= ROW + 8f;

        HUDHelpers.MakeRect("Div", _enemyBody, PAD, y + 4f, W - PAD * 2f, 1f)
            .AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        _epDescription = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("Desc", _enemyBody, PAD, 6f, W - PAD * 2f, y - 4f),
            "", new Color(0.75f, 0.75f, 0.75f), 12);
        _epDescription.alignment         = TextAnchor.UpperLeft;
        _epDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
        _epDescription.verticalOverflow   = VerticalWrapMode.Overflow;
    }

    // ── Tier research body ─────────────────────────────────────────────

    void BuildTierResBody()
    {
        _tierResBody = new GameObject("TierResBody");
        _tierResBody.transform.SetParent(_panel.transform, false);
        var rt = _tierResBody.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(W, H);

        // 4 tier unlock buttons side-by-side
        const float BTNH   = BODY_H - PAD * 2f;
        float btnW         = (W - PAD * 5f) / 4f;

        var tiers = new[]
        {
            (2, "Tier 2", new Color(0.12f,0.22f,0.15f), new Color(0.6f,0.9f,0.65f)),
            (3, "Tier 3", new Color(0.16f,0.12f,0.26f), new Color(0.85f,0.70f,1.0f)),
            (4, "Tier 4", new Color(0.18f,0.10f,0.28f), new Color(0.80f,0.55f,1.0f)),
            (5, "Tier 5", new Color(0.28f,0.06f,0.14f), new Color(1.0f,0.55f,0.75f)),
        };

        Button[] btns = new Button[4];
        Text[]   lbls = new Text[4];

        for (int i = 0; i < 4; i++)
        {
            var (tier, title, bg, tc) = tiers[i];
            float x = PAD + i * (btnW + PAD);
            var (btn, lbl) = HUDHelpers.MakeBtn(_tierResBody, $"T{tier}Btn", x, PAD, btnW, BTNH, bg, "", 15);
            lbl.color     = tc;
            lbl.alignment = TextAnchor.MiddleCenter;
            btns[i] = btn;
            lbls[i] = lbl;

            var hc = btn.colors;
            hc.disabledColor = new Color(0.18f, 0.18f, 0.20f);
            btn.colors = hc;
        }

        _resT2Btn = btns[0]; _resT2Lbl = lbls[0];
        _resT3Btn = btns[1]; _resT3Lbl = lbls[1];
        _resT4Btn = btns[2]; _resT4Lbl = lbls[2];
        _resT5Btn = btns[3]; _resT5Lbl = lbls[3];

        _resT2Btn.onClick.AddListener(() => { if (TechManager.Instance?.TryUnlockT2() == true) HUDTierSelector.Instance?.SyncWithShop(jumpToTier: 2); RefreshTierRes(); });
        _resT3Btn.onClick.AddListener(() => { if (TechManager.Instance?.TryUnlockT3() == true) HUDTierSelector.Instance?.SyncWithShop(jumpToTier: 3); RefreshTierRes(); });
        _resT4Btn.onClick.AddListener(() => { if (TechManager.Instance?.TryUnlockT4() == true) HUDTierSelector.Instance?.SyncWithShop(jumpToTier: 4); RefreshTierRes(); });
        _resT5Btn.onClick.AddListener(() => { if (TechManager.Instance?.TryUnlockT5() == true) HUDTierSelector.Instance?.SyncWithShop(jumpToTier: 5); RefreshTierRes(); });
    }

    // ── Mode switching ─────────────────────────────────────────────────

    void SetMode(Mode mode)
    {
        _mode = mode;
        _towerBody?.SetActive(mode == Mode.Tower);
        _towerResBody?.SetActive(mode == Mode.TowerResearch);
        _enemyBody?.SetActive(mode == Mode.Enemy);
        _tierResBody?.SetActive(mode == Mode.TierResearch);

        switch (mode)
        {
            case Mode.Tower:        if (_headerTitle != null) _headerTitle.text = "TOWER INFO";    break;
            case Mode.TowerResearch:if (_headerTitle != null) _headerTitle.text = "TOWER RESEARCH";break;
            case Mode.Enemy:        /* header title set by RefreshEnemy */                         break;
            case Mode.TierResearch: if (_headerTitle != null) _headerTitle.text = "RESEARCH";
                                    RefreshTierRes();                                              break;
            case Mode.None:         if (_headerTitle != null) _headerTitle.text = "";              break;
        }

        if (mode != Mode.None)
            _panel?.SetActive(true);
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
        RefreshTower(info);
        SetMode(Mode.Tower);
    }

    public void ShowEnemy(UnitManager unit)
    {
        if (unit == null || !unit.isAlive) return;
        DeselectCurrentTower();
        _selectedTower = null;
        SelectedEnemy = unit;
        RefreshEnemy();
        SetMode(Mode.Enemy);
    }

    public void ShowResearch() => SetMode(Mode.TierResearch);

    public void Hide()
    {
        DeselectCurrentTower();
        SelectedEnemy = null;
        _mode = Mode.None;
        _towerBody?.SetActive(false);
        _towerResBody?.SetActive(false);
        _enemyBody?.SetActive(false);
        _tierResBody?.SetActive(false);
        _panel?.SetActive(false);
    }

    public void Reset()
    {
        Hide();
        _panel?.SetActive(false);
    }

    // ── Tower refresh ──────────────────────────────────────────────────

    void RefreshTower(TowerInfo info)
    {
        if (info != _selectedTower) return;
        string tierLabel = info.maxTier > 1 ? $"  [Tier {info.Tier}/{info.maxTier}]" : "";
        string nameStr = info.displayName.ToUpper() + tierLabel;
        if (_headerTitle != null && _mode == Mode.Tower) _headerTitle.text = nameStr;
        if (_tpBalance != null) _tpBalance.text = $"Type  {info.balanceType}";
        RefreshBalanceDist(info);

        if (_tpDamage != null)
        {
            float baseDmg = info.damage * info.EffectiveDamageMult;
            string dmgStr = baseDmg > 0f ? $"Damage   {baseDmg:0.#}" : "Damage   —";
            if (baseDmg > 0f && info.AuraDamageMultiplier > 1.001f)
                dmgStr += $"  <color=#4DFF73>(+{baseDmg * (info.AuraDamageMultiplier - 1f):0.#})</color>";
            _tpDamage.text = dmgStr;
        }

        if (_tpShieldBonus != null)
        {
            // Scales with the same multipliers as the Damage line (and as runtime damage)
            float sb = info.shieldBonus * info.EffectiveDamageMult;
            if (Mathf.Abs(sb) > 0.001f)
            {
                string sign = sb > 0f ? "+" : "";
                string col  = sb > 0f ? "#4DFF73" : "#FF7A5C";
                _tpShieldBonus.text = $"vs Shields  <color={col}>{sign}{sb:0.#}</color>";
            }
            else _tpShieldBonus.text = "";
        }

        if (_tpFireRate != null)
        {
            var turrent   = info.GetComponent<Turrent>();
            float baseCd  = turrent?.fireAbility?.cost?.cooldownDuration ?? (info.cooldown > 0f ? info.cooldown : 0f);
            float speedMult = info.AuraSpeedMultiplier;
            var buffHandler = info.GetComponent<TowerBuffHandler>();
            if (buffHandler != null) speedMult *= 1f + buffHandler.FireRateMult;
            float effectiveCd = baseCd > 0f ? baseCd / speedMult : 0f;
            float baseFr      = baseCd > 0f ? 1f / baseCd : 0f;
            float fr          = effectiveCd > 0f ? 1f / effectiveCd : baseFr;
            string rateStr = baseFr > 0f ? $"Fire Rate  {baseFr:0.##}/s" : "Fire Rate  —";
            if (fr > baseFr + 0.001f)
                rateStr += $"  <color=#4DFF73>(+{fr - baseFr:0.##})</color>";
            _tpFireRate.text = rateStr;
        }

        if (_tpAura != null)
        {
            // Detector badge — gold when active, dim hint when it unlocks at a later tier
            if (info.detectorTier > 0)
                _tpAura.text = info.IsDetector
                    ? "<color=#FFD24D><b>◈ DETECTOR</b></color>"
                    : $"<color=#8A7B4A>◈ Detector at Tier {info.detectorTier}</color>";
            else
                _tpAura.text = "";
        }
        if (_tpKills != null) _tpKills.text = $"Kills  {info.KillCount}";

        if (_tpMoveZoneBtn != null)
        {
            var zone = info.GetComponent<SniperZone>();
            _tpMoveZoneBtn.gameObject.SetActive(zone != null);
            if (zone != null && _tpMoveZoneLabel != null)
                _tpMoveZoneLabel.text = zone.IsRepositioning ? "CONFIRMING..." : "MOVE ZONE";
        }

        RefreshTargetingButtons(info);
        RefreshUpgradeButton(info);

        if (_tpSellBtnLabel != null) _tpSellBtnLabel.text = $"SELL  —  {info.SellRefund}g";
    }

    void RefreshUpgradeButton(TowerInfo info)
    {
        if (_tpUpgradeBtn == null) return;
        _rm ??= ResourceManagerScript.Instance;
        bool canTier   = info.CanUpgrade;
        bool hasRes    = info.HasResearchForUpgrade;
        bool hasGold   = _rm != null && _rm.resourceOne >= info.UpgradeCost;
        _tpUpgradeBtn.gameObject.SetActive(info.maxTier > 1);
        _tpUpgradeBtn.interactable = canTier && hasRes && hasGold;
        if (_tpUpgradeBtnLabel == null) return;
        if (!canTier)        _tpUpgradeBtnLabel.text = "MAX TIER";
        else if (!hasRes)    _tpUpgradeBtnLabel.text = $"LOCKED  (needs Tier {info.RequiredResearchTier} research)";
        else
        {
            // Damage preview so upgrading isn't a blind purchase
            string label  = $"UPGRADE  Tier {info.Tier + 1}  —  {info.UpgradeCost}g";
            float  curDmg = info.damage * info.EffectiveDamageMult;
            if (curDmg > 0.01f)
                label += $"\nDMG  {curDmg:0.#} → {curDmg * info.upgradeStatMultiplier:0.#}";
            _tpUpgradeBtnLabel.text = label;
        }
    }

    void RefreshBalanceDist(TowerInfo info)
    {
        if (_tpBalanceDist == null) return;
        var towers = HUDHelpers.GetTowerData(out var idCounts);
        int worstCount = 0;
        foreach (var t in towers)
        {
            if (t.isGhost || t.balanceType != info.balanceType) continue;
            int cnt = idCounts.TryGetValue(t.definitionId, out int c) ? c : 0;
            if (cnt > worstCount) worstCount = cnt;
        }
        if (worstCount == 0) { _tpBalanceDist.text = "—"; return; }
        float ratio = HUDHelpers.BalanceRatio(worstCount);
        _tpBalanceDist.text = $"{info.balanceMultiplier:0.#}  ({Mathf.RoundToInt(ratio * 100f)}%)";
    }

    void RefreshTargetingButtons(TowerInfo info)
    {
        if (_tpTargetImgs == null) return;
        var turrent = info.GetComponent<Turrent>();
        TargetingMode current = turrent != null ? turrent.Targeting : TargetingMode.Furthest;
        Color active   = new Color(0.25f, 0.50f, 0.80f, 1f);
        Color inactive = new Color(0.18f, 0.18f, 0.28f, 1f);
        for (int i = 0; i < _tpTargetImgs.Length; i++)
            if (_tpTargetImgs[i] != null)
                _tpTargetImgs[i].color = (TargetingMode)i == current ? active : inactive;
    }

    void SetTargetingMode(TargetingMode mode)
    {
        if (_selectedTower == null) return;
        var t = _selectedTower.GetComponent<Turrent>();
        if (t != null) t.Targeting = mode;
        RefreshTargetingButtons(_selectedTower);
    }

    void OnMoveZoneClicked()
    {
        if (_selectedTower == null) return;
        _selectedTower.GetComponent<SniperZone>()?.BeginReposition();
        if (_tpMoveZoneLabel != null) _tpMoveZoneLabel.text = "CONFIRMING...";
    }

    void OnUpgradeClicked()
    {
        _rm ??= ResourceManagerScript.Instance;
        if (_selectedTower == null) return;
        if (_selectedTower.TryUpgrade(_rm))
        {
            PlayTowerActionSound(_selectedTower, d => d.upgradeSoundId, "tower_upgrade");
            RefreshTower(_selectedTower);
        }
    }

    // Tower-specific sound wins; the generic event is the fallback
    static void PlayTowerActionSound(TowerInfo info, Func<TowerDefinition, string> pick, string fallbackEvent)
    {
        string soundId = null;
        if (info != null && TowerDefinitionLibrary.Instance != null &&
            TowerDefinitionLibrary.Instance.TryGet(info.definitionId, out var def))
            soundId = pick(def);

        if (!string.IsNullOrEmpty(soundId)) AudioManager.Play(soundId);
        else AudioManager.PlayEvent(fallbackEvent);
    }

    void OnSellClicked()
    {
        _rm ??= ResourceManagerScript.Instance;
        if (_selectedTower == null) return;
        PlayTowerActionSound(_selectedTower, d => d.sellSoundId, "tower_sell");
        _selectedTower.Sell(_rm);
        DeselectCurrentTower();
        Hide();
    }

    void DeselectCurrentTower()
    {
        if (_selectedTower == null) return;
        _selectedTower.GetComponent<SniperZone>()?.SetSelected(false);
        _selectedTower.GetComponent<ShotgunOrienter>()?.SetSelected(false);
        _selectedTower = null;
    }

    // ── Tower research sub-body ────────────────────────────────────────

    void ShowTowerResBody()
    {
        if (_selectedTower == null) return;
        SetMode(Mode.TowerResearch);
        RefreshTowerResBody();
    }

    void RefreshTowerResBody()
    {
        if (_selectedTower == null || ResearchManager.Instance == null) return;

        foreach (var (btn, lbl, def) in _trButtons)
            if (btn != null) Destroy(btn.gameObject);
        _trButtons.Clear();

        var researches = ResearchManager.Instance.GetForTower(_selectedTower.definitionId);

        const float BTNW = 320f;
        const float BTNH = 50f;
        const float GAP  = 10f;
        float y = BODY_H - PAD - BTNH;

        foreach (var def in researches)
        {
            bool purchased = ResearchManager.Instance.IsPurchased(def.id);
            var bGO  = HUDHelpers.MakeRect($"Res_{def.id}", _towerResBody, PAD + 120f, y, BTNW, BTNH);
            var bImg = bGO.AddComponent<Image>();
            bImg.color = purchased ? new Color(0.18f,0.18f,0.18f) : new Color(0.14f,0.35f,0.55f);
            var btn  = bGO.AddComponent<Button>(); btn.targetGraphic = bImg;
            btn.interactable = !purchased;

            string label = purchased
                ? $"{def.displayName}\n(Purchased)"
                : $"{def.displayName}\n{def.description}   [{def.techCost} Tech]";
            var lbl = HUDHelpers.MakeText(HUDHelpers.MakeRect("L", bGO, 6f, 0f, BTNW - 8f, BTNH), label, Color.white, 13);
            lbl.alignment = TextAnchor.MiddleLeft; lbl.lineSpacing = 1.1f;

            var capDef = def;
            btn.onClick.AddListener(() => { ResearchManager.Instance.TryPurchase(capDef); RefreshTowerResBody(); RefreshTower(_selectedTower); });
            _trButtons.Add((btn, lbl, def));
            y -= BTNH + GAP;
        }
    }

    // ── Enemy refresh ──────────────────────────────────────────────────

    void RefreshEnemy()
    {
        if (SelectedEnemy == null) return;
        var u = SelectedEnemy;

        string name = u.definitionId;
        string desc = "";
        if (!string.IsNullOrEmpty(u.definitionId) && UnitDefinitionLibrary.Instance != null &&
            UnitDefinitionLibrary.Instance.TryGet(u.definitionId, out var def))
        {
            if (!string.IsNullOrEmpty(def.displayName)) name = def.displayName;
            desc = def.description ?? "";
        }

        if (_headerTitle  != null) _headerTitle.text = name.ToUpper();
        if (_epHp         != null) _epHp.text         = $"HP        {u.lifeCurrent:0}/{u.lifeMax:0}";
        if (_epShield     != null) _epShield.text     = u.hasShields ? $"Shield    {u.shieldCurrent:0}/{u.shieldMax:0}" : "";
        if (_epSpeed      != null) _epSpeed.text      = $"Speed     {u.speedMax:0.##}";
        if (_epDeathBlow  != null) _epDeathBlow.text  = $"End dmg   {u.deathBlow}";
        if (_epArmor      != null) _epArmor.text      = $"Armor     {u.physicalDefense}{ReductionPct(u, u.physicalDefense)}";
        if (_epResistance != null) _epResistance.text = $"Resist    {u.elementalDefense}{ReductionPct(u, u.elementalDefense)}";
        if (_epFortitude  != null) _epFortitude.text  = $"Fortitude {u.arcanaDefense}{ReductionPct(u, u.arcanaDefense)}";
        if (_epDescription!= null) _epDescription.text = desc;
    }

    /// <summary>
    /// Damage reduction granted by a defense stat, using the unit's live value —
    /// behaviors/auras that modify the stat show up automatically.
    /// </summary>
    static string ReductionPct(UnitParentClass u, int stat)
    {
        if (stat <= 0) return "";
        float pct = (1f - Mathf.Pow(u.damageReductionBaseModifier, stat)) * 100f;
        return $"  <color=#9be29b>(-{pct:0}%)</color>";
    }

    // ── Tier research refresh ──────────────────────────────────────────

    void RefreshTierRes()
    {
        var tm = TechManager.Instance;
        if (tm == null) return;

        void Setup(Button btn, Text lbl, bool done, bool prereq, bool canAfford,
                   int cost, int tier, System.Action unlock)
        {
            if (btn == null) return;
            btn.interactable = !done && prereq && canAfford;
            if (lbl == null) return;
            if (done)        lbl.text = $"Tier {tier}  ✓  Unlocked";
            else if (!prereq)lbl.text = $"Unlock Tier {tier}  —  {cost} tech  (needs T{tier-1})";
            else             lbl.text = $"Unlock Tier {tier}  —  {cost} tech";
        }

        Setup(_resT2Btn, _resT2Lbl, tm.T2Unlocked, true,              tm.Tech >= TechManager.T2Cost, TechManager.T2Cost, 2, null);
        Setup(_resT3Btn, _resT3Lbl, tm.T3Unlocked, tm.T2Unlocked,     tm.Tech >= TechManager.T3Cost, TechManager.T3Cost, 3, null);
        Setup(_resT4Btn, _resT4Lbl, tm.T4Unlocked, tm.T3Unlocked,     tm.Tech >= TechManager.T4Cost, TechManager.T4Cost, 4, null);
        Setup(_resT5Btn, _resT5Lbl, tm.T5Unlocked, tm.T4Unlocked,     tm.Tech >= TechManager.T5Cost, TechManager.T5Cost, 5, null);
    }

    // ── Update ─────────────────────────────────────────────────────────

    void Update()
    {
        if (_mode == Mode.Tower && _selectedTower != null)
        {
            RefreshUpgradeButton(_selectedTower);
            RefreshTower(_selectedTower);

            if (_tpMoveZoneLabel != null)
            {
                var zone = _selectedTower.GetComponent<SniperZone>();
                if (zone != null) _tpMoveZoneLabel.text = zone.IsRepositioning ? "CONFIRMING..." : "MOVE ZONE";
            }
        }

        if (_mode == Mode.Enemy)
        {
            if (SelectedEnemy != null && !SelectedEnemy.isAlive)
            {
                SelectedEnemy = null;
                Hide();
            }
            else
            {
                RefreshEnemy();
            }
        }

        if (_mode == Mode.TierResearch)
            RefreshTierRes();
    }

    public void OnTowerKill(TowerInfo info)
    {
        if (info == _selectedTower && _mode == Mode.Tower)
            RefreshTower(info);
    }

    public TowerInfo GetSelectedTower() => _mode == Mode.Tower ? _selectedTower : null;
}
