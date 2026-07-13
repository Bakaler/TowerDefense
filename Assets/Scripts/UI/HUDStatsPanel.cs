using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Green top-left panel — gold, tech, research button, tower count, E/A/P balance rows.
/// Anchored top-left, ~half-screen height.
/// </summary>
public class HUDStatsPanel : MonoBehaviour
{
    const float W   = HUDHelpers.STATS_W;
    const float H   = HUDHelpers.STATS_H;
    const float PAD = 14f;

    Text _goldText;
    Text _techText;
    Text _towerCountText;
    Text _towerMilestoneText;
    Text _hdrElem, _hdrElemBonus;
    Text _hdrArc,  _hdrArcBonus;
    Text _hdrPhys, _hdrPhysDrop;

    // Objectives
    GameObject _objectivesPanel;
    GameObject _objectivesRowRoot;

    ResourceManagerScript _rm;

    public void Build(GameObject canvasRoot)
    {
        var panel = new GameObject("StatsPanel");
        panel.transform.SetParent(canvasRoot.transform, false);
        var rt       = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(W, H);
        panel.AddComponent<Image>().color = new Color(0.05f, 0.13f, 0.07f, 0.93f);

        const float IW = W - PAD * 2f;
        const float R1 = 22f;  // tall row
        const float R2 = 16f;  // small row
        const float G  = 3f;
        float y = H - PAD - R1;

        // Gold
        _goldText = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("Gold", panel, PAD, y, IW, R1),
            "⚡  0", new Color(1f, 0.85f, 0.3f), 15, bold: true);
        y -= R1 + G;

        // Tech
        _techText = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("Tech", panel, PAD, y, IW, R1),
            "🔬  0", new Color(0.35f, 1f, 0.55f), 13);
        y -= R1 + G;

        // RESEARCH button — full width
        var (rBtn, _) = HUDHelpers.MakeBtn(panel, "ResearchBtn", PAD, y, IW, 22f,
            new Color(0.12f, 0.30f, 0.16f, 1f), "RESEARCH", 10, bold: true);
        rBtn.onClick.AddListener(() => GameHUD.Instance?.OpenResearchPanel());
        y -= 26f;

        // Tower count
        _towerCountText = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("TowerCount", panel, PAD, y, IW, R2),
            "Twr  0/8", new Color(0.75f, 0.75f, 0.85f), 11);
        y -= R2 + G;
        _towerMilestoneText = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("Milestone", panel, PAD, y, IW, R2),
            "0 / 12", new Color(0.55f, 0.85f, 0.55f), 10);
        y -= R2 + 8f;

        // Divider
        HUDHelpers.MakeRect("Div", panel, PAD, y, IW, 1f)
            .AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        y -= 8f;

        // E / A / P — stacked: value line then bonus line
        Color cE = new Color(0.4f, 0.85f, 0.5f);
        Color cA = new Color(0.7f, 0.5f,  1.0f);
        Color cP = new Color(0.9f, 0.55f, 0.3f);
        Color cD = new Color(1.0f, 0.85f, 0.3f);

        _hdrElem      = HUDHelpers.MakeText(HUDHelpers.MakeRect("Elem",      panel, PAD, y, IW, R2), "E  0.00",  cE, 11); y -= R2;
        _hdrElemBonus = HUDHelpers.MakeText(HUDHelpers.MakeRect("ElemBonus", panel, PAD, y, IW, R2), "+0% orbs", cE, 10); y -= R2 + G;
        _hdrArc       = HUDHelpers.MakeText(HUDHelpers.MakeRect("Arc",       panel, PAD, y, IW, R2), "A  0.00",  cA, 11); y -= R2;
        _hdrArcBonus  = HUDHelpers.MakeText(HUDHelpers.MakeRect("ArcBonus",  panel, PAD, y, IW, R2), "+0% sci",  cA, 10); y -= R2 + G;
        _hdrPhys      = HUDHelpers.MakeText(HUDHelpers.MakeRect("Phys",      panel, PAD, y, IW, R2), "P  0.00",  cP, 11); y -= R2;
        _hdrPhysDrop  = HUDHelpers.MakeText(HUDHelpers.MakeRect("PhysDrop",  panel, PAD, y, IW, R2), "15%+0%",  cD, 10); y -= R2 + 10f;

        // Objectives below balance rows
        BuildObjectives(panel, y);
    }

    void BuildObjectives(GameObject parent, float startY)
    {
        const float W_OBJ = HUDHelpers.STATS_W - 28f;

        _objectivesPanel = new GameObject("Objectives");
        _objectivesPanel.transform.SetParent(parent.transform, false);
        var rt          = _objectivesPanel.AddComponent<RectTransform>();
        rt.anchorMin    = Vector2.zero;
        rt.anchorMax    = Vector2.zero;
        rt.pivot        = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(14f, startY);
        rt.sizeDelta    = new Vector2(W_OBJ, 300f);
        _objectivesPanel.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.10f, 0.75f);

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(_objectivesPanel.transform, false);
        var tRT = titleGO.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
        tRT.pivot = new Vector2(0.5f, 1f); tRT.anchoredPosition = Vector2.zero;
        tRT.sizeDelta = new Vector2(0f, 26f);
        var tTxt = titleGO.AddComponent<Text>();
        tTxt.text = "OBJECTIVES"; tTxt.color = new Color(0.75f, 0.9f, 1f);
        tTxt.font = HUDHelpers.GetFont(); tTxt.fontSize = 13; tTxt.fontStyle = FontStyle.Bold;
        tTxt.alignment = TextAnchor.MiddleCenter; tTxt.raycastTarget = false;

        _objectivesRowRoot = new GameObject("Rows");
        _objectivesRowRoot.transform.SetParent(_objectivesPanel.transform, false);
        var rRT = _objectivesRowRoot.AddComponent<RectTransform>();
        rRT.anchorMin = new Vector2(0f, 1f); rRT.anchorMax = new Vector2(1f, 1f);
        rRT.pivot = new Vector2(0f, 1f); rRT.anchoredPosition = new Vector2(0f, -26f);
        rRT.sizeDelta = Vector2.zero;

        _objectivesPanel.SetActive(false);
    }

    public void RefreshObjectives()
    {
        if (_objectivesPanel == null || _objectivesRowRoot == null) return;
        for (int i = _objectivesRowRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(_objectivesRowRoot.transform.GetChild(i).gameObject);

        var objs = ObjectiveTracker.Objectives;
        if (objs == null || objs.Count == 0) { _objectivesPanel.SetActive(false); return; }
        _objectivesPanel.SetActive(true);

        const float ROW_H = 22f;
        const float PAD   = 7f;
        float y = 0f;
        for (int i = 0; i < objs.Count; i++)
        {
            var  def      = objs[i];
            int  progress = ObjectiveTracker.GetProgress(i);
            bool done     = ObjectiveTracker.IsComplete(i);

            var rowGO = new GameObject($"Row_{i}");
            rowGO.transform.SetParent(_objectivesRowRoot.transform, false);
            var rRT = rowGO.AddComponent<RectTransform>();
            rRT.anchorMin = new Vector2(0f, 1f); rRT.anchorMax = new Vector2(1f, 1f);
            rRT.pivot = new Vector2(0f, 1f); rRT.anchoredPosition = new Vector2(0f, y);
            rRT.sizeDelta = new Vector2(0f, ROW_H);

            string prefix = done ? "✓ " : (def.required ? "• " : "◦ ");
            Color  col    = done ? new Color(0.3f, 0.95f, 0.45f)
                          : def.required ? Color.white : new Color(0.65f, 0.65f, 0.65f);
            string prog   = def.count > 1 ? $"  {progress}/{def.count}" : "";
            string label  = !string.IsNullOrEmpty(def.description)
                          ? def.description + prog
                          : $"{def.type} {def.targetId}{prog}";

            var txtGO = new GameObject("L");
            txtGO.transform.SetParent(rowGO.transform, false);
            var tRT = txtGO.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(PAD, 0f); tRT.offsetMax = new Vector2(-PAD, 0f);
            var txt = txtGO.AddComponent<Text>();
            txt.text = prefix + label; txt.color = col; txt.font = HUDHelpers.GetFont();
            txt.fontSize = 12; txt.alignment = TextAnchor.MiddleLeft; txt.raycastTarget = false;
            y -= ROW_H;
        }

        float totalH = 26f + (-y) + 6f;
        var panelRT   = _objectivesPanel.GetComponent<RectTransform>();
        panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, totalH);
        _objectivesRowRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, -y);
    }

    void Start() => _rm = ResourceManagerScript.Instance;

    void Update()
    {
        if (_goldText != null && _rm != null)
            _goldText.text = $"⚡  {_rm.resourceOne}";

        if (_techText != null)
            _techText.text = $"🔬  {TechManager.Instance?.Tech ?? 0}";

        if (_towerCountText != null && BalanceManager.Instance != null)
        {
            var bm = BalanceManager.Instance;
            _towerCountText.color = bm.TowerCount >= bm.MaxTowers
                ? new Color(1f, 0.35f, 0.35f) : new Color(0.75f, 0.75f, 0.85f);
            _towerCountText.text = $"Towers  {bm.TowerCount}/{bm.MaxTowers}";

            if (_towerMilestoneText != null)
            {
                float total = bm.Elemental + bm.Arcane + bm.Physical;
                int   iTotal = Mathf.FloorToInt(total);
                int   next = -1;
                foreach (int t in BalanceManager.Thresholds) if (iTotal < t) { next = t; break; }
                _towerMilestoneText.text = $"{iTotal} / {(next >= 0 ? next.ToString() : "max")}";
            }
        }

        RefreshBalance();
    }

    void RefreshBalance()
    {
        var towers = HUDHelpers.GetTowerData(out var idCounts);
        float eCum = 0f, aCum = 0f, pCum = 0f;
        foreach (var t in towers)
        {
            if (t.isGhost) continue;
            float c = HUDHelpers.BalanceRatio(idCounts[t.definitionId]) * t.balanceMultiplier;
            switch (t.balanceType)
            {
                case BalanceType.Elemental: eCum += c; break;
                case BalanceType.Arcane:    aCum += c; break;
                case BalanceType.Physical:  pCum += c; break;
                case BalanceType.All:
                    eCum += c / 3f; aCum += c / 3f; pCum += c / 3f;
                    break;
            }
        }
        if (_hdrElem != null) _hdrElem.text = $"E  {eCum:0.00}";
        if (_hdrArc  != null) _hdrArc.text  = $"A  {aCum:0.00}";
        if (_hdrPhys != null) _hdrPhys.text = $"P  {pCum:0.00}";
        if (_hdrPhysDrop != null)
            _hdrPhysDrop.text = $"{15}% + {Mathf.FloorToInt(pCum * 0.25f)}%";
        if (_hdrElemBonus != null)
            _hdrElemBonus.text = $"+{Mathf.FloorToInt((1f - Mathf.Pow(0.99f, eCum)) * 100f)}% orbs";
        if (_hdrArcBonus != null)
            _hdrArcBonus.text = $"+{Mathf.FloorToInt((1f - Mathf.Pow(0.99f, aCum)) * 100f)}% sci";
    }
}
