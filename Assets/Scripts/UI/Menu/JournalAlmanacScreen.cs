using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The almanac itself: a card grid over every tower or unit definition.
/// One screen instance serves both modes — SetMode before pushing; the grid
/// rebuilds when the mode changed. All entries render as discovered for now
/// (JournalCardFactory.SetDiscovered carries the future gating).
/// </summary>
public class JournalAlmanacScreen : MenuScreen
{
    public enum Mode { Towers, Enemies }

    Mode _mode = Mode.Towers;
    Mode? _builtMode;
    UnityEngine.UI.Text _title;
    GameObject _gridContent;

    public void SetMode(Mode mode) => _mode = mode;

    protected override GameObject Build(GameObject canvasRoot)
    {
        var panel = UIControlFactory.Rect("JournalAlmanacScreen", canvasRoot, 0f, 0f, 1920f, 1080f);

        _title = UIControlFactory.Label(panel, "Title", 0f, 420f, 800f, 80f,
            "", UIControlFactory.TitleColor, 52, TextAnchor.MiddleCenter, bold: true);

        _gridContent = UIScrollListFactory.Grid(panel, "Grid", 0f, -30f, 1360f, 720f,
            JournalCardFactory.CardSize, spacing: 18f);

        var (backBtn, _) = UIControlFactory.Button(panel, "BackBtn", 0f, -470f, 220f, 52f,
            new Color(0.28f, 0.12f, 0.12f, 1f), "BACK", 22);
        backBtn.onClick.AddListener(() => Controller.Back());

        return panel;
    }

    protected override void Refresh()
    {
        _title.text = _mode == Mode.Towers ? "TOWER ALMANAC" : "ENEMY ALMANAC";
        if (_builtMode == _mode) return;
        _builtMode = _mode;

        UIScrollListFactory.Clear(_gridContent);
        if (_mode == Mode.Towers) BuildTowerCards();
        else                      BuildEnemyCards();
    }

    // ── Towers ────────────────────────────────────────────────────────

    void BuildTowerCards()
    {
        var lib = TowerDefinitionLibrary.Instance;
        if (lib == null) { WarnMissingLibrary("TowerDefinitionLibrary"); return; }

        foreach (var def in SortedByTier(lib.All.Values))
        {
            JournalCardFactory.Create(_gridContent, def.displayName ?? def.id,
                TowerBaseSprite(def), TowerStats(def),
                overlaySprite: string.IsNullOrEmpty(def.turretSpritePath) ? null : RuntimeSprites.Load(def.turretSpritePath),
                tint: def.tintColor);
        }
    }

    /// <summary>Same resolution order as the tower editor's tooltip preview:
    /// spritePath first (frame 0 of animated sheets via Load's fallback), then spriteSheet cell.</summary>
    static Sprite TowerBaseSprite(TowerDefinition def)
    {
        if (!string.IsNullOrEmpty(def.spritePath))
        {
            var s = RuntimeSprites.Load(def.spritePath);
            if (s != null) return s;
        }
        if (!string.IsNullOrEmpty(def.spriteSheet))
            return RuntimeSprites.FromSheet(def.spriteSheet, Mathf.Max(def.spriteIndex, 0));
        return null;
    }

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
        yield return dmg > 0f ? $"Damage    {dmg:0.#}" : "Damage    —";

        float cd = TowerStatResolver.Cooldown(def);
        yield return cd > 0f ? $"Fire Rate {1f / cd:0.##}/s" : "Fire Rate —";

        float sb = TowerStatResolver.ShieldBonus(def);
        if (Mathf.Abs(sb) > 0.001f)
            yield return $"vs Shield {(sb > 0f ? "+" : "")}{sb:0.#}";
    }

    // ── Enemies ───────────────────────────────────────────────────────

    void BuildEnemyCards()
    {
        var lib = UnitDefinitionLibrary.Instance;
        if (lib == null) { WarnMissingLibrary("UnitDefinitionLibrary"); return; }

        foreach (var def in lib.All.Values)
        {
            JournalCardFactory.Create(_gridContent, def.displayName ?? def.id,
                UnitSprite(def), EnemyStats(def), tint: def.tintColor);
        }
    }

    /// <summary>Same resolution order as the unit editor's tooltip preview:
    /// walk-animation frame 0, then spriteSheet cell, then single sprite.</summary>
    static Sprite UnitSprite(UnitDefinition def)
    {
        if (!string.IsNullOrEmpty(def.animSheet))
        {
            var s = RuntimeSprites.FromSheet(def.animSheet, 0);
            if (s != null) return s;
        }
        if (!string.IsNullOrEmpty(def.spriteSheet) && def.spriteIndex >= 0)
        {
            var s = RuntimeSprites.FromSheet(def.spriteSheet, def.spriteIndex);
            if (s != null) return s;
        }
        return RuntimeSprites.Load(def.spritePath);
    }

    static IEnumerable<string> EnemyStats(UnitDefinition def)
    {
        yield return $"Life      {def.life:0}";
        if (def.shield > 0f) yield return $"Shield    {def.shield:0}";
        yield return $"Speed     {def.speed:0.#}";
        if (def.physicalDefense  != 0) yield return $"Armor     {def.physicalDefense}";
        if (def.elementalDefense != 0) yield return $"Resist    {def.elementalDefense}";
        if (def.arcanaDefense    != 0) yield return $"Fortitude {def.arcanaDefense}";
        yield return $"Bounty    {def.bounty}g";
        yield return $"End dmg   {def.deathBlow}";
    }

    void WarnMissingLibrary(string libName)
    {
        Debug.LogWarning($"[JournalAlmanacScreen] {libName} missing from scene — regenerate the Main Menu scene.");
        UIControlFactory.Label(_gridContent, "Missing", 0f, 0f, 800f, 60f,
            "Definitions unavailable — regenerate the Main Menu scene.", UIControlFactory.TextDim, 22);
    }
}
