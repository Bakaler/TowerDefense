using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using System.Collections.Generic;

/// <summary>
/// TowerDefense → Setup Basic Wave Scene
///
/// Creates/updates scene objects for a basic wave test.
/// PATH nodes are preserved if they already exist — only Managers and Spawner are reset.
/// </summary>
public static class SceneBootstrap
{
    [MenuItem("TowerDefense/Setup Basic Wave Scene")]
    public static void SetupScene()
    {
        bool pathExists = GameObject.Find("--- PATH ---") != null;

        string msg = pathExists
            ? "This will reset Managers and Spawner.\nYour existing path nodes will be preserved.\nProceed?"
            : "This will create Managers, a default S-curve path, and a Spawner.\nProceed?";

        if (!EditorUtility.DisplayDialog("Setup Basic Wave Scene", msg, "Yes", "Cancel"))
            return;

        Undo.SetCurrentGroupName("Setup Basic Wave Scene");
        int undoGroup = Undo.GetCurrentGroup();

        // ── Always reset managers, spawner, and shop UI ───────────────
        DestroyIfExists("--- MANAGERS ---");
        DestroyIfExists("--- SPAWNER ---");
        DestroyIfExists("--- SHOP UI ---");

        // ─────────────────────────────────────────────────────────────
        // 1. MANAGERS
        // ─────────────────────────────────────────────────────────────
        var managersRoot = new GameObject("--- MANAGERS ---");
        Undo.RegisterCreatedObjectUndo(managersRoot, "Create Managers");

        var resourceGO = CreateChild("ResourceManager", managersRoot);
        resourceGO.tag = EnsureTag("ResourceManager");
        resourceGO.AddComponent<ResourceManagerScript>();

        var logicGO = CreateChild("LogicManager", managersRoot);
        logicGO.tag = EnsureTag("Logic");
        logicGO.AddComponent<LogicManager>();

        var roundGO = CreateChild("WaveManager", managersRoot);
        roundGO.AddComponent<WaveManager>();
        roundGO.AddComponent<GameHUD>();
        roundGO.AddComponent<BalanceManager>();
        roundGO.AddComponent<TechManager>();

        var librariesGO = CreateChild("Libraries", managersRoot);
        librariesGO.AddComponent<UnitDefinitionLibrary>();
        librariesGO.AddComponent<UnitFactory>();
        librariesGO.AddComponent<TowerDefinitionLibrary>();
        librariesGO.AddComponent<TowerFactory>();
        librariesGO.AddComponent<EffectLibrary>();
        librariesGO.AddComponent<AbilityLibrary>();
        librariesGO.AddComponent<BehaviorLibrary>();
        librariesGO.AddComponent<TowerPlacer>();

        // ─────────────────────────────────────────────────────────────
        // 2. PATH — only create if it doesn't already exist
        // ─────────────────────────────────────────────────────────────
        PathNode spawnHeadNode = null;

        if (!pathExists)
        {
            var pathRoot = new GameObject("--- PATH ---");
            Undo.RegisterCreatedObjectUndo(pathRoot, "Create Path");

            Vector3[] nodePositions =
            {
                new Vector3(-9f,  0f, 0f),
                new Vector3(-5f,  3f, 0f),
                new Vector3(-1f,  1f, 0f),
                new Vector3( 3f, -2f, 0f),
                new Vector3( 6f,  1f, 0f),
                new Vector3( 9f,  0f, 0f),
            };

            var nodes = new PathNode[nodePositions.Length];
            for (int i = 0; i < nodePositions.Length; i++)
            {
                var nodeGO = new GameObject($"PathNode_{i:00}");
                Undo.RegisterCreatedObjectUndo(nodeGO, "Create PathNode");
                nodeGO.transform.SetParent(pathRoot.transform);
                nodeGO.transform.position = nodePositions[i];
                nodes[i] = nodeGO.AddComponent<PathNode>();
            }

            for (int i = 0; i < nodes.Length - 1; i++)
                nodes[i].connections.Add(nodes[i + 1]);

            var pathGraph           = pathRoot.AddComponent<PathGraph>();
            pathGraph.nodes         = new List<PathNode>(nodes);
            pathGraph.samplesPerSegment = 12;

            var pathVis             = pathRoot.AddComponent<PathVisualizer>();
            pathVis.pathColor       = new Color(1f, 0.75f, 0f, 0.9f);
            pathVis.lineWidth       = 0.1f;
            pathVis.showArrows      = false;
            pathVis.showInGame      = false;

            spawnHeadNode = nodes[0];

            Selection.activeGameObject = pathRoot;
            SceneView.FrameLastActiveSceneView();

            Debug.Log("[SceneBootstrap] Created default S-curve path (6 nodes).");
        }
        else
        {
            // Use the existing path's head node for the spawner
            var existingGraph = Object.FindFirstObjectByType<PathGraph>();
            if (existingGraph != null)
            {
                var heads = existingGraph.GetHeads();
                if (heads.Count > 0) spawnHeadNode = heads[0];
            }
            Debug.Log("[SceneBootstrap] Existing path preserved.");
        }

        // ─────────────────────────────────────────────────────────────
        // 3. UNIT SPAWNER
        // ─────────────────────────────────────────────────────────────
        Vector3 spawnerPos = spawnHeadNode != null
            ? spawnHeadNode.transform.position
            : new Vector3(-9f, 0f, 0f);

        var spawnerRoot = new GameObject("--- SPAWNER ---");
        Undo.RegisterCreatedObjectUndo(spawnerRoot, "Create Spawner");
        spawnerRoot.transform.position = spawnerPos;

        var spawner          = spawnerRoot.AddComponent<UnitSpawner>();
        spawner.headNode     = spawnHeadNode;
        spawner.waves        = new List<WaveEntry>
        {
            new WaveEntry { unitDefinitionId = "basic_enemy",   count = 3, spread = 0.4f, spawnInterval = 0.6f },
            new WaveEntry { unitDefinitionId = "fast_enemy",    count = 2, spread = 0.3f, spawnInterval = 0.4f },
            new WaveEntry { unitDefinitionId = "armored_enemy", count = 2, spread = 0.3f, spawnInterval = 0.8f },
            new WaveEntry { unitDefinitionId = "boss_enemy",    count = 1, spread = 0f,   spawnInterval = 1.0f },
        };
        // WaveManager drives spawner state at runtime via BeginWave()

        // ─────────────────────────────────────────────────────────────
        // 4. SHOP UI — tower purchase buttons on the right side
        // ─────────────────────────────────────────────────────────────
        BuildShopUI(managersRoot);

        // ─────────────────────────────────────────────────────────────
        // 5. CAMERA
        // ─────────────────────────────────────────────────────────────
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

        Debug.Log("[SceneBootstrap] Done. Click 'START WAVE 1' (or press Space) in Play mode to begin.");
    }

    // ── Shop UI ───────────────────────────────────────────────────────

    static void BuildShopUI(GameObject managersRoot)
    {
        var shopRoot = new GameObject("--- SHOP UI ---");
        Undo.RegisterCreatedObjectUndo(shopRoot, "Create Shop UI");

        // Canvas
        var canvas = shopRoot.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;

        var scaler = shopRoot.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        shopRoot.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Right panel background
        var panel = new GameObject("ShopPanel");
        Undo.RegisterCreatedObjectUndo(panel, "Create ShopPanel");
        panel.transform.SetParent(shopRoot.transform, false);

        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(1f, 0f);
        panelRT.anchorMax        = new Vector2(1f, 1f);
        panelRT.pivot            = new Vector2(1f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(290f, 0f);

        var panelImg = panel.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);
        panelImg.raycastTarget = false;   // let world clicks pass through the panel background

        // 3-column layout  (T1 / T2 / T3)
        var columns = new (string id, string label, string cost)[][]
        {
            new[] {
                ("basic_tower",    "Basic\nTower",    "3g"),
                ("shotgun_tower",  "Shotgun\nTower",  "5g"),
                ("slow_tower",     "Slow\nTower",     "5g"),
                ("income_tower",   "Income\nTower",   "5g"),
                ("research_tower", "Research\nTower", "6g"),
            },
            new[] {
                ("chain_tower",     "Chain\nTower",     "6g"),
                ("bee_tower",       "Bee\nTower",       "6g"),
                ("boomerang_tower", "Boomerang\nTower", "5g"),
                ("root_tower",      "Root\nTower",      "7g"),
                ("entropy_tower",   "Entropy\nTower",   "8g"),
                ("poison_tower",    "Poison\nTower",    "6g"),
            },
            new[] {
                ("collector_tower", "Collector\nTower", "7g"),
                ("railgun_tower",   "Railgun\nTower",   "9g"),
                ("laser_tower",     "Laser\nTower",     "7g"),
            },
        };

        float startY   = -80f;
        float stepY    = -110f;
        float btnW     = 85f;
        float btnH     = 85f;
        float colStep  = 95f;
        float colStart = -colStep;   // left column offset from panel center

        for (int col = 0; col < columns.Length; col++)
        {
            float xOff = colStart + colStep * col;
            for (int row = 0; row < columns[col].Length; row++)
            {
            var (towerId, label, cost) = columns[col][row];

            var btnGO = new GameObject($"Buy_{towerId}");
            Undo.RegisterCreatedObjectUndo(btnGO, "Create TowerButton");
            btnGO.transform.SetParent(panel.transform, false);

            var rt = btnGO.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 1f);
            rt.anchorMax        = new Vector2(0.5f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(xOff, startY + stepY * row);
            rt.sizeDelta        = new Vector2(btnW, btnH);

            var img = btnGO.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.18f, 0.20f, 0.28f, 1f);

            var btn = btnGO.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            var cols = btn.colors;
            cols.highlightedColor = new Color(0.28f, 0.32f, 0.44f, 1f);
            cols.pressedColor     = new Color(0.12f, 0.14f, 0.20f, 1f);
            btn.colors = cols;

            // Generic shop button — works for any tower id
            var shopBtn = btnGO.AddComponent<TowerShopButton>();
            shopBtn.towerId = towerId;
            UnityEventTools.AddPersistentListener(btn.onClick, shopBtn.OnButtonPress);

            // Label
            var labelGO = new GameObject("Label");
            Undo.RegisterCreatedObjectUndo(labelGO, "Create Label");
            labelGO.transform.SetParent(btnGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 0f);
            labelRT.anchorMax = new Vector2(1f, 1f);
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var labelTxt = labelGO.AddComponent<UnityEngine.UI.Text>();
            labelTxt.text      = $"{label}\n<size=13>({cost})</size>";
            labelTxt.color     = Color.white;
            labelTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelTxt.fontSize  = 15;
            labelTxt.alignment = TextAnchor.MiddleCenter;
            } // row
        } // col
    }

    // ── Helpers ───────────────────────────────────────────────────────

    static void DestroyIfExists(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null) Undo.DestroyObjectImmediate(existing);
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
        Debug.Log($"[SceneBootstrap] Added missing tag: '{tag}'");
        return tag;
    }
}
