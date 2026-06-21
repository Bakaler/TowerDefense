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
        bool pathAExists   = GameObject.Find("--- PATH ---")   != null;
        bool pathBExists   = GameObject.Find("--- PATH 2 ---") != null;
        bool pathCExists   = GameObject.Find("--- PATH 3 ---") != null;
        bool anyPathExists = pathAExists || pathBExists || pathCExists;

        string msg = anyPathExists
            ? "This will reset Managers and all Spawners.\nExisting path nodes will be preserved.\nProceed?"
            : "This will create Managers, 3 paths, and 3 Spawners.\nProceed?";

        if (!EditorUtility.DisplayDialog("Setup Basic Wave Scene", msg, "Yes", "Cancel"))
            return;

        Undo.SetCurrentGroupName("Setup Basic Wave Scene");
        int undoGroup = Undo.GetCurrentGroup();

        // ── Always reset managers, spawners, and shop UI ─────────────
        DestroyIfExists("--- MANAGERS ---");
        DestroyIfExists("--- SPAWNER ---");
        DestroyIfExists("--- SPAWNER 2 ---");
        DestroyIfExists("--- SPAWNER 3 ---");
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
        // 2. PATHS — only create each path if it doesn't already exist
        //    All three paths share the same PathGraph singleton.
        // ─────────────────────────────────────────────────────────────

        // Locate or build PathGraph (lives on the main path root)
        PathGraph pathGraph = Object.FindFirstObjectByType<PathGraph>();

        PathNode headA = null, headB = null, headC = null;

        // ── PATH A (middle, existing S-curve) ─────────────────────────
        if (!pathAExists)
        {
            var pathRoot = new GameObject("--- PATH ---");
            Undo.RegisterCreatedObjectUndo(pathRoot, "Create Path A");

            Vector3[] posA =
            {
                new Vector3(-9f,  0f, 0f),
                new Vector3(-5f,  3f, 0f),
                new Vector3(-1f,  1f, 0f),
                new Vector3( 3f, -2f, 0f),
                new Vector3( 6f,  1f, 0f),
                new Vector3( 9f,  0f, 0f),
            };

            var nodesA = BuildPathNodes(pathRoot, "PathNode_A", posA);
            headA = nodesA[0];

            // PathGraph lives here; paths B+C register into it
            pathGraph = pathRoot.AddComponent<PathGraph>();
            pathGraph.nodes = new List<PathNode>(nodesA);
            pathGraph.samplesPerSegment = 12;

            var vis = pathRoot.AddComponent<PathVisualizer>();
            vis.pathColor  = new Color(1f, 0.75f, 0f, 0.9f);
            vis.lineWidth  = 0.1f;
            vis.showInGame = false;

            Debug.Log("[SceneBootstrap] Created Path A (middle S-curve).");
        }
        else
        {
            // Find existing head node
            var go = GameObject.Find("PathNode_A_00");
            if (go != null) headA = go.GetComponent<PathNode>();
            if (headA == null && pathGraph != null)
            {
                var heads = pathGraph.GetHeads();
                if (heads.Count > 0) headA = heads[0];
            }
            Debug.Log("[SceneBootstrap] Path A preserved.");
        }

        // ── PATH B (split path — shared entry, forks into upper + lower) ─
        if (!pathBExists)
        {
            var pathRoot = new GameObject("--- PATH 2 ---");
            Undo.RegisterCreatedObjectUndo(pathRoot, "Create Path B");

            // Shared approach section
            var b00 = MakeNode(pathRoot, "PathNode_B_00",   new Vector3(-9f,  5f, 0f));
            var b01 = MakeNode(pathRoot, "PathNode_B_01",   new Vector3(-5f,  5f, 0f));
            var fork = MakeNode(pathRoot, "PathNode_B_fork", new Vector3(-1f,  5f, 0f));

            // Upper branch
            var hi0 = MakeNode(pathRoot, "PathNode_B_hi_0", new Vector3( 2f,  8f, 0f));
            var hi1 = MakeNode(pathRoot, "PathNode_B_hi_1", new Vector3( 6f,  7f, 0f));
            var hiE = MakeNode(pathRoot, "PathNode_B_hi_E", new Vector3( 9f,  8f, 0f));

            // Lower branch
            var lo0 = MakeNode(pathRoot, "PathNode_B_lo_0", new Vector3( 2f,  3f, 0f));
            var lo1 = MakeNode(pathRoot, "PathNode_B_lo_1", new Vector3( 6f,  4f, 0f));
            var loE = MakeNode(pathRoot, "PathNode_B_lo_E", new Vector3( 9f,  3f, 0f));

            // Wire connections
            b00.connections.Add(b01);
            b01.connections.Add(fork);
            fork.connections.Add(hi0);   // junction — two outgoing edges
            fork.connections.Add(lo0);
            hi0.connections.Add(hi1);
            hi1.connections.Add(hiE);    // terminus (no connections)
            lo0.connections.Add(lo1);
            lo1.connections.Add(loE);    // terminus (no connections)

            headB = b00;

            // Register all nodes with the shared PathGraph
            if (pathGraph != null)
            {
                pathGraph.nodes.Add(b00); pathGraph.nodes.Add(b01); pathGraph.nodes.Add(fork);
                pathGraph.nodes.Add(hi0); pathGraph.nodes.Add(hi1); pathGraph.nodes.Add(hiE);
                pathGraph.nodes.Add(lo0); pathGraph.nodes.Add(lo1); pathGraph.nodes.Add(loE);
            }

            // PathVisualizer may auto-add a PathGraph via RequireComponent on older versions;
            // strip it here so the scene only has one PathGraph (on Path A).
            var strayPG = pathRoot.GetComponent<PathGraph>();
            if (strayPG != null) Object.DestroyImmediate(strayPG);

            var vis = pathRoot.AddComponent<PathVisualizer>();
            vis.pathColor  = new Color(0.3f, 0.8f, 1.0f, 0.9f);
            vis.lineWidth  = 0.1f;
            vis.showInGame = false;

            Debug.Log("[SceneBootstrap] Created Path B (split: upper + lower fork).");
        }
        else
        {
            var go = GameObject.Find("PathNode_B_00");
            if (go != null) headB = go.GetComponent<PathNode>();
            Debug.Log("[SceneBootstrap] Path B preserved.");
        }

        // ── PATH C (triple-exit — shared entry, 3-way junction, exits A/B/C) ─
        if (!pathCExists)
        {
            var pathRoot = new GameObject("--- PATH 3 ---");
            Undo.RegisterCreatedObjectUndo(pathRoot, "Create Path C");

            // Shared approach
            var c00  = MakeNode(pathRoot, "PathNode_C_00",   new Vector3(-9f, -5f, 0f));
            var c01  = MakeNode(pathRoot, "PathNode_C_01",   new Vector3(-5f, -4f, 0f));
            var cJxn = MakeNode(pathRoot, "PathNode_C_jxn",  new Vector3(-1f, -4f, 0f));

            // Exit A — upper
            var cA0 = MakeNode(pathRoot, "PathNode_C_A0",   new Vector3( 2f, -1f, 0f));
            var cA1 = MakeNode(pathRoot, "PathNode_C_A1",   new Vector3( 6f, -1f, 0f));
            var cAE = MakeNode(pathRoot, "PathNode_C_AE",   new Vector3( 9f, -1f, 0f));

            // Exit B — middle
            var cB0 = MakeNode(pathRoot, "PathNode_C_B0",   new Vector3( 2f, -4f, 0f));
            var cB1 = MakeNode(pathRoot, "PathNode_C_B1",   new Vector3( 6f, -4f, 0f));
            var cBE = MakeNode(pathRoot, "PathNode_C_BE",   new Vector3( 9f, -4f, 0f));

            // Exit C — lower
            var cC0 = MakeNode(pathRoot, "PathNode_C_C0",   new Vector3( 2f, -7f, 0f));
            var cC1 = MakeNode(pathRoot, "PathNode_C_C1",   new Vector3( 6f, -7f, 0f));
            var cCE = MakeNode(pathRoot, "PathNode_C_CE",   new Vector3( 9f, -7f, 0f));

            // Wire — 3-way junction connects to all three exits equally
            c00.connections.Add(c01);
            c01.connections.Add(cJxn);
            cJxn.connections.Add(cA0);   // ~33% chance each
            cJxn.connections.Add(cB0);
            cJxn.connections.Add(cC0);
            cA0.connections.Add(cA1); cA1.connections.Add(cAE);
            cB0.connections.Add(cB1); cB1.connections.Add(cBE);
            cC0.connections.Add(cC1); cC1.connections.Add(cCE);

            headC = c00;

            if (pathGraph != null)
            {
                pathGraph.nodes.Add(c00);  pathGraph.nodes.Add(c01);  pathGraph.nodes.Add(cJxn);
                pathGraph.nodes.Add(cA0);  pathGraph.nodes.Add(cA1);  pathGraph.nodes.Add(cAE);
                pathGraph.nodes.Add(cB0);  pathGraph.nodes.Add(cB1);  pathGraph.nodes.Add(cBE);
                pathGraph.nodes.Add(cC0);  pathGraph.nodes.Add(cC1);  pathGraph.nodes.Add(cCE);
            }

            var strayPGc = pathRoot.GetComponent<PathGraph>();
            if (strayPGc != null) Object.DestroyImmediate(strayPGc);

            var vis = pathRoot.AddComponent<PathVisualizer>();
            vis.pathColor  = new Color(1.0f, 0.4f, 0.4f, 0.9f);
            vis.lineWidth  = 0.1f;
            vis.showInGame = false;

            Debug.Log("[SceneBootstrap] Created Path C (3-way split: exits A/B/C).");
        }
        else
        {
            var go = GameObject.Find("PathNode_C_00");
            if (go != null) headC = go.GetComponent<PathNode>();
            Debug.Log("[SceneBootstrap] Path C preserved.");
        }

        // ─────────────────────────────────────────────────────────────
        // 3. UNIT SPAWNERS (one per path)
        // ─────────────────────────────────────────────────────────────
        CreateSpawner("--- SPAWNER ---",   headA, pathIndex: 0);
        CreateSpawner("--- SPAWNER 2 ---", headB, pathIndex: 1);
        CreateSpawner("--- SPAWNER 3 ---", headC, pathIndex: 2);

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
                ("chain_tower",       "Chain\nTower",       "6g"),
                ("bee_tower",         "Bee\nTower",         "6g"),
                ("boomerang_tower",   "Boomerang\nTower",   "5g"),
                ("root_tower",        "Root\nTower",        "7g"),
                ("entropy_tower",     "Entropy\nTower",     "8g"),
                ("poison_tower",      "Poison\nTower",      "6g"),
                ("speed_aura_tower",  "Speed\nAura",        "7g"),
                ("damage_aura_tower", "Damage\nAura",       "7g"),
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

    // ── Path / Spawner builders ───────────────────────────────────────

    static PathNode MakeNode(GameObject parent, string nodeName, Vector3 pos)
    {
        var go = new GameObject(nodeName);
        Undo.RegisterCreatedObjectUndo(go, $"Create {nodeName}");
        go.transform.SetParent(parent.transform);
        go.transform.position = pos;
        return go.AddComponent<PathNode>();
    }

    static PathNode[] BuildPathNodes(GameObject parent, string prefix, Vector3[] positions)
    {
        var nodes = new PathNode[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var nodeGO = new GameObject($"{prefix}_{i:00}");
            Undo.RegisterCreatedObjectUndo(nodeGO, $"Create {prefix}_{i:00}");
            nodeGO.transform.SetParent(parent.transform);
            nodeGO.transform.position = positions[i];
            nodes[i] = nodeGO.AddComponent<PathNode>();
        }
        for (int i = 0; i < nodes.Length - 1; i++)
            nodes[i].connections.Add(nodes[i + 1]);
        return nodes;
    }

    static void CreateSpawner(string goName, PathNode head, int pathIndex)
    {
        Vector3 pos = head != null ? head.transform.position : new Vector3(-9f, 0f, 0f);
        var spawnerRoot = new GameObject(goName);
        Undo.RegisterCreatedObjectUndo(spawnerRoot, $"Create {goName}");
        spawnerRoot.transform.position = pos;

        var spawner          = spawnerRoot.AddComponent<UnitSpawner>();
        spawner.headNode     = head;
        spawner.pathIndex    = pathIndex;
        spawner.waves        = new List<WaveEntry>();
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
