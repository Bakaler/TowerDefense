using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Builds and drives the entire game HUD in code. No inspector wiring, no prefabs.
///
/// Setup: drop this component on any persistent GameObject in the scene.
///
/// Layout:
///   Top bar   — Gold (left) | Wave status (center) | Lives (right)
///   Bottom    — "Start Wave" button, center-bottom
///   Overlays  — Game Over and Victory panels (shown/hidden automatically)
/// </summary>
[DisallowMultipleComponent]
public class GameHUD : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────
    public static GameHUD Instance { get; private set; }

    // ── Live refs ─────────────────────────────────────────────────────
    private Text   _goldText;
    private Text   _techText;
    private Text   _waveText;
    private Text   _livesText;
    private Button _waveButton;
    private Text   _waveButtonLabel;
    private GameObject _gameOverPanel;
    private GameObject _victoryPanel;

    // ── Header balance scores ─────────────────────────────────────────
    private Text _hdrElem;
    private Text _hdrElemBonus;
    private Text _hdrArc;
    private Text _hdrArcBonus;
    private Text _hdrPhys;
    private Text _hdrPhysDrop;

    // ── Research panel ────────────────────────────────────────────────
    private GameObject _researchPanel;
    private Button     _researchT2Btn;
    private Text       _researchT2Label;
    private Button     _researchT3Btn;
    private Text       _researchT3Label;
    private Button     _researchT4Btn;
    private Text       _researchT4Label;
    private Button     _researchT5Btn;
    private Text       _researchT5Label;

    // ── Tower info panel ──────────────────────────────────────────────
    private GameObject _towerPanel;
    private Text       _tpName;
    private Text       _tpBalance;
    private Text       _tpBalanceDist;
    private Text       _tpDamage;
    private Text       _tpFireRate;
    private Text       _tpKills;
    private Text       _tpAura;
    private Button     _tpUpgradeBtn;
    private Text       _tpUpgradeBtnLabel;
    private Button     _tpSellBtn;
    private Text       _tpSellBtnLabel;
    private Button[]   _tpTargetBtns;
    private Image[]    _tpTargetImgs;
    private TowerInfo  _selectedTower;

    // ── Tower research sub-panel ──────────────────────────────────────
    private GameObject _towerPanelBody;     // shown on main view
    private GameObject _towerResearchBody;  // shown on research view
    private GameObject _trHeader;           // shared header (always visible)
    private Text       _trHeaderName;       // name label in shared header (alias of _tpName)
    private List<(Button btn, Text lbl, ResearchDefinition def)> _trButtons = new();

    // ── Enemy info panel ──────────────────────────────────────────────
    private GameObject  _enemyPanel;
    private Text        _epName;
    private Text        _epHp;
    private Text        _epSpeed;
    private Text        _epDeathBlow;
    private Text        _epArmor;
    private Text        _epResistance;
    private Text        _epFortitude;
    private Text        _epDescription;
    private UnitManager _selectedEnemy;

    // ── Cached managers ───────────────────────────────────────────────
    private ResourceManagerScript _rm;
    private LogicManager          _lm;

    // ── Style constants ───────────────────────────────────────────────
    static readonly Color C_BarBg      = new Color(0.07f, 0.07f, 0.13f, 0.92f);
    static readonly Color C_Gold       = new Color(1.00f, 0.85f, 0.30f, 1.00f);
    static readonly Color C_Lives      = new Color(0.95f, 0.30f, 0.30f, 1.00f);
    static readonly Color C_Wave       = new Color(0.75f, 0.90f, 1.00f, 1.00f);
    static readonly Color C_BtnReady   = new Color(0.18f, 0.60f, 0.28f, 1.00f);
    static readonly Color C_BtnWait    = new Color(0.28f, 0.28f, 0.32f, 1.00f);
    static readonly Color C_OverlayBg  = new Color(0.04f, 0.04f, 0.10f, 0.94f);
    static readonly Color C_GameOver   = new Color(0.95f, 0.22f, 0.22f, 1.00f);
    static readonly Color C_Victory    = new Color(0.28f, 0.90f, 0.42f, 1.00f);
    static readonly Color C_BtnRestart = new Color(0.18f, 0.46f, 0.88f, 1.00f);

    // ── Lifecycle ─────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildHUD();
    }

    void Start()
    {
        _rm = FindFirstObjectByType<ResourceManagerScript>();
        _lm = FindFirstObjectByType<LogicManager>();
    }

    // ── Build ─────────────────────────────────────────────────────────

    void BuildHUD()
    {
        // Root canvas
        var canvasGO = new GameObject("[GameHUD]");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;   // above everything else

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        BuildTopBar(canvasGO);
        BuildTowerPanel(canvasGO);
        BuildEnemyPanel(canvasGO);
        BuildResearchPanel(canvasGO);
        BuildDebugPanel(canvasGO);
        _gameOverPanel = BuildOverlay(canvasGO, "GameOverPanel",  "GAME OVER", C_GameOver,  "Restart",    WaveManager.Restart);
        _victoryPanel  = BuildOverlay(canvasGO, "VictoryPanel",   "VICTORY",   C_Victory,   "Play Again", WaveManager.Restart);

        _gameOverPanel.SetActive(false);
        _victoryPanel.SetActive(false);
    }

    void OnEnable()
    {
        TowerInfo.OnTowerClicked += ShowTowerPanel;
        TowerInfo.OnTowerKill    += RefreshTowerPanel;
    }

    void OnDisable()
    {
        TowerInfo.OnTowerClicked -= ShowTowerPanel;
        TowerInfo.OnTowerKill    -= RefreshTowerPanel;
    }

    void ShowTowerPanel(TowerInfo info)
    {
        if (info == null)
        {
            _selectedTower = null;
            _towerPanel?.SetActive(false);
            return;
        }
        _selectedEnemy = null;
        _enemyPanel?.SetActive(false);
        _selectedTower = info;
        HideResearchBody();
        RefreshTowerPanel(info);
        _towerPanel?.SetActive(true);
    }

    public void SelectEnemy(UnitManager unit)
    {
        if (unit == null || !unit.isAlive) return;
        _selectedTower = null;
        _towerPanel?.SetActive(false);
        _selectedEnemy = unit;
        RefreshEnemyPanel();
        _enemyPanel?.SetActive(true);
    }

    void RefreshEnemyPanel()
    {
        if (_selectedEnemy == null) return;
        var u = _selectedEnemy;

        // Always resolve name + description from the library so all spawn paths work
        string name = u.definitionId;
        string desc = "";
        if (!string.IsNullOrEmpty(u.definitionId) && UnitDefinitionLibrary.Instance != null &&
            UnitDefinitionLibrary.Instance.TryGet(u.definitionId, out var def))
        {
            if (!string.IsNullOrEmpty(def.displayName)) name = def.displayName;
            desc = def.description ?? "";
        }

        if (_epName       != null) _epName.text       = name.ToUpper();
        if (_epHp         != null) _epHp.text         = $"HP        {u.lifeCurrent:0}/{u.lifeMax:0}";
        if (_epSpeed      != null) _epSpeed.text      = $"Speed     {u.speedMax:0.##}";
        if (_epDeathBlow  != null) _epDeathBlow.text  = $"End dmg   {u.deathBlow}";
        if (_epArmor      != null) _epArmor.text      = $"Armor     {u.physicalDefense}";
        if (_epResistance != null) _epResistance.text = $"Resist    {u.elementalDefense}";
        if (_epFortitude  != null) _epFortitude.text  = $"Fortitude {u.arcanaDefense}";
        if (_epDescription != null) _epDescription.text = desc;
    }

    void RefreshTowerPanel(TowerInfo info)
    {
        if (info != _selectedTower) return;
        string tierLabel = info.maxTier > 1 ? $"  [Tier {info.Tier}/{info.maxTier}]" : "";
        if (_tpName     != null) _tpName.text     = info.displayName.ToUpper() + tierLabel;
        if (_tpBalance  != null) _tpBalance.text  = $"Type  {info.balanceType}";
        RefreshBalanceDist();
        if (_tpDamage != null)
        {
            float baseDmg = info.damage * info.StatMultiplier;
            string dmgStr = baseDmg > 0f
                ? $"Damage   {baseDmg:0.#}"
                : "Damage   —";
            if (baseDmg > 0f && info.AuraDamageMultiplier > 1.001f)
            {
                float bonus = baseDmg * (info.AuraDamageMultiplier - 1f);
                dmgStr += $"  <color=#4DFF73>(+{bonus:0.#})</color>";
            }
            _tpDamage.text = dmgStr;
        }

        if (_tpFireRate != null)
        {
            // Read from ability's actual cooldown so research bonuses show immediately
            var turrent = info.GetComponent<Turrent>();
            float cd = turrent?.fireAbility?.cost?.cooldownDuration ?? (info.cooldown > 0f ? info.cooldown : 0f);
            float fr = cd > 0f ? 1f / cd : 0f;
            string rateStr = fr > 0f
                ? $"Fire Rate  {fr:0.##}/s"
                : "Fire Rate  —";
            if (fr > 0f && info.AuraSpeedMultiplier > 1.001f)
            {
                float boost = fr * info.AuraSpeedMultiplier - fr;
                rateStr += $"  <color=#4DFF73>(+{boost:0.##})</color>";
            }
            _tpFireRate.text = rateStr;
        }

        if (_tpAura != null) _tpAura.text = "";

        // Highlight active targeting button
        RefreshTargetingButtons(info);

        if (_tpKills    != null) _tpKills.text     = $"Kills  {info.KillCount}";

        if (_tpUpgradeBtn != null)
        {
            bool canTier     = info.CanUpgrade;
            bool hasResearch = info.HasResearchForUpgrade;
            bool hasGold     = _rm != null && _rm.resourceOne >= info.UpgradeCost;
            _tpUpgradeBtn.gameObject.SetActive(info.maxTier > 1);
            _tpUpgradeBtn.interactable = canTier && hasResearch && hasGold;
            if (_tpUpgradeBtnLabel != null)
            {
                if (!canTier)
                    _tpUpgradeBtnLabel.text = "MAX TIER";
                else if (!hasResearch)
                    _tpUpgradeBtnLabel.text = $"LOCKED  (needs Tier {info.RequiredResearchTier} research)";
                else
                    _tpUpgradeBtnLabel.text = $"UPGRADE  Tier {info.Tier + 1}  —  {info.UpgradeCost}g";
            }
        }

        if (_tpSellBtnLabel != null)
            _tpSellBtnLabel.text = $"SELL  —  {info.SellRefund}g";
    }

    void SetTargetingMode(TargetingMode mode)
    {
        if (_selectedTower == null) return;
        var turrent = _selectedTower.GetComponent<Turrent>();
        if (turrent != null) turrent.Targeting = mode;
        RefreshTargetingButtons(_selectedTower);
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

    void OnUpgradeClicked()
    {
        if (_selectedTower == null) return;
        if (_selectedTower.TryUpgrade(_rm))
            RefreshTowerPanel(_selectedTower);
    }

    void RefreshResearchPanel()
    {
        var tm = TechManager.Instance;
        if (tm == null) return;

        if (_researchT2Btn != null)
        {
            bool done = tm.T2Unlocked;
            _researchT2Btn.interactable = !done && tm.Tech >= TechManager.T2Cost;
            if (_researchT2Label != null)
                _researchT2Label.text = done
                    ? "Tier 2  ✓  Unlocked"
                    : $"Unlock Tier 2  —  {TechManager.T2Cost} tech";
        }

        if (_researchT3Btn != null)
        {
            bool done    = tm.T3Unlocked;
            bool canAfford = tm.Tech >= TechManager.T3Cost;
            bool prereq  = tm.T2Unlocked;
            _researchT3Btn.interactable = !done && prereq && canAfford;
            if (_researchT3Label != null)
            {
                if (done)
                    _researchT3Label.text = "Tier 3  ✓  Unlocked";
                else if (!prereq)
                    _researchT3Label.text = $"Unlock Tier 3  —  {TechManager.T3Cost} tech  (needs T2)";
                else
                    _researchT3Label.text = $"Unlock Tier 3  —  {TechManager.T3Cost} tech";
            }
        }

        if (_researchT4Btn != null)
        {
            bool done      = tm.T4Unlocked;
            bool canAfford = tm.Tech >= TechManager.T4Cost;
            bool prereq    = tm.T3Unlocked;
            _researchT4Btn.interactable = !done && prereq && canAfford;
            if (_researchT4Label != null)
            {
                if (done)      _researchT4Label.text = "Tier 4  ✓  Unlocked";
                else if (!prereq) _researchT4Label.text = $"Unlock Tier 4  —  {TechManager.T4Cost} tech  (needs T3)";
                else           _researchT4Label.text = $"Unlock Tier 4  —  {TechManager.T4Cost} tech";
            }
        }

        if (_researchT5Btn != null)
        {
            bool done      = tm.T5Unlocked;
            bool canAfford = tm.Tech >= TechManager.T5Cost;
            bool prereq    = tm.T4Unlocked;
            _researchT5Btn.interactable = !done && prereq && canAfford;
            if (_researchT5Label != null)
            {
                if (done)      _researchT5Label.text = "Tier 5  ✓  Unlocked";
                else if (!prereq) _researchT5Label.text = $"Unlock Tier 5  —  {TechManager.T5Cost} tech  (needs T4)";
                else           _researchT5Label.text = $"Unlock Tier 5  —  {TechManager.T5Cost} tech";
            }
        }
    }

    // Returns all non-ghost towers, plus a dictionary of definitionId → count
    static TowerInfo[] GetTowerData(out Dictionary<string, int> idCounts)
    {
        var towers = FindObjectsByType<TowerInfo>(FindObjectsSortMode.None);
        idCounts = new Dictionary<string, int>();
        foreach (var t in towers)
        {
            if (t.isGhost) continue;
            if (!idCounts.ContainsKey(t.definitionId)) idCounts[t.definitionId] = 0;
            idCounts[t.definitionId]++;
        }
        return towers;
    }

    // 1-4 towers of the same id = 1.00; steeply declines past 4 via (4/count)^2
    static float BalanceRatio(int count)
    {
        if (count <= 0) return 0f;
        if (count <= 4) return 1f;
        float r = 4f / count;
        return r * r;
    }

    void RefreshBalanceDist()
    {
        if (_tpBalanceDist == null || _selectedTower == null) return;
        var towers = GetTowerData(out var idCounts);

        // Find the worst idCount among all towers of the same balance type
        int worstCount = 0;
        foreach (var t in towers)
        {
            if (t.isGhost || t.balanceType != _selectedTower.balanceType) continue;
            int cnt = idCounts.TryGetValue(t.definitionId, out int c) ? c : 0;
            if (cnt > worstCount) worstCount = cnt;
        }
        if (worstCount == 0) { _tpBalanceDist.text = "—"; return; }

        float ratio   = BalanceRatio(worstCount);
        int   pct     = Mathf.RoundToInt(ratio * 100f);
        float multVal = _selectedTower.balanceMultiplier;
        _tpBalanceDist.text = $"{multVal:0.#}  ({pct}%)";
    }

    void RefreshHeaderBalance()
    {
        var towers = GetTowerData(out var idCounts);
        float eCum = 0f, aCum = 0f, pCum = 0f;
        foreach (var t in towers)
        {
            if (t.isGhost) continue;
            float contribution = BalanceRatio(idCounts[t.definitionId]) * t.balanceMultiplier;
            switch (t.balanceType)
            {
                case BalanceType.Elemental: eCum += contribution; break;
                case BalanceType.Arcane:    aCum += contribution; break;
                case BalanceType.Physical:  pCum += contribution; break;
            }
        }
        if (_hdrElem != null) _hdrElem.text = $"E  {eCum:0.00}";
        if (_hdrArc  != null) _hdrArc.text  = $"A  {aCum:0.00}";
        if (_hdrPhys != null) _hdrPhys.text = $"P  {pCum:0.00}";

        if (_hdrPhysDrop != null)
        {
            int baseChance  = 15;
            int bonusChance = Mathf.FloorToInt(pCum * 0.25f);
            _hdrPhysDrop.text = $"{baseChance}% + {bonusChance}%";
        }
        if (_hdrElemBonus != null)
        {
            int elemBonus = Mathf.FloorToInt((1f - Mathf.Pow(0.99f, eCum)) * 100f);
            _hdrElemBonus.text = $"+{elemBonus}% orbs";
        }
        if (_hdrArcBonus != null)
        {
            int arcBonus = Mathf.FloorToInt((1f - Mathf.Pow(0.99f, aCum)) * 100f);
            _hdrArcBonus.text = $"+{arcBonus}% sci";
        }
    }

    // ── Tower info panel ──────────────────────────────────────────────

    void BuildResearchPanel(GameObject root)
    {
        const float W = 420f, H = 780f, PAD = 18f;

        _researchPanel = new GameObject("ResearchPanel");
        _researchPanel.transform.SetParent(root.transform, false);

        // Centered on screen
        var rt          = _researchPanel.AddComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0.5f, 0.5f);
        rt.anchorMax    = new Vector2(0.5f, 0.5f);
        rt.pivot        = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta    = new Vector2(W, H);

        var rpBg = _researchPanel.AddComponent<Image>();
        rpBg.color = new Color(0.06f, 0.09f, 0.07f, 0.97f);
        rpBg.raycastTarget = false;   // let world clicks pass through the panel background

        // Title
        var titleGO = MakeRect("Title", _researchPanel, PAD, H - PAD - 40f, W - PAD * 2f - 36f, 40f);
        var title   = MakeText(titleGO, "RESEARCH", new Color(0.35f, 1f, 0.55f), 20, bold: true);
        title.alignment = TextAnchor.MiddleLeft;

        // Close button — top right
        var closeGO  = MakeRect("CloseBtn", _researchPanel, W - PAD - 32f, H - PAD - 36f, 32f, 32f);
        var closeImg = closeGO.AddComponent<Image>();
        closeImg.color = new Color(0.4f, 0.12f, 0.12f, 1f);
        var closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(() => _researchPanel.SetActive(false));
        MakeText(MakeRect("X", closeGO, 0f, 0f, 32f, 32f), "×", Color.white, 22, bold: true)
            .alignment = TextAnchor.MiddleCenter;

        // ── Tier 2 unlock ─────────────────────────────────────────────
        var div2GO = MakeRect("Div2", _researchPanel, PAD, H - PAD - 90f, W - PAD * 2f, 22f);
        var div2   = MakeText(div2GO, "── Tier 2 ──────────────────", new Color(0.5f, 0.7f, 0.55f), 12);
        div2.alignment = TextAnchor.MiddleLeft;

        var t2GO  = MakeRect("T2Btn", _researchPanel, PAD, H - PAD - 165f, W - PAD * 2f, 65f);
        var t2Img = t2GO.AddComponent<Image>();
        t2Img.color = new Color(0.12f, 0.22f, 0.15f, 1f);
        _researchT2Btn = t2GO.AddComponent<Button>();
        _researchT2Btn.targetGraphic = t2Img;
        var t2Cols = _researchT2Btn.colors;
        t2Cols.highlightedColor = new Color(0.18f, 0.32f, 0.22f, 1f);
        t2Cols.pressedColor     = new Color(0.08f, 0.14f, 0.10f, 1f);
        t2Cols.disabledColor    = new Color(0.18f, 0.18f, 0.20f, 1f);
        _researchT2Btn.colors = t2Cols;
        _researchT2Label = MakeText(MakeRect("Label", t2GO, 0f, 0f, W - PAD * 2f, 65f),
            "", new Color(0.6f, 0.9f, 0.65f), 15);
        _researchT2Label.alignment = TextAnchor.MiddleCenter;
        _researchT2Btn.onClick.AddListener(() => {
            if (TechManager.Instance != null && TechManager.Instance.TryUnlockT2())
                RefreshResearchPanel();
        });

        // ── Tier 3 unlock ─────────────────────────────────────────────
        var div3GO = MakeRect("Div3", _researchPanel, PAD, H - PAD - 205f, W - PAD * 2f, 22f);
        var div3   = MakeText(div3GO, "── Tier 3 ──────────────────", new Color(0.6f, 0.55f, 0.8f), 12);
        div3.alignment = TextAnchor.MiddleLeft;

        var t3GO  = MakeRect("T3Btn", _researchPanel, PAD, H - PAD - 280f, W - PAD * 2f, 65f);
        var t3Img = t3GO.AddComponent<Image>();
        t3Img.color = new Color(0.16f, 0.12f, 0.26f, 1f);
        _researchT3Btn = t3GO.AddComponent<Button>();
        _researchT3Btn.targetGraphic = t3Img;
        var t3Cols = _researchT3Btn.colors;
        t3Cols.highlightedColor = new Color(0.24f, 0.18f, 0.38f, 1f);
        t3Cols.pressedColor     = new Color(0.10f, 0.08f, 0.16f, 1f);
        t3Cols.disabledColor    = new Color(0.18f, 0.18f, 0.20f, 1f);
        _researchT3Btn.colors = t3Cols;
        _researchT3Label = MakeText(MakeRect("Label", t3GO, 0f, 0f, W - PAD * 2f, 65f),
            "", new Color(0.85f, 0.70f, 1.0f), 15);
        _researchT3Label.alignment = TextAnchor.MiddleCenter;
        _researchT3Btn.onClick.AddListener(() => {
            if (TechManager.Instance != null && TechManager.Instance.TryUnlockT3())
                RefreshResearchPanel();
        });

        // ── Tier 4 unlock ─────────────────────────────────────────────
        var div4GO = MakeRect("Div4", _researchPanel, PAD, H - PAD - 375f, W - PAD * 2f, 22f);
        var div4   = MakeText(div4GO, "── Tier 4 ──────────────────", new Color(0.65f, 0.30f, 1.0f), 12);
        div4.alignment = TextAnchor.MiddleLeft;

        var t4GO  = MakeRect("T4Btn", _researchPanel, PAD, H - PAD - 450f, W - PAD * 2f, 65f);
        var t4Img = t4GO.AddComponent<Image>();
        t4Img.color = new Color(0.18f, 0.10f, 0.28f, 1f);
        _researchT4Btn = t4GO.AddComponent<Button>();
        _researchT4Btn.targetGraphic = t4Img;
        var t4Cols = _researchT4Btn.colors;
        t4Cols.highlightedColor = new Color(0.28f, 0.15f, 0.42f, 1f);
        t4Cols.pressedColor     = new Color(0.12f, 0.06f, 0.18f, 1f);
        t4Cols.disabledColor    = new Color(0.18f, 0.18f, 0.20f, 1f);
        _researchT4Btn.colors = t4Cols;
        _researchT4Label = MakeText(MakeRect("Label", t4GO, 0f, 0f, W - PAD * 2f, 65f),
            "", new Color(0.80f, 0.55f, 1.0f), 15);
        _researchT4Label.alignment = TextAnchor.MiddleCenter;
        _researchT4Btn.onClick.AddListener(() => {
            if (TechManager.Instance != null && TechManager.Instance.TryUnlockT4())
                RefreshResearchPanel();
        });

        // ── Tier 5 unlock ─────────────────────────────────────────────
        var div5GO = MakeRect("Div5", _researchPanel, PAD, H - PAD - 545f, W - PAD * 2f, 22f);
        var div5   = MakeText(div5GO, "── Tier 5 ──────────────────", new Color(1.0f, 0.20f, 0.60f), 12);
        div5.alignment = TextAnchor.MiddleLeft;

        var t5GO  = MakeRect("T5Btn", _researchPanel, PAD, H - PAD - 620f, W - PAD * 2f, 65f);
        var t5Img = t5GO.AddComponent<Image>();
        t5Img.color = new Color(0.28f, 0.06f, 0.14f, 1f);
        _researchT5Btn = t5GO.AddComponent<Button>();
        _researchT5Btn.targetGraphic = t5Img;
        var t5Cols = _researchT5Btn.colors;
        t5Cols.highlightedColor = new Color(0.42f, 0.10f, 0.22f, 1f);
        t5Cols.pressedColor     = new Color(0.18f, 0.04f, 0.10f, 1f);
        t5Cols.disabledColor    = new Color(0.18f, 0.18f, 0.20f, 1f);
        _researchT5Btn.colors = t5Cols;
        _researchT5Label = MakeText(MakeRect("Label", t5GO, 0f, 0f, W - PAD * 2f, 65f),
            "", new Color(1.0f, 0.55f, 0.75f), 15);
        _researchT5Label.alignment = TextAnchor.MiddleCenter;
        _researchT5Btn.onClick.AddListener(() => {
            if (TechManager.Instance != null && TechManager.Instance.TryUnlockT5())
                RefreshResearchPanel();
        });

        _researchPanel.SetActive(false);
    }

    void BuildTowerPanel(GameObject root)
    {
        const float W     = 820f, H = 320f;
        const float HDR_H = 50f;
        const float PAD   = 16f;
        const float ROW   = 36f;
        const float GAP   = 12f;

        float innerW = W - PAD * 2f;
        float cW     = (innerW - GAP * 2f) / 3f;
        float c0x    = PAD;
        float c1x    = c0x + cW + GAP;
        float c2x    = c1x + cW + GAP;

        // ── Root panel ────────────────────────────────────────────────
        _towerPanel = new GameObject("TowerInfoPanel");
        _towerPanel.transform.SetParent(root.transform, false);
        var rt              = _towerPanel.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta        = new Vector2(W, H);
        var tpBg = _towerPanel.AddComponent<Image>();
        tpBg.color = new Color(0.07f, 0.07f, 0.13f, 0.96f);
        tpBg.raycastTarget = false;

        // ── Shared header ─────────────────────────────────────────────
        var hdrGO = MakeRect("Header", _towerPanel, 0f, H - HDR_H, W, HDR_H);
        hdrGO.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.10f, 1f);
        _tpName = MakeText(MakeRect("Name", hdrGO, PAD, 0f, W - HDR_H - PAD, HDR_H),
            "", new Color(1f, 0.85f, 0.3f), 19, bold: true);
        _tpName.alignment = TextAnchor.MiddleLeft;
        var closeGO = MakeRect("CloseBtn", hdrGO, W - HDR_H, 0f, HDR_H, HDR_H);
        var cImg    = closeGO.AddComponent<Image>(); cImg.color = new Color(0.55f, 0.12f, 0.12f, 1f);
        var cBtn    = closeGO.AddComponent<Button>(); cBtn.targetGraphic = cImg;
        cBtn.onClick.AddListener(() => { _towerPanel.SetActive(false); _selectedTower = null; TowerInfo.OnTowerClickedPublic(null); });
        MakeText(MakeRect("X", closeGO, 0f, 0f, HDR_H, HDR_H), "×", Color.white, 22, bold: true)
            .alignment = TextAnchor.MiddleCenter;

        // ── Main body (3 columns) ─────────────────────────────────────
        _towerPanelBody = new GameObject("TowerBody");
        _towerPanelBody.transform.SetParent(_towerPanel.transform, false);
        var bodyRt = _towerPanelBody.AddComponent<RectTransform>();
        bodyRt.anchorMin = bodyRt.anchorMax = Vector2.zero;
        bodyRt.pivot     = Vector2.zero;
        bodyRt.anchoredPosition = Vector2.zero;
        bodyRt.sizeDelta = new Vector2(W, H);   // full panel size so child coords match

        float topY = H - HDR_H - PAD - ROW;   // bottom of first row in panel-space

        // C0: Balance / BalanceDist / Kills / Aura
        float y = topY;
        _tpBalance     = MakeText(MakeRect("Balance",     _towerPanelBody, c0x, y, cW, ROW), "", new Color(0.6f, 0.9f, 0.6f),   17); y -= ROW;
        _tpBalanceDist = MakeText(MakeRect("BalanceDist", _towerPanelBody, c0x, y, cW, ROW), "", new Color(0.75f, 0.75f, 0.85f), 16); y -= ROW;
        _tpKills       = MakeText(MakeRect("Kills",       _towerPanelBody, c0x, y, cW, ROW), "", new Color(0.95f, 0.5f, 0.5f),  16);
        _tpAura        = MakeText(MakeRect("Aura",        _towerPanelBody, c0x, y - ROW, cW, ROW), "", new Color(0.3f, 1f, 0.45f), 14);

        // C1: Damage / FireRate
        y = topY;
        _tpDamage   = MakeText(MakeRect("Damage",   _towerPanelBody, c1x, y, cW, ROW), "", new Color(0.9f, 0.9f, 0.9f), 17); y -= ROW;
        _tpFireRate = MakeText(MakeRect("FireRate",  _towerPanelBody, c1x, y, cW, ROW), "", new Color(0.9f, 0.9f, 0.9f), 17);

        // C2: Upgrade / Research / TARGET
        y = topY;
        float btnH = 44f;

        // Upgrade button
        var upgGO  = MakeRect("UpgradeBtn", _towerPanelBody, c2x, y - btnH + ROW, cW, btnH);
        var upgImg = upgGO.AddComponent<Image>(); upgImg.color = new Color(0.15f, 0.55f, 0.25f, 1f);
        _tpUpgradeBtn = upgGO.AddComponent<Button>(); _tpUpgradeBtn.targetGraphic = upgImg;
        var uc = _tpUpgradeBtn.colors;
        uc.highlightedColor = new Color(0.2f, 0.7f, 0.3f, 1f);
        uc.pressedColor     = new Color(0.1f, 0.4f, 0.18f, 1f);
        uc.disabledColor    = new Color(0.22f, 0.22f, 0.22f, 1f);
        _tpUpgradeBtn.colors = uc;
        _tpUpgradeBtnLabel = MakeText(MakeRect("Label", upgGO, 0f, 0f, cW, btnH), "UPGRADE", Color.white, 14, bold: true);
        _tpUpgradeBtnLabel.alignment = TextAnchor.MiddleCenter;
        _tpUpgradeBtn.onClick.AddListener(OnUpgradeClicked);

        // Research button (below Upgrade)
        float researchBtnY = y - btnH + ROW - btnH - 6f;
        var resBtnGO  = MakeRect("ResearchBtn", _towerPanelBody, c2x, researchBtnY, cW, btnH);
        var resBtnImg = resBtnGO.AddComponent<Image>(); resBtnImg.color = new Color(0.15f, 0.30f, 0.60f, 1f);
        var resBtn    = resBtnGO.AddComponent<Button>(); resBtn.targetGraphic = resBtnImg;
        var rc = resBtn.colors;
        rc.highlightedColor = new Color(0.2f, 0.4f, 0.8f, 1f);
        rc.pressedColor     = new Color(0.1f, 0.2f, 0.45f, 1f);
        resBtn.colors = rc;
        MakeText(MakeRect("Label", resBtnGO, 0f, 0f, cW, btnH), "RESEARCH", Color.white, 14, bold: true)
            .alignment = TextAnchor.MiddleCenter;
        resBtn.onClick.AddListener(ShowResearchBody);

        // Sell button (below Research)
        float sellBtnY = researchBtnY - btnH - 6f;
        var sellGO  = MakeRect("SellBtn", _towerPanelBody, c2x, sellBtnY, cW, btnH);
        var sellImg = sellGO.AddComponent<Image>(); sellImg.color = new Color(0.55f, 0.10f, 0.10f, 1f);
        _tpSellBtn  = sellGO.AddComponent<Button>(); _tpSellBtn.targetGraphic = sellImg;
        var sc = _tpSellBtn.colors;
        sc.highlightedColor = new Color(0.75f, 0.15f, 0.15f, 1f);
        sc.pressedColor     = new Color(0.35f, 0.06f, 0.06f, 1f);
        _tpSellBtn.colors = sc;
        _tpSellBtnLabel = MakeText(MakeRect("Label", sellGO, 0f, 0f, cW, btnH), "SELL", Color.white, 14, bold: true);
        _tpSellBtnLabel.alignment = TextAnchor.MiddleCenter;
        _tpSellBtn.onClick.AddListener(() =>
        {
            if (_selectedTower == null) return;
            _selectedTower.Sell(_rm);
            _towerPanel.SetActive(false);
            _selectedTower = null;
        });

        // TARGET section pinned to bottom
        float tgtBtnY = PAD;
        float tgtTop  = tgtBtnY + 36f + 6f;
        MakeText(MakeRect("TargetLabel", _towerPanelBody, c2x, tgtTop, cW, 22f),
            "TARGET", new Color(0.6f, 0.6f, 0.7f), 11, bold: true);

        string[] modeLabels = { "Furthest", "Closest", "Lowest" };
        float mBtnW = (cW - 8f) / 3f;
        _tpTargetBtns = new Button[3];
        _tpTargetImgs = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            float xOff = c2x + i * (mBtnW + 4f);
            var bGO  = MakeRect($"Target_{modeLabels[i]}", _towerPanelBody, xOff, tgtBtnY, mBtnW, 36f);
            var bImg = bGO.AddComponent<Image>(); bImg.color = new Color(0.18f, 0.18f, 0.28f, 1f);
            var btn  = bGO.AddComponent<Button>(); btn.targetGraphic = bImg;
            MakeText(MakeRect("L", bGO, 0f, 0f, mBtnW, 36f), modeLabels[i], Color.white, 11, bold: true)
                .alignment = TextAnchor.MiddleCenter;
            int idx = i;
            btn.onClick.AddListener(() => SetTargetingMode((TargetingMode)idx));
            _tpTargetBtns[i] = btn;
            _tpTargetImgs[i] = bImg;
        }

        // ── Research body (replaces 3-col content) ────────────────────
        _towerResearchBody = new GameObject("ResearchBody");
        _towerResearchBody.transform.SetParent(_towerPanel.transform, false);
        var rbRt = _towerResearchBody.AddComponent<RectTransform>();
        rbRt.anchorMin = rbRt.anchorMax = Vector2.zero;
        rbRt.pivot     = Vector2.zero;
        rbRt.anchoredPosition = Vector2.zero;
        rbRt.sizeDelta = new Vector2(W, H);   // full panel size so child coords match

        // Back button
        float backBtnY = H - HDR_H - PAD - btnH;
        var backGO  = MakeRect("BackBtn", _towerResearchBody, PAD, backBtnY, 110f, btnH);
        var backImg = backGO.AddComponent<Image>(); backImg.color = new Color(0.25f, 0.25f, 0.35f, 1f);
        var backBtn = backGO.AddComponent<Button>(); backBtn.targetGraphic = backImg;
        MakeText(MakeRect("L", backGO, 0f, 0f, 110f, btnH), "← Back", Color.white, 13, bold: true)
            .alignment = TextAnchor.MiddleCenter;
        backBtn.onClick.AddListener(HideResearchBody);

        _towerResearchBody.SetActive(false);
        _towerPanel.SetActive(false);
    }

    void ShowResearchBody()
    {
        if (_selectedTower == null) return;
        _towerPanelBody?.SetActive(false);
        _towerResearchBody.SetActive(true);
        RefreshResearchBody();
    }

    void HideResearchBody()
    {
        _towerResearchBody.SetActive(false);
        _towerPanelBody?.SetActive(true);
    }

    void RefreshResearchBody()
    {
        if (_selectedTower == null || ResearchManager.Instance == null) return;

        // Destroy old research buttons
        foreach (var (btn, lbl, def) in _trButtons)
            if (btn != null) Destroy(btn.gameObject);
        _trButtons.Clear();

        var researches = ResearchManager.Instance.GetForTower(_selectedTower.definitionId);

        const float PAD  = 16f;
        const float BTNW = 260f;
        const float BTNH = 52f;
        const float GAP  = 10f;
        float y = 270f - BTNH;    // start near top of body area

        foreach (var def in researches)
        {
            bool purchased = ResearchManager.Instance.IsPurchased(def.id);
            var bGO  = MakeRect($"Research_{def.id}", _towerResearchBody, PAD + 120f, y, BTNW, BTNH);
            var bImg = bGO.AddComponent<Image>();
            bImg.color = purchased ? new Color(0.18f, 0.18f, 0.18f, 1f) : new Color(0.14f, 0.35f, 0.55f, 1f);
            var btn  = bGO.AddComponent<Button>(); btn.targetGraphic = bImg;
            btn.interactable = !purchased;

            string label = purchased
                ? $"{def.displayName}\n(Purchased)"
                : $"{def.displayName}\n{def.description}   [{def.techCost} Tech]";
            var lbl = MakeText(MakeRect("L", bGO, 6f, 0f, BTNW - 8f, BTNH), label, Color.white, 13);
            lbl.alignment = TextAnchor.MiddleLeft;
            lbl.lineSpacing = 1.1f;

            var capDef = def;
            btn.onClick.AddListener(() =>
            {
                ResearchManager.Instance.TryPurchase(capDef);
                RefreshResearchBody();
                RefreshTowerPanel(_selectedTower);
            });

            _trButtons.Add((btn, lbl, def));
            y -= BTNH + GAP;
        }
    }

    // Absolute-positioned rect child (x,y from bottom-left of parent)
    static GameObject MakeRect(string name, GameObject parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt          = go.AddComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0f, 0f);
        rt.anchorMax    = new Vector2(0f, 0f);
        rt.pivot        = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta    = new Vector2(w, h);
        return go;
    }

    Text MakeText(GameObject go, string text, Color color, int size, bool bold = false)
    {
        var txt            = go.AddComponent<Text>();
        txt.text           = text;
        txt.color          = color;
        txt.font           = GetFont();
        txt.fontSize       = size;
        txt.fontStyle      = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.alignment      = TextAnchor.MiddleLeft;
        txt.raycastTarget  = false;   // labels must not steal button clicks
        return txt;
    }

    // ── Enemy info panel (same anchor as tower panel) ─────────────────

    void BuildEnemyPanel(GameObject root)
    {
        const float W     = 500f, H = 360f;
        const float HDR_H = 50f;
        const float PAD   = 16f;
        const float ROW   = 36f;

        _enemyPanel = new GameObject("EnemyInfoPanel");
        _enemyPanel.transform.SetParent(root.transform, false);
        var rt              = _enemyPanel.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta        = new Vector2(W, H);
        _enemyPanel.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.13f, 0.96f);

        // Header
        var hdrGO = MakeRect("Header", _enemyPanel, 0f, H - HDR_H, W, HDR_H);
        hdrGO.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.10f, 1f);
        _epName = MakeText(MakeRect("Name", hdrGO, PAD, 0f, W - HDR_H - PAD, HDR_H),
            "", new Color(0.95f, 0.4f, 0.4f, 1f), 19, bold: true);
        _epName.alignment = TextAnchor.MiddleLeft;
        var closeGO = MakeRect("CloseBtn", hdrGO, W - HDR_H, 0f, HDR_H, HDR_H);
        var cImg    = closeGO.AddComponent<Image>(); cImg.color = new Color(0.55f, 0.12f, 0.12f, 1f);
        var cBtn    = closeGO.AddComponent<Button>(); cBtn.targetGraphic = cImg;
        cBtn.onClick.AddListener(() => { _enemyPanel.SetActive(false); _selectedEnemy = null; });
        MakeText(MakeRect("X", closeGO, 0f, 0f, HDR_H, HDR_H), "×", Color.white, 22, bold: true)
            .alignment = TextAnchor.MiddleCenter;

        // Stats body — two columns
        float colW = (W - PAD * 3f) * 0.5f;
        float y    = H - HDR_H - PAD - ROW;
        _epHp         = MakeText(MakeRect("HP",         _enemyPanel, PAD,          y, colW, ROW), "", Color.white, 17);
        _epArmor      = MakeText(MakeRect("Armor",      _enemyPanel, PAD + colW + PAD, y, colW, ROW), "", new Color(0.8f, 0.8f, 0.8f, 1f), 17); y -= ROW + 4f;
        _epSpeed      = MakeText(MakeRect("Speed",      _enemyPanel, PAD,          y, colW, ROW), "", Color.white, 17);
        _epResistance = MakeText(MakeRect("Resistance", _enemyPanel, PAD + colW + PAD, y, colW, ROW), "", new Color(0.4f, 0.85f, 1f, 1f), 17);  y -= ROW + 4f;
        _epDeathBlow  = MakeText(MakeRect("DeathBlow",  _enemyPanel, PAD,              y, colW, ROW), "", new Color(0.95f, 0.4f, 0.4f, 1f), 17);
        _epFortitude  = MakeText(MakeRect("Fortitude",  _enemyPanel, PAD + colW + PAD, y, colW, ROW), "", new Color(0.85f, 0.6f, 1f, 1f), 17);
        // Description — fixed to panel bottom
        const float DESC_H = 72f;
        var divGO = MakeRect("Divider", _enemyPanel, PAD, PAD + DESC_H + 6f, W - PAD * 2f, 1f);
        divGO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        _epDescription = MakeText(MakeRect("Desc", _enemyPanel, PAD, PAD, W - PAD * 2f, DESC_H), "", new Color(0.75f, 0.75f, 0.75f, 1f), 14);
        _epDescription.alignment          = TextAnchor.UpperLeft;
        _epDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
        _epDescription.verticalOverflow   = VerticalWrapMode.Overflow;

        _enemyPanel.SetActive(false);
    }

    // ── Debug spawn panel (bottom-left) ──────────────────────────────

    void BuildDebugPanel(GameObject root)
    {
        const float BTN_W = 110f, BTN_H = 28f, PAD = 6f;

        var (ids, labels, colors) = (
            new[] { "basic_enemy", "fast_enemy", "armored_enemy", "boss_enemy", "resilient_enemy", "splitter_enemy",
                    "phantom_enemy", "charger_enemy", "priest_enemy", "shielder_enemy" },
            new[] { "Basic",  "Fast",    "Armored", "Boss",    "Resilient", "Splitter",
                    "Phantom", "Charger", "Priest",  "Shielder" },
            new Color[]
            {
                new Color(0.65f, 0.15f, 0.15f, 1f),
                new Color(0.65f, 0.60f, 0.10f, 1f),
                new Color(0.35f, 0.35f, 0.40f, 1f),
                new Color(0.55f, 0.10f, 0.55f, 1f),
                new Color(0.10f, 0.50f, 0.75f, 1f),
                new Color(0.70f, 0.28f, 0.05f, 1f),
                new Color(0.55f, 0.30f, 0.85f, 1f),
                new Color(0.80f, 0.25f, 0.05f, 1f),
                new Color(0.75f, 0.70f, 0.10f, 1f),
                new Color(0.15f, 0.45f, 0.80f, 1f),
            }
        );

        var panel = new GameObject("DebugSpawnPanel");
        panel.transform.SetParent(root.transform, false);
        var rt          = panel.AddComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0f, 0f);
        rt.anchorMax    = new Vector2(0f, 0f);
        rt.pivot        = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(PAD, PAD);
        rt.sizeDelta    = new Vector2(BTN_W + PAD * 2f, Mathf.Min(ids.Length, 10) * (BTN_H + PAD) + PAD);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        for (int i = 0; i < ids.Length; i++)
        {
            float y = rt.sizeDelta.y - PAD - (i + 1) * (BTN_H + PAD) + PAD;
            var bGO  = MakeRect($"Debug_{ids[i]}", panel, PAD, y, BTN_W, BTN_H);
            var bImg = bGO.AddComponent<Image>();
            bImg.color = colors[i];
            var btn  = bGO.AddComponent<Button>();
            btn.targetGraphic = bImg;
            MakeText(MakeRect("L", bGO, 0f, 0f, BTN_W, BTN_H), labels[i], Color.white, 11, bold: true)
                .alignment = TextAnchor.MiddleCenter;

            string unitId = ids[i];
            btn.onClick.AddListener(() => DebugSpawnUnit(unitId));
        }
    }

    void DebugSpawnUnit(string unitId)
    {
        if (UnitFactory.Instance == null || PathGraph.Instance == null) return;

        // Find spawner with pathIndex == 1
        UnitSpawner spawner = null;
        foreach (var s in FindObjectsByType<UnitSpawner>(FindObjectsSortMode.None))
        {
            if (s.pathIndex == 0) { spawner = s; break; }
        }
        if (spawner == null) return;

        var go = UnitFactory.Instance.Build(unitId, spawner.transform.position);
        if (go == null) return;

        var unit = go.GetComponent<UnitManager>();
        if (unit != null && spawner.headNode != null)
        {
            var route = PathGraph.Instance.BuildRoute(spawner.headNode);
            unit.AssignRoute(route);
            WaveManager.Instance?.RegisterUnit(unit);
        }
    }

    // ── Top bar ───────────────────────────────────────────────────────

    void BuildTopBar(GameObject root)
    {
        const float BAR_H  = 100f;
        const float TOP_H  = 50f;   // gold / wave / lives row height
        const float ROW_H  = 17f;   // each balance row height
        const float LEFT   = 20f;   // left padding

        var bar = new GameObject("TopBar");
        bar.transform.SetParent(root.transform, false);

        var rt = bar.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, BAR_H);

        var barBg = bar.AddComponent<Image>();
        barBg.color = C_BarBg;
        barBg.raycastTarget = false;

        // Top row: Gold | Wave | Lives  (anchored to top, fixed height TOP_H)
        _goldText  = AddAbsLabel(bar, "Gold",  LEFT,          BAR_H - TOP_H, 220f, TOP_H,
            "⚡  0",  C_Gold,  24, TextAnchor.MiddleLeft);
        _techText  = AddAbsLabel(bar, "Tech",  LEFT + 220f,   BAR_H - TOP_H, 120f, TOP_H,
            "🔬  0", new Color(0.35f, 1f, 0.55f), 22, TextAnchor.MiddleLeft);

        // "RESEARCH" button — sits right of the tech count, opens the research panel
        var rBtnGO  = new GameObject("ResearchBtn");
        rBtnGO.transform.SetParent(bar.transform, false);
        var rBtnRT  = rBtnGO.AddComponent<RectTransform>();
        rBtnRT.anchorMin = new Vector2(0f, 0f);
        rBtnRT.anchorMax = new Vector2(0f, 0f);
        rBtnRT.pivot     = new Vector2(0f, 0f);
        rBtnRT.offsetMin = new Vector2(LEFT + 345f, BAR_H - TOP_H + 8f);
        rBtnRT.offsetMax = new Vector2(LEFT + 475f, BAR_H - 8f);
        var rBtnImg  = rBtnGO.AddComponent<Image>();
        rBtnImg.color = new Color(0.12f, 0.30f, 0.16f, 1f);
        var rBtn     = rBtnGO.AddComponent<Button>();
        rBtn.targetGraphic = rBtnImg;
        var rCols = rBtn.colors;
        rCols.highlightedColor = new Color(0.18f, 0.42f, 0.22f, 1f);
        rCols.pressedColor     = new Color(0.08f, 0.18f, 0.10f, 1f);
        rBtn.colors = rCols;
        var rLabelGO = new GameObject("Label");
        rLabelGO.transform.SetParent(rBtnGO.transform, false);
        var rLabelRT = rLabelGO.AddComponent<RectTransform>();
        rLabelRT.anchorMin = Vector2.zero; rLabelRT.anchorMax = Vector2.one;
        rLabelRT.offsetMin = Vector2.zero; rLabelRT.offsetMax = Vector2.zero;
        var rLabelTxt = MakeText(rLabelGO, "RESEARCH", Color.white, 13, bold: true);
        rLabelTxt.alignment = TextAnchor.MiddleCenter;
        rBtn.onClick.AddListener(() =>
        {
            if (_researchPanel != null)
                _researchPanel.SetActive(!_researchPanel.activeSelf);
        });
        _waveText  = AddAbsLabel(bar, "Wave",  0f, BAR_H - TOP_H, 0f,   TOP_H,
            "Ready", C_Wave,  22, TextAnchor.MiddleCenter, stretchX: true);
        _livesText = AddAbsLabel(bar, "Lives", 0f, BAR_H - TOP_H, 500f, TOP_H,
            "♥  20", C_Lives, 24, TextAnchor.MiddleRight,  fromRight: true);

        // Wave button embedded in top bar — centered, second row (below gold/lives)
        var wBtnGO = new GameObject("WaveButton");
        wBtnGO.transform.SetParent(bar.transform, false);
        var wBtnRT       = wBtnGO.AddComponent<RectTransform>();
        wBtnRT.anchorMin = new Vector2(0.5f, 0f);
        wBtnRT.anchorMax = new Vector2(0.5f, 0f);
        wBtnRT.pivot     = new Vector2(0.5f, 0f);
        wBtnRT.offsetMin = new Vector2(-200f, 5f);
        wBtnRT.offsetMax = new Vector2( 200f, BAR_H - TOP_H - 5f);
        var wBtnImg      = wBtnGO.AddComponent<Image>();
        wBtnImg.color    = C_BtnReady;
        _waveButton      = wBtnGO.AddComponent<Button>();
        _waveButton.targetGraphic = wBtnImg;
        var wCols = _waveButton.colors;
        wCols.normalColor      = C_BtnReady;
        wCols.highlightedColor = C_BtnReady + new Color(0.1f, 0.1f, 0.1f, 0f);
        wCols.pressedColor     = C_BtnReady - new Color(0.1f, 0.1f, 0.1f, 0f);
        wCols.disabledColor    = C_BtnWait;
        _waveButton.colors     = wCols;
        _waveButton.onClick.AddListener(() => {
            var wm = WaveManager.Instance;
            if (wm == null) return;
            wm.AutoStartCountdown = -1f;
            wm.StartNextWave();
        });
        _waveButtonLabel = AddLabel(wBtnGO, "Label", Vector2.zero, Vector2.one,
            "START WAVE 1", Color.white, 18, TextAnchor.MiddleCenter, bold: true);

        // Balance rows stacked under gold, left-aligned
        Color cE = new Color(0.4f, 0.85f, 0.5f);
        Color cA = new Color(0.7f, 0.5f,  1.0f);
        Color cP = new Color(0.9f, 0.55f, 0.3f);
        Color cD = new Color(1.0f, 0.85f, 0.3f);

        float y2 = BAR_H - TOP_H - ROW_H;         // E row top-Y from bar bottom
        float y1 = BAR_H - TOP_H - ROW_H * 2f;    // A row
        float y0 = BAR_H - TOP_H - ROW_H * 3f;    // P row

        _hdrElem      = AddAbsLabel(bar, "Elem",      LEFT,        y2, 160f, ROW_H, "E  0.00",  cE, 13, TextAnchor.MiddleLeft);
        _hdrElemBonus = AddAbsLabel(bar, "ElemBonus", LEFT + 160f, y2, 180f, ROW_H, "+0% orbs", cE, 13, TextAnchor.MiddleLeft);
        _hdrArc       = AddAbsLabel(bar, "Arc",       LEFT,        y1, 160f, ROW_H, "A  0.00",  cA, 13, TextAnchor.MiddleLeft);
        _hdrArcBonus  = AddAbsLabel(bar, "ArcBonus",  LEFT + 160f, y1, 180f, ROW_H, "+0% sci",  cA, 13, TextAnchor.MiddleLeft);
        _hdrPhys      = AddAbsLabel(bar, "Phys",      LEFT,        y0, 160f, ROW_H, "P  0.00",  cP, 13, TextAnchor.MiddleLeft);
        _hdrPhysDrop  = AddAbsLabel(bar, "PhysDrop",  LEFT + 160f, y0, 180f, ROW_H, "15% + 0%", cD, 13, TextAnchor.MiddleLeft);
    }

    // Places a label at an absolute pixel position within parent (anchor bottom-left).
    Text AddAbsLabel(GameObject parent, string goName,
        float x, float y, float w, float h,
        string content, Color color, int size, TextAnchor anchor,
        bool stretchX = false, bool fromRight = false)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();

        if (stretchX)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.offsetMin = new Vector2(0f, y);
            rt.offsetMax = new Vector2(0f, y + h);
        }
        else if (fromRight)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.offsetMin = new Vector2(-w, y);
            rt.offsetMax = new Vector2(0f, y + h);
        }
        else
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.offsetMin = new Vector2(x, y);
            rt.offsetMax = new Vector2(x + w, y + h);
        }

        var txt = go.AddComponent<Text>();
        txt.text          = content;
        txt.color         = color;
        txt.font          = GetFont();
        txt.fontSize      = size;
        txt.alignment     = anchor;
        txt.raycastTarget = false;
        return txt;
    }

    // ── Overlay builder ───────────────────────────────────────────────

    GameObject BuildOverlay(GameObject root, string goName,
        string title, Color titleColor, string btnText,
        UnityEngine.Events.UnityAction onBtn)
    {
        var panel = new GameObject(goName);
        panel.transform.SetParent(root.transform, false);

        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        panel.AddComponent<Image>().color = C_OverlayBg;

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panel.transform, false);
        var tRT = titleGO.AddComponent<RectTransform>();
        tRT.anchorMin        = new Vector2(0.5f, 0.5f);
        tRT.anchorMax        = new Vector2(0.5f, 0.5f);
        tRT.pivot            = new Vector2(0.5f, 0.5f);
        tRT.anchoredPosition = new Vector2(0f, 80f);
        tRT.sizeDelta        = new Vector2(900f, 120f);

        var tTxt = titleGO.AddComponent<Text>();
        tTxt.text      = title;
        tTxt.color     = titleColor;
        tTxt.font      = GetFont();
        tTxt.fontSize  = 80;
        tTxt.fontStyle = FontStyle.Bold;
        tTxt.alignment = TextAnchor.MiddleCenter;

        // Subtitle
        var subGO = new GameObject("Sub");
        subGO.transform.SetParent(panel.transform, false);
        var sRT = subGO.AddComponent<RectTransform>();
        sRT.anchorMin        = new Vector2(0.5f, 0.5f);
        sRT.anchorMax        = new Vector2(0.5f, 0.5f);
        sRT.pivot            = new Vector2(0.5f, 0.5f);
        sRT.anchoredPosition = new Vector2(0f, 10f);
        sRT.sizeDelta        = new Vector2(700f, 60f);

        var subTxt = subGO.AddComponent<Text>();
        subTxt.text      = goName == "GameOverPanel" ? "Your base has been breached." : "All waves repelled.";
        subTxt.color     = new Color(0.8f, 0.8f, 0.85f, 1f);
        subTxt.font      = GetFont();
        subTxt.fontSize  = 28;
        subTxt.alignment = TextAnchor.MiddleCenter;

        // Button
        var bGO = new GameObject("RestartButton");
        bGO.transform.SetParent(panel.transform, false);
        var bRT = bGO.AddComponent<RectTransform>();
        bRT.anchorMin        = new Vector2(0.5f, 0.5f);
        bRT.anchorMax        = new Vector2(0.5f, 0.5f);
        bRT.pivot            = new Vector2(0.5f, 0.5f);
        bRT.anchoredPosition = new Vector2(0f, -80f);
        bRT.sizeDelta        = new Vector2(280f, 62f);

        var bImg = bGO.AddComponent<Image>();
        bImg.color = C_BtnRestart;
        var btn = bGO.AddComponent<Button>();
        btn.targetGraphic = bImg;
        btn.onClick.AddListener(onBtn);

        AddLabel(bGO, "Label", Vector2.zero, Vector2.one,
            btnText, Color.white, 26, TextAnchor.MiddleCenter, bold: true);

        return panel;
    }

    // ── Update ────────────────────────────────────────────────────────

    void Update()
    {
        BalanceManager.Instance?.Recalculate();

        // Selection — click towers or enemies
        if (Input.GetMouseButtonDown(0) &&
            !EventSystem.current.IsPointerOverGameObject() &&
            !(TowerPlacer.Instance != null && TowerPlacer.Instance.IsPlacing) &&
            Camera.main != null)
        {
            Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // Towers
            TowerInfo best = null;
            float bestDist = float.MaxValue;
            foreach (var ti in FindObjectsByType<TowerInfo>(FindObjectsSortMode.None))
            {
                if (ti.isGhost) continue;
                float dist = Vector2.Distance(mouse, ti.transform.position);
                if (dist <= ti.ClickRadius && dist < bestDist) { best = ti; bestDist = dist; }
            }

            if (best != null)
            {
                TowerInfo.OnTowerClickedPublic(best);
            }
            else
            {
                // Enemies — pick closest within collider radius
                UnitManager bestUnit = null;
                float bestUDist = 0.6f;   // max pick radius in world units
                foreach (var um in FindObjectsByType<UnitManager>(FindObjectsSortMode.None))
                {
                    if (!um.isAlive) continue;
                    float dist = Vector2.Distance(mouse, um.transform.position);
                    if (dist < bestUDist) { bestUnit = um; bestUDist = dist; }
                }

                if (bestUnit != null)
                    SelectEnemy(bestUnit);
                else
                    TowerInfo.OnTowerClickedPublic(null);   // deselect everything
            }
        }

        // Rotate selected tower base with Q / E
        if (_selectedTower != null && (TowerPlacer.Instance == null || !TowerPlacer.Instance.IsPlacing))
        {
            const float ROT_SPEED = 90f;
            float rot = 0f;
            if (Input.GetKey(KeyCode.Q)) rot =  ROT_SPEED * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) rot = -ROT_SPEED * Time.deltaTime;
            if (rot != 0f)
                _selectedTower.transform.Rotate(0f, 0f, rot);
        }

        // Keep tower upgrade button live (gold/research state can change any frame)
        if (_selectedTower != null && _tpUpgradeBtn != null)
        {
            bool canTier  = _selectedTower.CanUpgrade;
            bool hasRes   = _selectedTower.HasResearchForUpgrade;
            bool hasGold  = _rm != null && _rm.resourceOne >= _selectedTower.UpgradeCost;
            _tpUpgradeBtn.interactable = canTier && hasRes && hasGold;
            if (_tpUpgradeBtnLabel != null)
            {
                if (!canTier)
                    _tpUpgradeBtnLabel.text = "MAX TIER";
                else if (!hasRes)
                    _tpUpgradeBtnLabel.text = $"LOCKED  (needs Tier {_selectedTower.RequiredResearchTier} research)";
                else
                    _tpUpgradeBtnLabel.text = $"UPGRADE  Tier {_selectedTower.Tier + 1}  —  {_selectedTower.UpgradeCost}g";
            }
        }

        // Keep enemy panel live while an enemy is selected
        if (_selectedEnemy != null)
        {
            if (!_selectedEnemy.isAlive)
            {
                _selectedEnemy = null;
                _enemyPanel?.SetActive(false);
            }
            else
            {
                RefreshEnemyPanel();
            }
        }

        var wm = WaveManager.Instance;

        // Gold
        if (_goldText != null && _rm != null)
            _goldText.text = $"⚡  {_rm.resourceOne}";

        if (_techText != null)
            _techText.text = $"🔬  {TechManager.Instance?.Tech ?? 0}";

        // Lives
        if (_livesText != null && _lm != null)
            _livesText.text = $"♥  {Mathf.Max(0, (int)_lm.lives)}";

        // Wave label
        if (_waveText != null && wm != null)
        {
            if (wm.IsGameOver || wm.IsVictory)
                _waveText.text = string.Empty;
            else if (wm.CurrentWave == 0)
                _waveText.text = "Prep Phase";
            else if (wm.IsWaveActive)
                _waveText.text = $"Wave  {wm.CurrentWave} / {wm.TotalWaves}";
            else if (wm.CurrentWave >= wm.TotalWaves)
                _waveText.text = $"Wave  {wm.CurrentWave} / {wm.TotalWaves}  —  Cleared";
            else if (wm.IsCountingDown)
                _waveText.text = $"Wave  {wm.CurrentWave} / {wm.TotalWaves}  —  Next in {Mathf.CeilToInt(wm.AutoStartCountdown)}s";
            else
                _waveText.text = $"Wave  {wm.CurrentWave} / {wm.TotalWaves}  —  Prep";
        }

        // Wave button
        if (_waveButton != null && wm != null)
        {
            bool canSend = wm.CanStartWave || wm.IsCountingDown;
            _waveButton.interactable = canSend;

            if (_waveButtonLabel != null)
            {
                int next = wm.CurrentWave + 1;
                _waveButtonLabel.text = wm.IsWaveActive
                    ? "Wave in progress..."
                    : wm.IsCountingDown
                        ? $"SEND NOW  ({Mathf.CeilToInt(wm.AutoStartCountdown)}s)"
                        : wm.CurrentWave == 0
                            ? "START WAVE 1"
                            : $"SEND WAVE {next}";
            }
        }

        // Overlays
        if (wm != null)
        {
            if (_gameOverPanel != null) _gameOverPanel.SetActive(wm.IsGameOver);
            if (_victoryPanel  != null) _victoryPanel.SetActive(wm.IsVictory);
        }

        // Header balance (always live)
        RefreshHeaderBalance();

        // Research panel live refresh (updates button state as tech changes)
        if (_researchPanel != null && _researchPanel.activeSelf)
            RefreshResearchPanel();

        // Tower panel live refresh (upgrade button affordability and kill count update each frame)
        if (_selectedTower != null)
            RefreshTowerPanel(_selectedTower);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    Font _font;
    Font GetFont()
    {
        if (_font != null) return _font;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _font;
    }

    Text AddLabel(GameObject parent, string goName,
        Vector2 anchorMin, Vector2 anchorMax,
        string content, Color color, int size,
        TextAnchor anchor,
        float leftPad = 0f, float rightPad = 0f,
        bool bold = false)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(leftPad, 0f);
        rt.offsetMax = new Vector2(-rightPad, 0f);

        var txt = go.AddComponent<Text>();
        txt.text      = content;
        txt.color     = color;
        txt.font      = GetFont();
        txt.fontSize  = size;
        txt.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.alignment = anchor;

        return txt;
    }
}
