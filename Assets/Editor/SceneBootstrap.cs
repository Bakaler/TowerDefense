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

        var scaler                 = shopRoot.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        shopRoot.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var panel = new GameObject("ShopPanel");
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

        // TowerShop builds buttons at runtime from the level's allowedTowers list
        var shop       = shopRoot.AddComponent<TowerShop>();
        shop.shopPanel = panelRT;
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
