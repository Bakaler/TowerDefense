using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dark-purple top-right corner panel.
/// Shows which tower tier column is currently displayed in the shop,
/// with ◀ ▶ buttons to shift between tiers.
/// </summary>
public class HUDTierSelector : MonoBehaviour
{
    public static HUDTierSelector Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }
    const float W = HUDHelpers.RIGHT_W;
    const float H = HUDHelpers.TIER_H;

    Text   _tierLabel;
    Button _prevBtn;
    Button _nextBtn;
    Text   _prevLabel;
    Text   _nextLabel;

    // Cached available tiers from TowerShop (populated after Rebuild)
    int[] _tiers    = System.Array.Empty<int>();
    int   _tierIdx  = 0;

    public void Build(GameObject canvasRoot)
    {
        var panel = new GameObject("TierSelector");
        panel.transform.SetParent(canvasRoot.transform, false);
        var rt       = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(W, H);
        panel.AddComponent<Image>().color = new Color(0.10f, 0.06f, 0.18f, 0.95f);

        const float PAD  = 6f;
        const float BTNH = 28f;
        const float BTNW = (W - PAD * 2f - 4f) * 0.5f;

        // Header
        HUDHelpers.MakeText(
            HUDHelpers.MakeRect("Header", panel, PAD, H - PAD - 18f, W - PAD * 2f, 18f),
            "TIERS", new Color(0.75f, 0.55f, 1f), 10, bold: true)
            .alignment = TextAnchor.MiddleCenter;

        // Tier name label
        _tierLabel = HUDHelpers.MakeText(
            HUDHelpers.MakeRect("TierLabel", panel, PAD, H - PAD - 18f - 32f, W - PAD * 2f, 30f),
            "T1", new Color(0.90f, 0.80f, 1f), 18, bold: true);
        _tierLabel.alignment = TextAnchor.MiddleCenter;

        // ◀ / ▶ buttons
        float btnY = PAD;
        (_prevBtn, _prevLabel) = HUDHelpers.MakeBtn(panel, "PrevTier",
            PAD, btnY, BTNW, BTNH,
            new Color(0.22f, 0.14f, 0.36f, 1f), "◀", 13, bold: true);
        (_nextBtn, _nextLabel) = HUDHelpers.MakeBtn(panel, "NextTier",
            PAD + BTNW + 4f, btnY, BTNW, BTNH,
            new Color(0.22f, 0.14f, 0.36f, 1f), "▶", 13, bold: true);

        _prevBtn.onClick.AddListener(PrevTier);
        _nextBtn.onClick.AddListener(NextTier);
    }

    void PrevTier()
    {
        if (_tiers.Length == 0) return;
        _tierIdx = (_tierIdx - 1 + _tiers.Length) % _tiers.Length;
        Apply();
    }

    void NextTier()
    {
        if (_tiers.Length == 0) return;
        _tierIdx = (_tierIdx + 1) % _tiers.Length;
        Apply();
    }

    void Apply()
    {
        if (TowerShop.Instance == null || _tiers.Length == 0) return;
        int tier = _tiers[_tierIdx];

        TowerShop.Instance.SetVisibleTier(tier);

        if (_tierLabel != null)
        {
            _tierLabel.text  = $"T{tier}";
            _tierLabel.color = new Color(0.90f, 0.80f, 1f);
        }

        if (_prevBtn != null) _prevBtn.interactable = _tiers.Length > 1;
        if (_nextBtn != null) _nextBtn.interactable = _tiers.Length > 1;
    }

    // Call after TowerShop.Rebuild() so we pick up the new tier list.
    // Pass jumpToTier > 0 to snap to that tier after syncing (e.g. after unlocking a new tier).
    public void SyncWithShop(int jumpToTier = 0)
    {
        if (TowerShop.Instance == null) return;
        _tiers   = TowerShop.Instance.AvailableTiers;
        _tierIdx = 0;
        if (jumpToTier > 0)
        {
            for (int i = 0; i < _tiers.Length; i++)
                if (_tiers[i] == jumpToTier) { _tierIdx = i; break; }
        }
        Apply();
    }

    void Update()
    {
        // Re-sync when tier list changes (e.g. after level load)
        if (TowerShop.Instance != null && _tiers != TowerShop.Instance.AvailableTiers)
            SyncWithShop();
    }
}
