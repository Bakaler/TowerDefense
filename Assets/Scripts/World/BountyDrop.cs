using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A clickable world pickup spawned on enemy kill.
/// Uses the same orb art as IncomeTower. Player clicks to collect;
/// CollectorTower can call Collect() directly.
/// </summary>
public class BountyDrop : MonoBehaviour
{
    public int   goldValue   = 1;
    public float clickRadius = 0.4f;   // world-space click radius

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (TowerPlacer.Instance != null && TowerPlacer.Instance.IsPlacing) return;

        Vector2 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (Vector2.Distance(mouse, transform.position) <= clickRadius)
            Collect();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, clickRadius);
    }

    public void Collect()
    {
        var orb = GetComponent<IncomeOrb>();
        if (orb != null) orb.Pop();

        var rm = FindFirstObjectByType<ResourceManagerScript>();
        if (rm != null) rm.ChangeResourceOne(goldValue);

        // IncomeOrb.Pop() destroys itself; destroy GO if no orb component
        if (orb == null) Destroy(gameObject);
    }

    // ── Factory ───────────────────────────────────────────────────────

    public static BountyDrop Spawn(Vector3 worldPos, int value)
    {
        var go = new GameObject("BountyDrop");
        go.transform.position   = worldPos;
        go.transform.localScale = Vector3.one * 1.1f;

        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Units";
        sr.sortingOrder     = 5;
        sr.color            = Color.white;

        var sheet = Resources.LoadAll<Sprite>("Art/Towers/TowerSet1");
        if (sheet != null && sheet.Length > 1)
            sr.sprite = sheet[1];

        go.AddComponent<IncomeOrb>();   // handles bob + pop animation

        var drop       = go.AddComponent<BountyDrop>();
        drop.goldValue = value;
        return drop;
    }
}
