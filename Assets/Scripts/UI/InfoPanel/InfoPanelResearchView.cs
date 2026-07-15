using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tier research body of the info panel: four side-by-side unlock buttons for
/// T2–T5 tech, wired to TechManager. Built and owned by HUDInfoPanel; refreshed
/// every frame while visible so costs/affordability stay live.
/// </summary>
internal sealed class InfoPanelResearchView
{
    const float W      = InfoPanelLayout.W;
    const float H      = InfoPanelLayout.H;
    const float PAD    = InfoPanelLayout.PAD;
    const float BODY_H = InfoPanelLayout.BODY_H;

    GameObject _tierResBody;
    Button _resT2Btn, _resT3Btn, _resT4Btn, _resT5Btn;
    Text   _resT2Lbl, _resT3Lbl, _resT4Lbl, _resT5Lbl;

    public InfoPanelResearchView(GameObject panelRoot)
    {
        Build(panelRoot);
    }

    public void SetActive(bool active) => _tierResBody?.SetActive(active);

    void Build(GameObject panelRoot)
    {
        _tierResBody = new GameObject("TierResBody");
        _tierResBody.transform.SetParent(panelRoot.transform, false);
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

        _resT2Btn.onClick.AddListener(() => { if (TechManager.Instance?.TryUnlockT2() == true) HUDTierSelector.Instance?.SyncWithShop(jumpToTier: 2); Refresh(); });
        _resT3Btn.onClick.AddListener(() => { if (TechManager.Instance?.TryUnlockT3() == true) HUDTierSelector.Instance?.SyncWithShop(jumpToTier: 3); Refresh(); });
        _resT4Btn.onClick.AddListener(() => { if (TechManager.Instance?.TryUnlockT4() == true) HUDTierSelector.Instance?.SyncWithShop(jumpToTier: 4); Refresh(); });
        _resT5Btn.onClick.AddListener(() => { if (TechManager.Instance?.TryUnlockT5() == true) HUDTierSelector.Instance?.SyncWithShop(jumpToTier: 5); Refresh(); });
    }

    public void Refresh()
    {
        var tm = TechManager.Instance;
        if (tm == null) return;

        void Setup(Button btn, Text lbl, bool done, bool prereq, bool canAfford, int cost, int tier)
        {
            if (btn == null) return;
            btn.interactable = !done && prereq && canAfford;
            if (lbl == null) return;
            if (done)        lbl.text = $"Tier {tier}  ✓  Unlocked";
            else if (!prereq)lbl.text = $"Unlock Tier {tier}  —  {cost} tech  (needs T{tier-1})";
            else             lbl.text = $"Unlock Tier {tier}  —  {cost} tech";
        }

        Setup(_resT2Btn, _resT2Lbl, tm.T2Unlocked, true,          tm.Tech >= TechManager.T2Cost, TechManager.T2Cost, 2);
        Setup(_resT3Btn, _resT3Lbl, tm.T3Unlocked, tm.T2Unlocked, tm.Tech >= TechManager.T3Cost, TechManager.T3Cost, 3);
        Setup(_resT4Btn, _resT4Lbl, tm.T4Unlocked, tm.T3Unlocked, tm.Tech >= TechManager.T4Cost, TechManager.T4Cost, 4);
        Setup(_resT5Btn, _resT5Lbl, tm.T5Unlocked, tm.T4Unlocked, tm.Tech >= TechManager.T5Cost, TechManager.T5Cost, 5);
    }
}
