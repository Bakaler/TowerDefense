using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-left debug enemy spawn buttons, built live from units.json.
/// Reloads the definition library and rebuilds itself at the start of every round,
/// so units added or edited in the Tower &amp; Ability Editor show up without leaving
/// play mode. Button colors come from each definition's debugColor.
/// </summary>
public class HUDDebugPanel : MonoBehaviour
{
    const float BTN_W = 110f, BTN_H = 28f, PAD = 6f;
    const int   MAX_ROWS = 15;   // wrap into a second column past this

    GameObject _canvasRoot;
    GameObject _panel;
    int        _lastWave = int.MinValue;

    public void Build(GameObject canvasRoot)
    {
        _canvasRoot = canvasRoot;
        Rebuild();
    }

    void Update()
    {
        // New round (or first frame) — pick up any units.json changes and rebuild
        int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWave : -1;
        if (wave == _lastWave) return;
        _lastWave = wave;

        UnitDefinitionLibrary.Instance?.Reload();
        SoundLibrary.Reload();
        AudioManager.Instance?.OnDefinitionsReloaded();
        Rebuild();
    }

    void Rebuild()
    {
        if (_canvasRoot == null) return;
        if (_panel != null) Destroy(_panel);

        IReadOnlyList<UnitDefinition> defs = UnitDefinitionLibrary.Instance != null
            ? UnitDefinitionLibrary.Instance.AllOrdered
            : System.Array.Empty<UnitDefinition>();

        int count = defs.Count;
        int cols  = Mathf.Max(1, Mathf.CeilToInt(count / (float)MAX_ROWS));
        int rows  = cols > 1 ? Mathf.CeilToInt(count / (float)cols) : count;

        _panel = new GameObject("DebugSpawnPanel");
        _panel.transform.SetParent(_canvasRoot.transform, false);
        var rt              = _panel.AddComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = Vector2.zero;
        rt.pivot            = Vector2.zero;
        rt.anchoredPosition = new Vector2(PAD, PAD);
        rt.sizeDelta        = new Vector2(cols * (BTN_W + PAD) + PAD, rows * (BTN_H + PAD) + PAD);
        _panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        for (int i = 0; i < count; i++)
        {
            var def = defs[i];
            int col = i / rows;
            int row = i % rows;

            float x = PAD + col * (BTN_W + PAD);
            float y = rt.sizeDelta.y - PAD - (row + 1) * (BTN_H + PAD) + PAD;

            var bGO  = HUDHelpers.MakeRect($"Debug_{def.id}", _panel, x, y, BTN_W, BTN_H);
            var bImg = bGO.AddComponent<Image>();
            bImg.color = ButtonColor(def);
            var btn  = bGO.AddComponent<Button>(); btn.targetGraphic = bImg;

            string label = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
            HUDHelpers.MakeText(HUDHelpers.MakeRect("L", bGO, 0f, 0f, BTN_W, BTN_H),
                label, Color.white, 11, bold: true).alignment = TextAnchor.MiddleCenter;

            string unitId = def.id;
            btn.onClick.AddListener(() => SpawnUnit(unitId));
        }
    }

    // Darkened debugColor so white button text stays readable
    static Color ButtonColor(UnitDefinition def)
    {
        var c = Color.Lerp(def.debugColor, Color.black, 0.45f);
        c.a = 1f;
        return c;
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
