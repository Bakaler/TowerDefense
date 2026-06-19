using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
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

        // ── Always reset managers & spawner ───────────────────────────
        DestroyIfExists("--- MANAGERS ---");
        DestroyIfExists("--- SPAWNER ---");

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

        var roundGO = CreateChild("RoundManager", managersRoot);
        roundGO.tag = EnsureTag("RoundManager");
        var roundManager = roundGO.AddComponent<RoundManager>();

        var librariesGO = CreateChild("Libraries", managersRoot);
        librariesGO.AddComponent<UnitDefinitionLibrary>();
        librariesGO.AddComponent<UnitFactory>();
        librariesGO.AddComponent<TowerDefinitionLibrary>();
        librariesGO.AddComponent<TowerFactory>();
        librariesGO.AddComponent<EffectLibrary>();
        librariesGO.AddComponent<AbilityLibrary>();
        librariesGO.AddComponent<TowerPlacer>();

        roundManager.resourceManager = resourceGO.GetComponent<ResourceManagerScript>();

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
        spawner.spawnCount          = 0;
        spawner.formationArrayIndex = -1;

        // ─────────────────────────────────────────────────────────────
        // 4. CAMERA
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

        Debug.Log("[SceneBootstrap] Done. Press SPACE in Play mode to start the wave.");
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
