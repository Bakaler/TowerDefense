using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

/// <summary>
/// TowerDefense → Setup Basic Wave Scene
///
/// Creates a blank-canvas scene with all manager objects.
/// Paths, spawners, and placement zones are built at runtime by LevelManager
/// from Resources/Definitions/Levels/level_1.json (and level_2.json, level_3.json).
///
/// Press Play → Level 1 loads automatically.
/// Press 1 / 2 / 3 in Play mode to switch levels.
/// </summary>
public static class SceneBootstrap
{
    [MenuItem("TowerDefense/Scene Generators (Wave)", false, 1)]
    static void OpenWindow() => SceneGeneratorsWindow.Open();

    public static void SetupScene()
    {
        if (!EditorUtility.DisplayDialog(
            "Setup Basic Wave Scene",
            "This will reset all scene objects.\n\n" +
            "Paths and spawners are built at runtime from JSON — no pre-placed nodes needed.\n\n" +
            "Press Play after setup, then 1/2/3 to switch levels.\n\nProceed?",
            "Yes", "Cancel"))
            return;

        Undo.SetCurrentGroupName("Setup Basic Wave Scene");
        int undoGroup = Undo.GetCurrentGroup();

        // ── Destroy everything the old bootstrap may have created ─────
        foreach (string n in new[]
        {
            "--- MANAGERS ---", "--- GLOBAL LIGHT ---", "--- SHOP UI ---",
            "--- PATH ---", "--- PATH 2 ---", "--- PATH 3 ---",
            "--- SPAWNER ---", "--- SPAWNER 2 ---", "--- SPAWNER 3 ---",
            "[LevelManager]", "[Background]",
        })
            DestroyIfExists(n);

        // ── 0. Global URP 2D light ────────────────────────────────────
        var lightRoot   = new GameObject("--- GLOBAL LIGHT ---");
        Undo.RegisterCreatedObjectUndo(lightRoot, "Create Global Light");
        var globalLight = lightRoot.AddComponent<Light2D>();
        globalLight.lightType = Light2D.LightType.Global;
        globalLight.intensity = 1f;
        globalLight.color     = Color.white;

        // ── 1. Managers ───────────────────────────────────────────────
        var managersRoot = new GameObject("--- MANAGERS ---");
        Undo.RegisterCreatedObjectUndo(managersRoot, "Create Managers");

        // Resource + Logic (each needs a tag for legacy Find calls)
        var resourceGO = CreateChild("ResourceManager", managersRoot);
        resourceGO.tag = EnsureTag("ResourceManager");
        resourceGO.AddComponent<ResourceManagerScript>();

        var logicGO = CreateChild("LogicManager", managersRoot);
        logicGO.tag = EnsureTag("Logic");
        logicGO.AddComponent<LogicManager>();

        // Core systems — all on one GO for simplicity
        var coreGO = CreateChild("Core", managersRoot);
        coreGO.AddComponent<WaveManager>();
        coreGO.AddComponent<GameHUD>();
        coreGO.AddComponent<BalanceManager>();
        coreGO.AddComponent<TechManager>();
        coreGO.AddComponent<CheatManager>();
        coreGO.AddComponent<ResearchManager>();
        coreGO.AddComponent<LevelManager>();    // ← drives everything from JSON

        // Libraries + factories
        var libGO = CreateChild("Libraries", managersRoot);
        libGO.AddComponent<UnitDefinitionLibrary>();
        libGO.AddComponent<UnitFactory>();
        libGO.AddComponent<TowerDefinitionLibrary>();
        libGO.AddComponent<TowerFactory>();
        libGO.AddComponent<EffectLibrary>();
        libGO.AddComponent<AbilityLibrary>();
        libGO.AddComponent<BehaviorLibrary>();
        libGO.AddComponent<PathGraph>();        // ScanScene() finds nodes at runtime
        libGO.AddComponent<TowerPlacer>();      // placementZones set by LevelManager

        // ── 2. Shop UI ────────────────────────────────────────────────
        BuildShopUI();

        // ── 3. Camera ─────────────────────────────────────────────────
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.orthographic       = true;
            cam.orthographicSize   = 7f;
            cam.backgroundColor    = new Color(0.08f, 0.08f, 0.12f);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[SceneBootstrap] Done. Press Play — Level 1 loads automatically. " +
                  "Use 1/2/3 to switch levels.");
    }

    // ── Shop UI ───────────────────────────────────────────────────────

    static void BuildShopUI()
    {
        var shopRoot = new GameObject("--- SHOP UI ---");
        Undo.RegisterCreatedObjectUndo(shopRoot, "Create Shop UI");

        var canvas          = shopRoot.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;

        var scaler                = shopRoot.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode        = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        shopRoot.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var panel   = new GameObject("ShopPanel");
        Undo.RegisterCreatedObjectUndo(panel, "Create ShopPanel");
        panel.transform.SetParent(shopRoot.transform, false);

        var panelRT              = panel.AddComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(1f, 0f);
        panelRT.anchorMax        = new Vector2(1f, 1f);
        panelRT.pivot            = new Vector2(1f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(290f, 0f);

        var panelImg           = panel.AddComponent<UnityEngine.UI.Image>();
        panelImg.color         = new Color(0.1f, 0.1f, 0.15f, 0.92f);
        panelImg.raycastTarget = false;

        var columns = new (string id, string label, string cost)[][]
        {
            new[] {
                ("basic_tower",    "Basic\nTower",    "3g"),
                ("shotgun_tower",  "Shotgun\nTower",  "5g"),
                ("research_tower", "Research\nTower", "6g"),
                ("slow_tower",     "Slow\nTower",     "3g"),
                ("income_tower",   "Income\nTower",   "5g"),
            },
            new[] {
                ("boomerang_tower",   "Boomerang\nTower",   "5g"),
                ("bee_tower",         "Bee\nTower",         "6g"),
                ("damage_aura_tower", "Damage\nAura",       "7g"),
                ("chain_tower",       "Chain\nTower",       "6g"),
                ("speed_aura_tower",  "Speed\nAura",        "7g"),
                ("entropy_tower",     "Entropy\nTower",     "8g"),
                ("siphon_tower",      "Siphon\nTower",      "4g"),
                ("poison_tower",      "Poison\nTower",      "6g"),
                ("root_tower",        "Root\nTower",        "7g"),
                ("sniper_tower",      "Sniper\nTower",      "8g"),
            },
            new[] {
                ("mortar_tower",    "Mortar\nTower",    "7g"),
                ("railgun_tower",   "Railgun\nTower",   "9g"),
                ("laser_tower",     "Laser\nTower",     "7g"),
                ("collector_tower", "Collector\nTower", "7g"),
            },
        };

        const float btnW     = 85f;
        const float btnH     = 80f;
        const float stepY    = -86f;
        const float startY   = -46f;
        const float colStep  = 95f;
        const float colStart = -colStep;

        for (int col = 0; col < columns.Length; col++)
        {
            float xOff = colStart + colStep * col;
            for (int row = 0; row < columns[col].Length; row++)
            {
                var (towerId, label, cost) = columns[col][row];

                var btnGO = new GameObject($"Buy_{towerId}");
                Undo.RegisterCreatedObjectUndo(btnGO, "Create TowerButton");
                btnGO.transform.SetParent(panel.transform, false);

                var rt              = btnGO.AddComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0.5f, 1f);
                rt.anchorMax        = new Vector2(0.5f, 1f);
                rt.pivot            = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(xOff, startY + stepY * row);
                rt.sizeDelta        = new Vector2(btnW, btnH);

                var img   = btnGO.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(0.18f, 0.20f, 0.28f, 1f);

                var btn = btnGO.AddComponent<UnityEngine.UI.Button>();
                btn.targetGraphic = img;
                var cols = btn.colors;
                cols.highlightedColor = new Color(0.28f, 0.32f, 0.44f, 1f);
                cols.pressedColor     = new Color(0.12f, 0.14f, 0.20f, 1f);
                btn.colors = cols;

                var shopBtn     = btnGO.AddComponent<TowerShopButton>();
                shopBtn.towerId = towerId;
                UnityEventTools.AddPersistentListener(btn.onClick, shopBtn.OnButtonPress);

                var labelGO = new GameObject("Label");
                Undo.RegisterCreatedObjectUndo(labelGO, "Create Label");
                labelGO.transform.SetParent(btnGO.transform, false);
                var labelRT       = labelGO.AddComponent<RectTransform>();
                labelRT.anchorMin = Vector2.zero;
                labelRT.anchorMax = Vector2.one;
                labelRT.offsetMin = Vector2.zero;
                labelRT.offsetMax = Vector2.zero;
                var labelTxt      = labelGO.AddComponent<UnityEngine.UI.Text>();
                labelTxt.text      = $"{label}\n<size=13>({cost})</size>";
                labelTxt.color     = Color.white;
                labelTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                labelTxt.fontSize  = 15;
                labelTxt.alignment = TextAnchor.MiddleCenter;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    static void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Undo.DestroyObjectImmediate(go);
    }

    static GameObject CreateChild(string name, GameObject parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform);
        return go;
    }

    static string EnsureTag(string tag)
    {
        var so   = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        var tags = so.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                return tag;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        so.ApplyModifiedProperties();
        return tag;
    }
}
