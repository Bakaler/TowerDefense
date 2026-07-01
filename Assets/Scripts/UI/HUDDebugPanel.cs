using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-left debug enemy spawn buttons. Unchanged from original layout.
/// </summary>
public class HUDDebugPanel : MonoBehaviour
{
    public void Build(GameObject canvasRoot)
    {
        const float BTN_W = 110f, BTN_H = 28f, PAD = 6f;

        var ids    = new[] { "basic_enemy", "fast_enemy", "armored_enemy", "boss_enemy", "resilient_enemy",
                             "splitter_enemy", "phantom_enemy", "charger_enemy", "priest_enemy", "shielder_enemy" };
        var labels = new[] { "Basic", "Fast", "Armored", "Boss", "Resilient",
                             "Splitter", "Phantom", "Charger", "Priest", "Shielder" };
        var colors = new Color[]
        {
            new Color(0.65f, 0.15f, 0.15f, 1f), new Color(0.65f, 0.60f, 0.10f, 1f),
            new Color(0.35f, 0.35f, 0.40f, 1f), new Color(0.55f, 0.10f, 0.55f, 1f),
            new Color(0.10f, 0.50f, 0.75f, 1f), new Color(0.70f, 0.28f, 0.05f, 1f),
            new Color(0.55f, 0.30f, 0.85f, 1f), new Color(0.80f, 0.25f, 0.05f, 1f),
            new Color(0.75f, 0.70f, 0.10f, 1f), new Color(0.15f, 0.45f, 0.80f, 1f),
        };

        var panel = new GameObject("DebugSpawnPanel");
        panel.transform.SetParent(canvasRoot.transform, false);
        var rt          = panel.AddComponent<RectTransform>();
        rt.anchorMin    = rt.anchorMax = Vector2.zero;
        rt.pivot        = Vector2.zero;
        rt.anchoredPosition = new Vector2(PAD, PAD);
        rt.sizeDelta    = new Vector2(BTN_W + PAD * 2f, ids.Length * (BTN_H + PAD) + PAD);
        panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        for (int i = 0; i < ids.Length; i++)
        {
            float y  = rt.sizeDelta.y - PAD - (i + 1) * (BTN_H + PAD) + PAD;
            var bGO  = HUDHelpers.MakeRect($"Debug_{ids[i]}", panel, PAD, y, BTN_W, BTN_H);
            var bImg = bGO.AddComponent<Image>(); bImg.color = colors[i];
            var btn  = bGO.AddComponent<Button>(); btn.targetGraphic = bImg;
            HUDHelpers.MakeText(HUDHelpers.MakeRect("L", bGO, 0f, 0f, BTN_W, BTN_H),
                labels[i], Color.white, 11, bold: true).alignment = TextAnchor.MiddleCenter;
            string unitId = ids[i];
            btn.onClick.AddListener(() => SpawnUnit(unitId));
        }
    }

    static void SpawnUnit(string unitId)
    {
        if (UnitFactory.Instance == null || PathGraph.Instance == null) return;
        UnitSpawner spawner = null;
        foreach (var s in FindObjectsByType<UnitSpawner>(FindObjectsSortMode.None))
            if (s.pathIndex == 0) { spawner = s; break; }
        if (spawner == null) return;

        var go = UnitFactory.Instance.Build(unitId, spawner.transform.position);
        if (go == null) return;
        var unit = go.GetComponent<UnitManager>();
        if (unit != null && spawner.headNode != null)
        {
            unit.AssignRoute(PathGraph.Instance.BuildRoute(spawner.headNode));
            WaveManager.Instance?.RegisterUnit(unit);
        }
    }
}
