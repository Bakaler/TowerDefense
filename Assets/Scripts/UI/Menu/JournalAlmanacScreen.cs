using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The almanac itself: a card grid over every tower or unit definition.
/// One screen instance serves both modes — SetMode before pushing.
/// Towers get tier / balance-type filter chips; clicking any card opens a
/// detail popup with the full description. All entries render as discovered
/// for now (JournalCardFactory.SetDiscovered carries the future gating).
/// </summary>
public class JournalAlmanacScreen : MenuScreen
{
    public enum Mode { Towers, Enemies }

    Mode _mode = Mode.Towers;
    Mode? _builtMode;
    GameObject _panel;
    Text       _title;
    GameObject _gridContent;
    GameObject _detailPopup;

    // ── Tower filters ─────────────────────────────────────────────────
    GameObject _chipRow;
    bool       _chipsBuilt;
    int        _tierFilter;          // 0 = all
    string     _typeFilter;          // null = all
    readonly List<(Image img, System.Func<bool> isActive)> _chips = new();

    static readonly Color ChipIdle   = new Color(0.14f, 0.16f, 0.22f, 1f);
    static readonly Color ChipActive = new Color(0.25f, 0.45f, 0.75f, 1f);

    public void SetMode(Mode mode) => _mode = mode;

    protected override GameObject Build(GameObject canvasRoot)
    {
        _panel = UIControlFactory.Rect("JournalAlmanacScreen", canvasRoot, 0f, 0f, 1920f, 1080f);

        _title = UIControlFactory.Label(_panel, "Title", 0f, 430f, 800f, 70f,
            "", UIControlFactory.TitleColor, 48, TextAnchor.MiddleCenter, bold: true);

        _chipRow = UIControlFactory.Rect("Chips", _panel, 0f, 360f, 1400f, 44f);

        _gridContent = UIScrollListFactory.Grid(_panel, "Grid", 0f, -50f, 1360f, 680f,
            JournalCardFactory.CardSize, spacing: 18f);

        var (backBtn, _) = UIControlFactory.Button(_panel, "BackBtn", 0f, -470f, 220f, 52f,
            new Color(0.28f, 0.12f, 0.12f, 1f), "BACK", 22);
        backBtn.onClick.AddListener(() => Controller.Back());

        return _panel;
    }

    protected override void Refresh()
    {
        _title.text = _mode == Mode.Towers ? "TOWER ALMANAC" : "ENEMY ALMANAC";
        CloseDetail();

        _chipRow.SetActive(_mode == Mode.Towers);
        if (_mode == Mode.Towers && !_chipsBuilt) BuildChips();

        if (_builtMode == _mode) return;
        _builtMode = _mode;
        RebuildGrid();
    }

    // ── Filter chips ──────────────────────────────────────────────────

    void BuildChips()
    {
        var lib = TowerDefinitionLibrary.Instance;
        if (lib == null) return;
        _chipsBuilt = true;

        // Distinct tiers and balance types straight from the data
        var tiers = new SortedSet<int>();
        var types = new SortedSet<string>();
        foreach (var def in lib.All.Values)
        {
            tiers.Add(def.towerTier);
            if (!string.IsNullOrEmpty(def.balanceType)) types.Add(def.balanceType);
        }

        var entries = new List<(string label, System.Action onClick, System.Func<bool> isActive)>
        {
            ("ALL", () => { _tierFilter = 0; _typeFilter = null; },
                    () => _tierFilter == 0 && _typeFilter == null),
        };
        foreach (int t in tiers)
        {
            int tier = t;
            entries.Add(($"T{tier}", () => _tierFilter = _tierFilter == tier ? 0 : tier,
                                     () => _tierFilter == tier));
        }
        foreach (string ty in types)
        {
            string type = ty;
            entries.Add((type, () => _typeFilter = _typeFilter == type ? null : type,
                               () => _typeFilter == type));
        }

        const float CHIP_H = 40f, GAP = 10f;
        float chipW = Mathf.Min(150f, (1400f - GAP * (entries.Count - 1)) / entries.Count);
        float totalW = entries.Count * chipW + (entries.Count - 1) * GAP;
        float x = -totalW * 0.5f + chipW * 0.5f;

        foreach (var (label, onClick, isActive) in entries)
        {
            var (btn, _) = UIControlFactory.Button(_chipRow, $"Chip_{label}", x, 0f, chipW, CHIP_H,
                ChipIdle, label, 17);
            _chips.Add((btn.image, isActive));
            btn.onClick.AddListener(() =>
            {
                onClick();
                RefreshChipColors();
                RebuildGrid();
            });
            x += chipW + GAP;
        }
        RefreshChipColors();
    }

    void RefreshChipColors()
    {
        foreach (var (img, isActive) in _chips)
            img.color = isActive() ? ChipActive : ChipIdle;
    }

    // ── Grid ──────────────────────────────────────────────────────────

    void RebuildGrid()
    {
        CloseDetail();
        UIScrollListFactory.Clear(_gridContent);
        if (_mode == Mode.Towers) BuildTowerCards();
        else                      BuildEnemyCards();
    }

    void BuildTowerCards()
    {
        var lib = TowerDefinitionLibrary.Instance;
        if (lib == null) { WarnMissingLibrary("TowerDefinitionLibrary"); return; }

        foreach (var def in SortedByTier(lib.All.Values))
        {
            if (_tierFilter != 0 && def.towerTier != _tierFilter) continue;
            if (_typeFilter != null && def.balanceType != _typeFilter) continue;

            var captured = def;
            JournalCardFactory.Create(_gridContent, def.displayName ?? def.id,
                TowerBaseSprite(def), TowerStats(def),
                overlaySprite: TowerTurretSprite(def),
                tint: def.tintColor,
                onClick: () => ShowDetail(
                    captured.displayName ?? captured.id,
                    TowerBaseSprite(captured), TowerTurretSprite(captured), captured.tintColor,
                    string.Join("\n", TowerStats(captured)), DescriptionTags.Resolve(captured.description)));
        }
    }

    void BuildEnemyCards()
    {
        var lib = UnitDefinitionLibrary.Instance;
        if (lib == null) { WarnMissingLibrary("UnitDefinitionLibrary"); return; }

        foreach (var def in lib.All.Values)
        {
            var captured = def;
            JournalCardFactory.Create(_gridContent, def.displayName ?? def.id,
                UnitSprite(def), EnemyStats(def), tint: def.tintColor,
                onClick: () => ShowDetail(
                    captured.displayName ?? captured.id,
                    UnitSprite(captured), null, captured.tintColor,
                    string.Join("\n", EnemyStats(captured)), DescriptionTags.Resolve(captured.description)));
        }
    }

    // ── Detail popup ──────────────────────────────────────────────────

    void ShowDetail(string entryName, Sprite sprite, Sprite overlay, Color tint,
        string statsText, string description)
    {
        CloseDetail();

        var content = UIControlFactory.Popup(_panel, "DetailPopup", entryName.ToUpper(),
            860f, 560f, out var closeBtn);
        _detailPopup = content.transform.parent.parent.gameObject;   // full-screen blocker
        closeBtn.onClick.AddListener(CloseDetail);

        // Clicking the dim area outside the frame also closes
        var blockerBtn = _detailPopup.AddComponent<Button>();
        blockerBtn.targetGraphic = _detailPopup.GetComponent<Image>();
        blockerBtn.transition    = Selectable.Transition.None;
        blockerBtn.onClick.AddListener(CloseDetail);

        // Left column: big sprite + stats beneath
        var frame = JournalCardFactory.SpriteComposite(content, "Sprite", 190f, sprite, overlay, tint);
        frame.GetComponent<RectTransform>().anchoredPosition = new Vector2(-270f, 120f);

        UIControlFactory.Label(content, "Stats", -270f, -115f, 280f, 250f,
            statsText, UIControlFactory.TextColor, 19, TextAnchor.UpperLeft);

        // Right column: full description
        UIControlFactory.Label(content, "DescHeader", 160f, 195f, 460f, 32f,
            "— DESCRIPTION —", UIControlFactory.TextDim, 18);
        var desc = UIControlFactory.Label(content, "Desc", 160f, -30f, 460f, 400f,
            string.IsNullOrEmpty(description) ? "No description yet." : description,
            UIControlFactory.TextColor, 21, TextAnchor.UpperLeft);
        desc.lineSpacing = 1.15f;
    }

    void CloseDetail()
    {
        if (_detailPopup != null) Destroy(_detailPopup);
        _detailPopup = null;
    }

    // ── Sprite resolution — shared with the wave preview ──────────────

    static Sprite TowerBaseSprite(TowerDefinition def)   => DefinitionIcons.TowerBase(def);
    static Sprite TowerTurretSprite(TowerDefinition def) => DefinitionIcons.TowerTurret(def);
    static Sprite UnitSprite(UnitDefinition def)         => DefinitionIcons.Unit(def);

    // ── Stat lines ────────────────────────────────────────────────────

    static IEnumerable<TowerDefinition> SortedByTier(IEnumerable<TowerDefinition> defs)
    {
        var list = new List<TowerDefinition>(defs);
        list.Sort((a, b) => a.towerTier != b.towerTier
            ? a.towerTier.CompareTo(b.towerTier)
            : string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase));
        return list;
    }

    static IEnumerable<string> TowerStats(TowerDefinition def)
    {
        yield return $"Tier      T{def.towerTier}  ·  {def.balanceType}";
        yield return $"Cost      {def.resourceCost}g";
        yield return $"Range     {def.range:0.#}";

        float dmg = TowerStatResolver.Damage(def);
        var   dt  = TowerStatResolver.DamageTypeFor(def);
        yield return dmg > 0f
            ? $"Damage    {dmg:0.#}" + (dt.HasValue ? $"  {DamageTypeColors.Tag(dt.Value)}" : "")
            : "Damage    —";

        float cd = TowerStatResolver.Cooldown(def);
        yield return cd > 0f ? $"Fire Rate {1f / cd:0.##}/s" : "Fire Rate —";

        float sb = TowerStatResolver.ShieldBonus(def);
        if (Mathf.Abs(sb) > 0.001f)
            yield return $"vs Shield {(sb > 0f ? "+" : "")}{sb:0.#}";
    }

    static IEnumerable<string> EnemyStats(UnitDefinition def)
    {
        yield return $"Life      {def.life:0}";
        if (def.shield > 0f) yield return $"Shield    {def.shield:0}";
        yield return $"Speed     {def.speed:0.#}";
        if (def.physicalDefense  != 0) yield return $"Armor     {def.physicalDefense}";
        if (def.elementalDefense != 0) yield return $"Resist    {def.elementalDefense}";
        if (def.arcanaDefense    != 0) yield return $"Fortitude {def.arcanaDefense}";
        yield return $"End dmg   {def.deathBlow}";
    }

    void WarnMissingLibrary(string libName)
    {
        Debug.LogWarning($"[JournalAlmanacScreen] {libName} missing from scene — regenerate the Main Menu scene.");
        UIControlFactory.Label(_gridContent, "Missing", 0f, 0f, 800f, 60f,
            "Definitions unavailable — regenerate the Main Menu scene.", UIControlFactory.TextDim, 22);
    }
}
