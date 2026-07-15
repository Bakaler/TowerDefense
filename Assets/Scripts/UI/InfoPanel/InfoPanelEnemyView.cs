using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enemy inspector body of the info panel: live stats on the right, a 3×4
/// behavior status grid (tinted circles + stack counts + hover tooltip) on the
/// left. Built and owned by HUDInfoPanel; refreshed every frame while visible.
/// </summary>
internal sealed class InfoPanelEnemyView
{
    const float W      = InfoPanelLayout.W;
    const float H      = InfoPanelLayout.H;
    const float HDR_H  = InfoPanelLayout.HDR_H;
    const float PAD    = InfoPanelLayout.PAD;
    const float BODY_H = InfoPanelLayout.BODY_H;

    readonly HUDInfoPanel _owner;
    GameObject _enemyBody;

    Text _epHp, _epShield, _epSpeed, _epDeathBlow;
    Text _epArmor, _epResistance, _epFortitude, _epDescription;

    // ── Behavior grid ──────────────────────────────────────────────────
    const int   BGRID_COLS = 3, BGRID_ROWS = 4;
    const float BGRID_CELL = 50f, BGRID_GAP = 6f;
    const float BGRID_W    = BGRID_COLS * BGRID_CELL + (BGRID_COLS - 1) * BGRID_GAP;
    GameObject[] _ebSlots;
    Image[]      _ebCircles;
    Image[]      _ebIcons;
    Text[]       _ebLetters;
    Text[]       _ebCounts;
    GameObject   _ebTooltip;
    Text         _ebTooltipText;
    int          _ebHover = -1;
    readonly List<BehaviorHandler.ActiveStack> _ebStacks = new();
    readonly Dictionary<string, Sprite> _behaviorIconCache = new();

    public InfoPanelEnemyView(HUDInfoPanel owner, GameObject panelRoot)
    {
        _owner = owner;
        Build(panelRoot);
    }

    public void SetActive(bool active) => _enemyBody?.SetActive(active);

    /// <summary>Closes the hover tooltip (called when the panel switches mode/hides).</summary>
    public void HideTooltip()
    {
        _ebHover = -1;
        _ebTooltip?.SetActive(false);
    }

    // ── Build ──────────────────────────────────────────────────────────

    void Build(GameObject panelRoot)
    {
        _enemyBody = new GameObject("EnemyBody");
        _enemyBody.transform.SetParent(panelRoot.transform, false);
        var rt = _enemyBody.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(W, H);

        // Left: 3×4 behavior status grid; stats fill two columns to its right
        float bx   = PAD + BGRID_W + PAD;
        float colW = (W - bx - PAD * 2f) * 0.5f;
        const float ROW = 26f;
        float y = H - HDR_H - 8f - ROW;

        _epHp         = HUDHelpers.MakeText(HUDHelpers.MakeRect("HP",     _enemyBody, bx,             y, colW, ROW), "", Color.white, 14);
        _epArmor      = HUDHelpers.MakeText(HUDHelpers.MakeRect("Armor",  _enemyBody, bx + colW + PAD, y, colW, ROW), "", new Color(0.8f,0.8f,0.8f), 14); y -= ROW + 3f;
        _epShield     = HUDHelpers.MakeText(HUDHelpers.MakeRect("Shield", _enemyBody, bx,             y, colW, ROW), "", new Color(0.35f,0.75f,1f), 14);
        _epResistance = HUDHelpers.MakeText(HUDHelpers.MakeRect("Res",    _enemyBody, bx + colW + PAD, y, colW, ROW), "", new Color(0.4f,0.85f,1f), 14); y -= ROW + 3f;
        _epSpeed      = HUDHelpers.MakeText(HUDHelpers.MakeRect("Spd",    _enemyBody, bx,             y, colW, ROW), "", Color.white, 14);
        _epFortitude  = HUDHelpers.MakeText(HUDHelpers.MakeRect("Fort",   _enemyBody, bx + colW + PAD, y, colW, ROW), "", new Color(0.85f,0.6f,1f), 14); y -= ROW + 3f;
        _epDeathBlow  = HUDHelpers.MakeText(HUDHelpers.MakeRect("DB",     _enemyBody, bx,             y, colW, ROW), "", new Color(0.95f,0.4f,0.4f), 14); y -= ROW + 8f;

        HUDHelpers.MakeRect("Div", _enemyBody, bx, y + 4f, W - bx - PAD, 1f)
            .AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        _epDescription = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("Desc", _enemyBody, bx, 6f, W - bx - PAD, y - 4f),
            "", new Color(0.75f, 0.75f, 0.75f), 12);
        _epDescription.alignment         = TextAnchor.UpperLeft;
        _epDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
        _epDescription.verticalOverflow   = VerticalWrapMode.Overflow;

        BuildBehaviorGrid();
    }

    /// <summary>3×4 grid of status circles on the left of the enemy body. Each active
    /// behavior gets a tinted circle (icon or first letter), a stack count, and a
    /// hover tooltip with the behavior's description.</summary>
    void BuildBehaviorGrid()
    {
        int n = BGRID_COLS * BGRID_ROWS;
        _ebSlots   = new GameObject[n];
        _ebCircles = new Image[n];
        _ebIcons   = new Image[n];
        _ebLetters = new Text[n];
        _ebCounts  = new Text[n];

        float topY  = H - HDR_H - 8f - BGRID_CELL;
        var circle  = RuntimeSprites.Circle(32);

        for (int i = 0; i < n; i++)
        {
            int   col = i % BGRID_COLS, row = i / BGRID_COLS;
            float x   = PAD + col * (BGRID_CELL + BGRID_GAP);
            float y   = topY - row * (BGRID_CELL + BGRID_GAP);

            var slot = HUDHelpers.MakeRect($"Behavior_{i}", _enemyBody, x, y, BGRID_CELL, BGRID_CELL);
            var circ = slot.AddComponent<Image>();
            circ.sprite        = circle;
            circ.raycastTarget = true;

            var iconGO = HUDHelpers.MakeRect("Icon", slot, 5f, 5f, BGRID_CELL - 10f, BGRID_CELL - 10f);
            var icon   = iconGO.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.enabled       = false;

            var letter = HUDHelpers.MakeText(
                HUDHelpers.MakeRect("L", slot, 0f, 0f, BGRID_CELL, BGRID_CELL), "", new Color(0.05f, 0.05f, 0.10f), 20, bold: true);
            letter.alignment     = TextAnchor.MiddleCenter;
            letter.raycastTarget = false;

            var count = HUDHelpers.MakeText(
                HUDHelpers.MakeRect("N", slot, BGRID_CELL * 0.3f, -3f, BGRID_CELL * 0.7f, BGRID_CELL * 0.45f), "", new Color(1f, 0.95f, 0.55f), 14, bold: true);
            count.alignment     = TextAnchor.LowerRight;
            count.raycastTarget = false;

            int slotIdx  = i;
            var hover    = slot.AddComponent<HoverCallbacks>();
            hover.onEnter = () => { _ebHover = slotIdx; RefreshBehaviorTooltip(); };
            hover.onExit  = () => { if (_ebHover == slotIdx) { _ebHover = -1; _ebTooltip?.SetActive(false); } };

            slot.SetActive(false);
            _ebSlots[i] = slot; _ebCircles[i] = circ; _ebIcons[i] = icon;
            _ebLetters[i] = letter; _ebCounts[i] = count;
        }

        // Tooltip — built last so it draws above everything else in the body
        _ebTooltip = HUDHelpers.MakeRect("BehaviorTooltip", _enemyBody, PAD + BGRID_W + PAD, 6f, 420f, 84f);
        _ebTooltip.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.09f, 0.97f);
        _ebTooltipText = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("T", _ebTooltip, 10f, 5f, 400f, 74f), "", Color.white, 12);
        _ebTooltipText.alignment          = TextAnchor.UpperLeft;
        _ebTooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _ebTooltipText.verticalOverflow   = VerticalWrapMode.Truncate;
        _ebTooltip.SetActive(false);
    }

    // ── Refresh ────────────────────────────────────────────────────────

    public void Refresh(UnitManager u)
    {
        if (u == null) return;

        string name = u.definitionId;
        string desc = "";
        if (!string.IsNullOrEmpty(u.definitionId) && UnitDefinitionLibrary.Instance != null &&
            UnitDefinitionLibrary.Instance.TryGet(u.definitionId, out var def))
        {
            if (!string.IsNullOrEmpty(def.displayName)) name = def.displayName;
            desc = DescriptionTags.Resolve(def.description ?? "");
        }

        _owner.SetHeaderTitle(name.ToUpper());
        if (_epHp         != null) _epHp.text         = $"HP        {u.lifeCurrent:0}/{u.lifeMax:0}";
        if (_epShield     != null) _epShield.text     = u.hasShields ? $"Shield    {u.shieldCurrent:0}/{u.shieldMax:0}" : "";
        if (_epSpeed      != null) _epSpeed.text      = $"Speed     {u.speedMax:0.##}";
        if (_epDeathBlow  != null) _epDeathBlow.text  = $"End dmg   {u.deathBlow}";
        if (_epArmor      != null) _epArmor.text      = $"Armor     {u.physicalDefense}{ReductionPct(u, u.physicalDefense)}";
        if (_epResistance != null) _epResistance.text = $"Resist    {u.elementalDefense}{ReductionPct(u, u.elementalDefense)}";
        if (_epFortitude  != null) _epFortitude.text  = $"Fortitude {u.arcanaDefense}{ReductionPct(u, u.arcanaDefense)}";
        if (_epDescription!= null) _epDescription.text = desc;

        RefreshBehaviorGrid(u);
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

    void RefreshBehaviorGrid(UnitManager u)
    {
        if (_ebSlots == null) return;

        var handler = u.Behaviors;
        if (handler != null) handler.GetActiveStacks(_ebStacks);
        else                 _ebStacks.Clear();

        for (int i = 0; i < _ebSlots.Length; i++)
        {
            bool used = i < _ebStacks.Count;
            _ebSlots[i].SetActive(used);
            if (!used) continue;

            var def = _ebStacks[i].Def;

            Color c = def.tintColor == Color.white ? new Color(0.52f, 0.58f, 0.70f) : def.tintColor;
            c.a = 1f;
            _ebCircles[i].color = c;

            var icon = LoadBehaviorIcon(def.iconPath);
            _ebIcons[i].enabled = icon != null;
            if (icon != null) _ebIcons[i].sprite = icon;

            string name = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
            _ebLetters[i].text = icon == null && name.Length > 0 ? name.Substring(0, 1).ToUpper() : "";

            _ebCounts[i].text = _ebStacks[i].Count > 1 ? _ebStacks[i].Count.ToString() : "";
        }

        // Live-update (or hide) the tooltip while hovering as stacks change
        if (_ebHover >= 0) RefreshBehaviorTooltip();
    }

    void RefreshBehaviorTooltip()
    {
        if (_ebTooltip == null) return;
        if (_ebHover < 0 || _ebHover >= _ebStacks.Count) { _ebTooltip.SetActive(false); return; }

        var s = _ebStacks[_ebHover];
        string title  = string.IsNullOrEmpty(s.Def.displayName) ? s.Def.id : s.Def.displayName;
        string stacks = s.Count > 1 ? $"  ×{s.Count}" : "";
        string timing = s.Remaining < 99999f ? $"   <color=#8a8a99>{s.Remaining:0.0}s</color>" : "";
        string desc   = string.IsNullOrEmpty(s.Def.description) ? "" : "\n" + DescriptionTags.Resolve(s.Def.description);
        _ebTooltipText.text = $"<b>{title}{stacks}</b>{timing}{desc}";

        // Sit just right of the hovered slot, clamped inside the body
        var slotRT = _ebSlots[_ebHover].GetComponent<RectTransform>();
        var ttRT   = _ebTooltip.GetComponent<RectTransform>();
        Vector2 pos = slotRT.anchoredPosition + new Vector2(BGRID_CELL + 8f, BGRID_CELL - 84f);
        pos.y = Mathf.Clamp(pos.y, 6f, BODY_H - 84f - 6f);
        ttRT.anchoredPosition = pos;

        _ebTooltip.SetActive(true);
    }

    Sprite LoadBehaviorIcon(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (_behaviorIconCache.TryGetValue(path, out var sp)) return sp;
        sp = Resources.Load<Sprite>(path);
        _behaviorIconCache[path] = sp;   // cache misses too so we don't re-hit Resources
        return sp;
    }
}
