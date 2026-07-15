using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tower inspector of the info panel: stats + upgrade/research/sell buttons +
/// targeting (or research fill-priority) dropdowns, plus the per-tower research
/// sub-body. Built and owned by HUDInfoPanel, which routes mode changes and
/// holds the selection; this view reads it via owner.SelectedTower.
/// </summary>
internal sealed class InfoPanelTowerView
{
    const float W      = InfoPanelLayout.W;
    const float H      = InfoPanelLayout.H;
    const float HDR_H  = InfoPanelLayout.HDR_H;
    const float PAD    = InfoPanelLayout.PAD;
    const float BODY_H = InfoPanelLayout.BODY_H;
    const float COL_W  = InfoPanelLayout.COL_W;
    const float C0     = InfoPanelLayout.C0;
    const float C1     = InfoPanelLayout.C1;
    const float C2     = InfoPanelLayout.C2;
    const float C3     = InfoPanelLayout.C3;

    readonly HUDInfoPanel _owner;
    GameObject _towerBody;
    GameObject _towerResBody;

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
    Text       _tpTargetLabel;     // primary targeting dropdown label (current mode)
    GameObject _tpTargetPopup;     // option list, opens above the button
    Image[]    _tpTargetItemImgs;
    GameObject _tpTargetHdr;       // "TARGET 1" caption + button — hidden for research towers
    GameObject _tpTargetDD;
    Text       _tpTarget2Label;    // secondary targeting (tie-breaker) dropdown
    GameObject _tpTarget2Popup;
    Image[]    _tpTarget2ItemImgs;
    GameObject _tpTarget2Hdr;
    GameObject _tpTarget2DD;
    Text       _tpPrioLabel;       // research fill-priority dropdown (same C3 slot)
    GameObject _tpPrioHdr;
    GameObject _tpPrioDD;
    GameObject _tpPrioPopup;
    Image[]    _tpPrioItemImgs;

    // Tower research sub-panel
    readonly List<(Button btn, Text lbl, ResearchDefinition def)> _trButtons = new();

    ResourceManagerScript _rm;

    // Dropdown entries — order defines the list top-to-bottom
    static readonly (TargetingMode mode, string label)[] TargetOptions =
    {
        (TargetingMode.Furthest,       "Furthest"),
        (TargetingMode.Closest,        "Closest"),
        (TargetingMode.LowestHP,       "Lowest HP"),
        (TargetingMode.HighestHP,      "Highest HP"),
        (TargetingMode.LeastShields,   "Least Shields"),
        (TargetingMode.HighestShields, "Most Shields"),
        (TargetingMode.HighPrio,       "High Prio"),
        (TargetingMode.Boss,           "Boss"),
        (TargetingMode.Invisible,      "Invisible"),
    };

    public InfoPanelTowerView(HUDInfoPanel owner, GameObject panelRoot)
    {
        _owner = owner;
        BuildTowerBody(panelRoot);
        BuildTowerResBody(panelRoot);
    }

    public void SetTowerActive(bool active)    => _towerBody?.SetActive(active);
    public void SetResearchActive(bool active) => _towerResBody?.SetActive(active);

    public void ClosePopups()
    {
        _tpTargetPopup?.SetActive(false);
        _tpTarget2Popup?.SetActive(false);
        _tpPrioPopup?.SetActive(false);
    }

    // ── Build: tower body ──────────────────────────────────────────────

    void BuildTowerBody(GameObject panelRoot)
    {
        _towerBody = new GameObject("TowerBody");
        _towerBody.transform.SetParent(panelRoot.transform, false);
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
        resBtn.onClick.AddListener(() => { if (_owner.SelectedTower != null) _owner.ShowTowerResearchMode(); });

        float sellBY = resBY - BTNH - 5f;
        var (sellBtn, sellLbl) = HUDHelpers.MakeBtn(_towerBody, "SellBtn", C2, sellBY, COL_W, BTNH, new Color(0.55f,0.10f,0.10f,1f), "SELL", 12, true);
        _tpSellBtn = sellBtn; _tpSellBtnLabel = sellLbl;
        var sc = _tpSellBtn.colors; sc.highlightedColor = new Color(0.75f,0.15f,0.15f); _tpSellBtn.colors = sc;
        _tpSellBtn.onClick.AddListener(OnSellClicked);

        // C3: two stacked targeting dropdowns — primary on top, secondary
        // (tie-breaker) below. Each opens its option list upward.
        float tgtBtnY = TPAD;
        const float ITEM_H = 26f;
        float tgt1Y = tgtBtnY + 30f + 2f + 18f + 4f;   // primary sits above the secondary stack

        BuildTargetDropdown("TARGET 1", tgt1Y, SetTargetingMode,
            out _tpTargetHdr, out _tpTargetDD, out _tpTargetLabel,
            out _tpTargetPopup, out _tpTargetItemImgs);
        BuildTargetDropdown("TARGET 2", tgtBtnY, SetTargetingModeSecondary,
            out _tpTarget2Hdr, out _tpTarget2DD, out _tpTarget2Label,
            out _tpTarget2Popup, out _tpTarget2ItemImgs);

        // C3 (research towers): fill-priority dropdown in the same slot as targeting
        _tpPrioHdr = HUDHelpers.MakeRect("PrioLabel", _towerBody, C3, tgtBtnY + 30f + 5f, COL_W, 20f);
        HUDHelpers.MakeText(_tpPrioHdr, "FILL PRIORITY", new Color(0.6f, 0.6f, 0.7f), 10, bold: true);

        _tpPrioDD = HUDHelpers.MakeRect("PrioDropdown", _towerBody, C3, tgtBtnY, COL_W, 30f);
        var pdImg = _tpPrioDD.AddComponent<Image>(); pdImg.color = new Color(0.18f, 0.18f, 0.28f, 1f);
        var pdBtn = _tpPrioDD.AddComponent<Button>(); pdBtn.targetGraphic = pdImg;
        _tpPrioLabel = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("L", _tpPrioDD, 0f, 0f, COL_W, 30f), "Prio 1  ▾", Color.white, 10, bold: true);
        _tpPrioLabel.alignment = TextAnchor.MiddleCenter;
        pdBtn.onClick.AddListener(TogglePrioPopup);

        float prioPopupH = ResearchTower.MaxPriority * ITEM_H;
        _tpPrioPopup = HUDHelpers.MakeRect("PrioPopup", _towerBody, C3, tgtBtnY + 30f + 2f, COL_W, prioPopupH);
        _tpPrioPopup.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.16f, 0.98f);
        _tpPrioItemImgs = new Image[ResearchTower.MaxPriority];
        for (int i = 0; i < ResearchTower.MaxPriority; i++)
        {
            int prio  = i + 1;
            float iy  = prioPopupH - (i + 1) * ITEM_H;
            var  iGO  = HUDHelpers.MakeRect($"Prio_{prio}", _tpPrioPopup, 1f, iy + 1f, COL_W - 2f, ITEM_H - 2f);
            var  iImg = iGO.AddComponent<Image>(); iImg.color = new Color(0.18f, 0.18f, 0.28f, 1f);
            var  iBtn = iGO.AddComponent<Button>(); iBtn.targetGraphic = iImg;
            HUDHelpers.MakeText(HUDHelpers.MakeRect("L", iGO, 0f, 0f, COL_W - 2f, ITEM_H - 2f), $"Prio {prio}", Color.white, 10, bold: true)
                .alignment = TextAnchor.MiddleCenter;
            iBtn.onClick.AddListener(() => { SetResearchPriority(prio); _owner.CloseAllPopups(); });
            _tpPrioItemImgs[i] = iImg;
        }
        _tpPrioPopup.SetActive(false);
        _tpPrioHdr.SetActive(false);
        _tpPrioDD.SetActive(false);
    }

    /// <summary>Builds one targeting dropdown (caption, button, upward option list) in C3.</summary>
    void BuildTargetDropdown(string caption, float ddY, Action<TargetingMode> onPick,
                             out GameObject hdr, out GameObject dd, out Text lbl,
                             out GameObject popup, out Image[] itemImgs)
    {
        const float ITEM_H = 26f;

        hdr = HUDHelpers.MakeRect(caption + "_Hdr", _towerBody, C3, ddY + 30f + 2f, COL_W, 18f);
        HUDHelpers.MakeText(hdr, caption, new Color(0.6f, 0.6f, 0.7f), 10, bold: true);

        dd = HUDHelpers.MakeRect(caption + "_DD", _towerBody, C3, ddY, COL_W, 30f);
        var ddImg = dd.AddComponent<Image>(); ddImg.color = new Color(0.18f, 0.18f, 0.28f, 1f);
        var ddBtn = dd.AddComponent<Button>(); ddBtn.targetGraphic = ddImg;
        lbl = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("L", dd, 0f, 0f, COL_W, 30f), "Furthest  ▾", Color.white, 10, bold: true);
        lbl.alignment = TextAnchor.MiddleCenter;

        // Option list opens upward from the button; drawn over other columns
        // (UGUI sibling order = draw order).
        float popupH = TargetOptions.Length * ITEM_H;
        popup = HUDHelpers.MakeRect(caption + "_Popup", _towerBody, C3, ddY + 30f + 2f, COL_W, popupH);
        popup.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.16f, 0.98f);
        itemImgs = new Image[TargetOptions.Length];
        for (int i = 0; i < TargetOptions.Length; i++)
        {
            var (mode, label) = TargetOptions[i];
            float iy  = popupH - (i + 1) * ITEM_H;
            var  iGO  = HUDHelpers.MakeRect($"Opt_{mode}", popup, 1f, iy + 1f, COL_W - 2f, ITEM_H - 2f);
            var  iImg = iGO.AddComponent<Image>(); iImg.color = new Color(0.18f, 0.18f, 0.28f, 1f);
            var  iBtn = iGO.AddComponent<Button>(); iBtn.targetGraphic = iImg;
            HUDHelpers.MakeText(HUDHelpers.MakeRect("L", iGO, 0f, 0f, COL_W - 2f, ITEM_H - 2f), label, Color.white, 10, bold: true)
                .alignment = TextAnchor.MiddleCenter;
            var capMode = mode;
            iBtn.onClick.AddListener(() => { onPick(capMode); _owner.CloseAllPopups(); });
            itemImgs[i] = iImg;
        }
        popup.SetActive(false);

        // Toggling one dropdown closes everything else first
        var capPopup = popup;
        ddBtn.onClick.AddListener(() =>
        {
            bool wasOpen = capPopup.activeSelf;
            _owner.CloseAllPopups();
            capPopup.SetActive(!wasOpen);
        });
    }

    // ── Build: tower research sub-body ─────────────────────────────────

    void BuildTowerResBody(GameObject panelRoot)
    {
        _towerResBody = new GameObject("TowerResBody");
        _towerResBody.transform.SetParent(panelRoot.transform, false);
        var rt = _towerResBody.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(W, H);

        var (backBtn, _) = HUDHelpers.MakeBtn(_towerResBody, "BackBtn",
            PAD, H - HDR_H - PAD - 40f, 110f, 40f,
            new Color(0.25f, 0.25f, 0.35f, 1f), "← Back", 13, bold: true);
        backBtn.onClick.AddListener(() => _owner.ShowTowerMode());
    }

    public void RefreshResearchBody()
    {
        var selected = _owner.SelectedTower;
        if (selected == null || ResearchManager.Instance == null) return;

        foreach (var (btn, lbl, def) in _trButtons)
            if (btn != null) UnityEngine.Object.Destroy(btn.gameObject);
        _trButtons.Clear();

        var researches = ResearchManager.Instance.GetForTower(selected.definitionId);

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
                : $"{def.displayName}\n{DescriptionTags.Resolve(def.description)}   [{def.techCost} Tech]";
            var lbl = HUDHelpers.MakeText(HUDHelpers.MakeRect("L", bGO, 6f, 0f, BTNW - 8f, BTNH), label, Color.white, 13);
            lbl.alignment = TextAnchor.MiddleLeft; lbl.lineSpacing = 1.1f;

            var capDef = def;
            btn.onClick.AddListener(() =>
            {
                ResearchManager.Instance.TryPurchase(capDef);
                RefreshResearchBody();
                Refresh(_owner.SelectedTower);
            });
            _trButtons.Add((btn, lbl, def));
            y -= BTNH + GAP;
        }
    }

    // ── Refresh ────────────────────────────────────────────────────────

    public void Refresh(TowerInfo info)
    {
        if (info == null || info != _owner.SelectedTower) return;
        string tierLabel = info.maxTier > 1 ? $"  [Tier {info.Tier}/{info.maxTier}]" : "";
        string nameStr = info.displayName.ToUpper() + tierLabel;
        if (_towerBody != null && _towerBody.activeSelf) _owner.SetHeaderTitle(nameStr);
        if (_tpBalance != null) _tpBalance.text = $"Type  {info.balanceType}";
        RefreshBalanceDist(info);

        if (_tpDamage != null)
        {
            float baseDmg = info.damage * info.EffectiveDamageMult;
            string dmgStr = baseDmg > 0f ? $"Damage   {baseDmg:0.#}" : "Damage   —";
            if (baseDmg > 0f && info.AuraDamageMultiplier > 1.001f)
                dmgStr += $"  <color=#4DFF73>(+{baseDmg * (info.AuraDamageMultiplier - 1f):0.#})</color>";
            if (baseDmg > 0f && info.hasDamageType)
                dmgStr += $"  {DamageTypeColors.Tag(info.damageType)}";
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
            var turret   = info.GetComponent<Turret>();
            float baseCd  = turret?.fireAbility?.cost?.cooldownDuration ?? (info.cooldown > 0f ? info.cooldown : 0f);
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

        // Research towers show a fill-priority dropdown where targeting normally sits
        bool isResearch = info.GetComponent<ResearchTower>() != null;
        if (_tpTargetHdr  != null) _tpTargetHdr.SetActive(!isResearch);
        if (_tpTargetDD   != null) _tpTargetDD.SetActive(!isResearch);
        if (_tpTarget2Hdr != null) _tpTarget2Hdr.SetActive(!isResearch);
        if (_tpTarget2DD  != null) _tpTarget2DD.SetActive(!isResearch);
        if (_tpPrioHdr   != null) _tpPrioHdr.SetActive(isResearch);
        if (_tpPrioDD    != null) _tpPrioDD.SetActive(isResearch);
        if (isResearch) RefreshPrioButtons(info);
        else            RefreshTargetingButtons(info);

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
        var turret = info.GetComponent<Turret>();
        TargetingMode current  = turret != null ? turret.Targeting          : TargetingMode.Furthest;
        TargetingMode current2 = turret != null ? turret.TargetingSecondary : TargetingMode.Furthest;

        if (_tpTargetLabel  != null) _tpTargetLabel.text  = TargetLabelFor(current)  + "  ▾";
        if (_tpTarget2Label != null) _tpTarget2Label.text = TargetLabelFor(current2) + "  ▾";

        Color active   = new Color(0.25f, 0.50f, 0.80f, 1f);
        Color inactive = new Color(0.18f, 0.18f, 0.28f, 1f);
        if (_tpTargetItemImgs != null)
            for (int i = 0; i < _tpTargetItemImgs.Length; i++)
                if (_tpTargetItemImgs[i] != null)
                    _tpTargetItemImgs[i].color = TargetOptions[i].mode == current ? active : inactive;
        if (_tpTarget2ItemImgs != null)
            for (int i = 0; i < _tpTarget2ItemImgs.Length; i++)
                if (_tpTarget2ItemImgs[i] != null)
                    _tpTarget2ItemImgs[i].color = TargetOptions[i].mode == current2 ? active : inactive;
    }

    static string TargetLabelFor(TargetingMode mode)
    {
        foreach (var (m, label) in TargetOptions)
            if (m == mode) return label;
        return mode.ToString();
    }

    void RefreshPrioButtons(TowerInfo info)
    {
        var rt = info.GetComponent<ResearchTower>();
        int current = rt != null ? rt.priority : 1;
        if (_tpPrioLabel != null) _tpPrioLabel.text = $"Prio {current}  ▾";

        if (_tpPrioItemImgs == null) return;
        Color active   = new Color(0.25f, 0.50f, 0.80f, 1f);
        Color inactive = new Color(0.18f, 0.18f, 0.28f, 1f);
        for (int i = 0; i < _tpPrioItemImgs.Length; i++)
            if (_tpPrioItemImgs[i] != null)
                _tpPrioItemImgs[i].color = (i + 1) == current ? active : inactive;
    }

    // ── Click handlers ─────────────────────────────────────────────────

    void SetTargetingMode(TargetingMode mode)
    {
        var selected = _owner.SelectedTower;
        if (selected == null) return;
        var t = selected.GetComponent<Turret>();
        if (t != null) t.Targeting = mode;
        RefreshTargetingButtons(selected);
    }

    void SetTargetingModeSecondary(TargetingMode mode)
    {
        var selected = _owner.SelectedTower;
        if (selected == null) return;
        var t = selected.GetComponent<Turret>();
        if (t != null) t.TargetingSecondary = mode;
        RefreshTargetingButtons(selected);
    }

    void TogglePrioPopup()
    {
        if (_tpPrioPopup != null) _tpPrioPopup.SetActive(!_tpPrioPopup.activeSelf);
    }

    void SetResearchPriority(int prio)
    {
        var selected = _owner.SelectedTower;
        if (selected == null) return;
        var rt = selected.GetComponent<ResearchTower>();
        if (rt != null) rt.priority = Mathf.Clamp(prio, 1, ResearchTower.MaxPriority);
        RefreshPrioButtons(selected);
    }

    void OnMoveZoneClicked()
    {
        var selected = _owner.SelectedTower;
        if (selected == null) return;
        selected.GetComponent<SniperZone>()?.BeginReposition();
        if (_tpMoveZoneLabel != null) _tpMoveZoneLabel.text = "CONFIRMING...";
    }

    void OnUpgradeClicked()
    {
        _rm ??= ResourceManagerScript.Instance;
        var selected = _owner.SelectedTower;
        if (selected == null) return;
        if (selected.TryUpgrade(_rm))
        {
            PlayTowerActionSound(selected, d => d.upgradeSoundId, "tower_upgrade");
            Refresh(selected);
        }
    }

    void OnSellClicked()
    {
        _rm ??= ResourceManagerScript.Instance;
        var selected = _owner.SelectedTower;
        if (selected == null) return;
        PlayTowerActionSound(selected, d => d.sellSoundId, "tower_sell");
        selected.Sell(_rm);
        _owner.Hide();   // Hide() also deselects the (now destroyed) tower
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
}
