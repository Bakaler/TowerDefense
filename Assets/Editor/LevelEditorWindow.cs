using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class LevelEditorWindow : EditorWindow
{
    const string RootName   = "[LevelEditor]";
    const string ZonesName  = "Zones";
    const string SessionKey = "LevelEditorWindow_ActiveLevel";
    const int    MaxLevels  = 3;

    int     _activeLevel;
    Vector2 _scroll;

    readonly Dictionary<int, bool> _zoneFoldouts = new();
    readonly Dictionary<int, bool> _pathFoldouts = new();
    readonly Dictionary<int, bool> _waveFoldouts = new();

    WaveCollection _waveData;
    string[]       _unitIds = System.Array.Empty<string>();

    // Group drag-reorder state (within a wave)
    int                           _dragGroupWave = -1;
    int                           _dragGroupIdx  = -1;
    int                           _dropGroupIdx  = -1;
    readonly Dictionary<int, List<float>> _groupTopYs = new();
    readonly Dictionary<int, float>       _groupBotY  = new();

    [MenuItem("TowerDefense/Level Editor")]
    public static void Open() => GetWindow<LevelEditorWindow>("Level Editor");

    void OnEnable() => _activeLevel = SessionState.GetInt(SessionKey, 0);

    // ── GUI ───────────────────────────────────────────────────────────

    void OnGUI()
    {
        GUILayout.Label("Level Editor", EditorStyles.boldLabel);
        GUILayout.Space(6);

        // ── Level buttons ─────────────────────────────────────────────
        GUILayout.Label("Load Level", EditorStyles.miniBoldLabel);
        GUILayout.BeginHorizontal();
        for (int i = 1; i <= MaxLevels; i++)
        {
            bool current = _activeLevel == i && FindRoot() != null;
            GUI.backgroundColor = current ? new Color(0.35f, 0.9f, 0.35f) : Color.white;
            if (GUILayout.Button($"Level {i}", GUILayout.Height(36))) LoadLevel(i);
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        var root = FindRoot();
        if (root == null || _activeLevel == 0)
        {
            if (_activeLevel > 0)
            {
                EditorGUILayout.HelpBox($"Level {_activeLevel} nodes were removed. Load again.", MessageType.Warning);
                _activeLevel = 0;
                SessionState.SetInt(SessionKey, 0);
            }
            else
                EditorGUILayout.HelpBox("Select a level above to load it.", MessageType.Info);
            return;
        }

        GUILayout.Label($"Editing  ›  Level {_activeLevel}", EditorStyles.boldLabel);

        _scroll = GUILayout.BeginScrollView(_scroll);
        GUILayout.Space(6);

        // ── Background ────────────────────────────────────────────────
        GUILayout.Label("Background", EditorStyles.miniBoldLabel);
        GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
        if (GUILayout.Button("Save Background Position  →  JSON", GUILayout.Height(30)))
            SaveBackgroundPosition(_activeLevel, root);
        GUI.backgroundColor = Color.white;

        GUILayout.Space(8);

        // ── Routes ───────────────────────────────────────────────────
        GUILayout.Label("Routes", EditorStyles.miniBoldLabel);

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.4f, 1f, 0.5f);
        if (GUILayout.Button("Save Routes  →  JSON", GUILayout.Height(30)))
            SaveRoutes(_activeLevel, root);
        GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
        if (GUILayout.Button("+ Add Route", GUILayout.Height(30), GUILayout.Width(100)))
            AddRoute(root);
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        DrawPathList(root);

        GUILayout.Space(8);

        // ── Placement zones ───────────────────────────────────────────
        GUILayout.Label("Placement Zones", EditorStyles.miniBoldLabel);

        GUI.backgroundColor = new Color(0.4f, 0.85f, 1f);
        if (GUILayout.Button("Save Zones  →  JSON", GUILayout.Height(30)))
            SaveZones(_activeLevel, root);
        GUI.backgroundColor = Color.white;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Circle Zone", GUILayout.Height(26))) AddZone(root, LevelEditorZone.ZoneType.Circle);
        if (GUILayout.Button("+ Lane Zone",   GUILayout.Height(26))) AddZone(root, LevelEditorZone.ZoneType.Lane);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        DrawZoneList(root);

        GUILayout.Space(8);

        // ── Waves ─────────────────────────────────────────────────────
        GUILayout.Label("Waves", EditorStyles.miniBoldLabel);

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.4f, 1f, 0.5f);
        if (GUILayout.Button("Save Waves  →  JSON", GUILayout.Height(30)))
            SaveWaves(_activeLevel);
        GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
        if (GUILayout.Button("+ Wave", GUILayout.Height(30), GUILayout.Width(70)))
            AddWave();
        GUI.backgroundColor = new Color(1f, 0.9f, 0.4f);
        if (GUILayout.Button("Graph", GUILayout.Height(30), GUILayout.Width(56)))
            WaveGraphWindow.OpenWith(_waveData, _unitIds);
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        DrawWaveList();

        GUILayout.Space(12);

        // ── Discard ───────────────────────────────────────────────────
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("Discard  (remove all editor nodes)", GUILayout.Height(26)))
            if (EditorUtility.DisplayDialog("Discard", $"Remove Level {_activeLevel} editor nodes without saving?", "Discard", "Cancel"))
                ClearRoot();
        GUI.backgroundColor = Color.white;

        GUILayout.Space(8);
        GUILayout.EndScrollView();
    }

    // ── Path list ─────────────────────────────────────────────────────

    void DrawPathList(GameObject root)
    {
        // Collect paths
        var paths = new List<(LevelEditorPath lep, Transform tr)>();
        foreach (Transform child in root.transform)
        {
            var lep = child.GetComponent<LevelEditorPath>();
            if (lep != null) paths.Add((lep, child));
        }

        if (paths.Count == 0)
        {
            GUILayout.Label("  No routes — click + Add Route above.", EditorStyles.miniLabel);
            return;
        }

        // Deferred actions
        int              pathToDelete      = -1;
        LevelEditorPath  pendingAddNodeTo  = null;

        for (int pi = 0; pi < paths.Count; pi++)
        {
            var (lep, pathTr) = paths[pi];
            if (!_pathFoldouts.ContainsKey(pi)) _pathFoldouts[pi] = true;

            // Find head node (not referenced by any other node in this path)
            var allNodes  = CollectNodes(pathTr);
            var refed     = new HashSet<PathNode>();
            foreach (var pn in allNodes)
                foreach (var c in pn.connections)
                    if (c != null) refed.Add(c);
            PathNode headNode = null;
            foreach (var pn in allNodes)
                if (!refed.Contains(pn)) { headNode = pn; break; }

            // ── Path header row ───────────────────────────────────────
            GUILayout.BeginHorizontal();

            _pathFoldouts[pi] = EditorGUILayout.Foldout(
                _pathFoldouts[pi], $"Route {pi + 1}  ({allNodes.Count} nodes)", true);

            // Spawner index field
            GUILayout.Label("Spawner", EditorStyles.miniLabel, GUILayout.Width(48));
            int newIdx = EditorGUILayout.IntField(lep.spawnerIndex, GUILayout.Width(28));
            if (newIdx != lep.spawnerIndex)
            {
                Undo.RecordObject(lep, "Change Spawner Index");
                lep.spawnerIndex = newIdx;
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            if (GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(30)))
                Selection.activeGameObject = pathTr.gameObject;

            if (GUILayout.Button("+Node", EditorStyles.miniButton, GUILayout.Width(46)))
                pendingAddNodeTo = lep;

            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                pathToDelete = pi;
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();

            // ── Node sub-rows ─────────────────────────────────────────
            if (_pathFoldouts[pi])
            {
                foreach (Transform nodeTr in pathTr)
                {
                    var pn = nodeTr.GetComponent<PathNode>();
                    if (pn == null) continue;

                    bool isHead = pn == headNode;
                    bool isTerm = pn.IsTerminus;

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(18);

                    // Tag
                    string tag = isHead ? "[HEAD]" : isTerm ? "[END]" : "";
                    if (!string.IsNullOrEmpty(tag))
                    {
                        var style = new GUIStyle(EditorStyles.miniLabel);
                        style.normal.textColor = isHead ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.4f, 0.4f);
                        GUILayout.Label(tag, style, GUILayout.Width(46));
                    }
                    else
                        GUILayout.Space(46);

                    GUILayout.Label(nodeTr.name, EditorStyles.miniLabel, GUILayout.Width(40));
                    GUILayout.Label(
                        $"({nodeTr.position.x:F1}, {nodeTr.position.y:F1})",
                        EditorStyles.miniLabel, GUILayout.MinWidth(80));

                    if (GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(30)))
                    {
                        Selection.activeGameObject = nodeTr.gameObject;
                        EditorGUIUtility.PingObject(nodeTr.gameObject);
                    }
                    GUILayout.EndHorizontal();
                }

                // Head node spawner hint
                if (headNode != null)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(18);
                    var hint = new GUIStyle(EditorStyles.miniLabel);
                    hint.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                    GUILayout.Label($"↑ Spawner {lep.spawnerIndex} attaches here at runtime", hint);
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(4);
            }
        }

        // ── Deferred mutations ────────────────────────────────────────
        if (pathToDelete >= 0 && pathToDelete < paths.Count)
        {
            _pathFoldouts.Remove(pathToDelete);
            Undo.DestroyObjectImmediate(paths[pathToDelete].tr.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Repaint();
        }

        if (pendingAddNodeTo != null)
            AddNodeToPath(pendingAddNodeTo);
    }

    // ── Add route ─────────────────────────────────────────────────────

    void AddRoute(GameObject root)
    {
        // Auto-assign next spawner index
        int nextIndex = 0;
        foreach (Transform child in root.transform)
        {
            var lep = child.GetComponent<LevelEditorPath>();
            if (lep != null) nextIndex = Mathf.Max(nextIndex, lep.spawnerIndex + 1);
        }

        var pathGO = new GameObject($"Path_{nextIndex}");
        Undo.RegisterCreatedObjectUndo(pathGO, "Add Route");
        pathGO.transform.SetParent(root.transform);
        var path = pathGO.AddComponent<LevelEditorPath>();
        path.spawnerIndex = nextIndex;

        // Seed two nodes
        var n0GO = new GameObject("n0");
        Undo.RegisterCreatedObjectUndo(n0GO, "Add Route Node");
        n0GO.transform.SetParent(pathGO.transform);
        n0GO.transform.position = new Vector3(-3f, 0f, 0f);
        var n0 = n0GO.AddComponent<PathNode>();

        var n1GO = new GameObject("n1");
        Undo.RegisterCreatedObjectUndo(n1GO, "Add Route Node");
        n1GO.transform.SetParent(pathGO.transform);
        n1GO.transform.position = new Vector3(3f, 0f, 0f);
        n1GO.AddComponent<PathNode>();

        n0.connections.Add(n1GO.GetComponent<PathNode>());

        // Auto-expand
        int newIdx = 0;
        foreach (Transform child in root.transform)
            if (child.GetComponent<LevelEditorPath>() != null) newIdx++;
        _pathFoldouts[newIdx - 1] = true;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = n0GO;
        EditorGUIUtility.PingObject(n0GO);
        Repaint();
    }

    // ── Add node to path ──────────────────────────────────────────────

    void AddNodeToPath(LevelEditorPath lep)
    {
        var pathTr   = lep.transform;
        var allNodes = CollectNodes(pathTr);

        // Find terminus nodes (no connections) — new node connects from them
        var termini = new List<PathNode>();
        foreach (var pn in allNodes)
            if (pn.IsTerminus) termini.Add(pn);

        // Position: offset right from last terminus, or from path origin
        Vector3 newPos = termini.Count > 0
            ? termini[termini.Count - 1].transform.position + new Vector3(2f, 0f, 0f)
            : pathTr.position + new Vector3(2f, 0f, 0f);

        int newId  = allNodes.Count;
        var nodeGO = new GameObject($"n{newId}");
        Undo.RegisterCreatedObjectUndo(nodeGO, "Add Path Node");
        nodeGO.transform.SetParent(pathTr);
        nodeGO.transform.position = newPos;
        var newPn = nodeGO.AddComponent<PathNode>();

        // Wire last terminus → new node
        foreach (var t in termini)
        {
            Undo.RecordObject(t, "Wire Path Node");
            t.connections.Add(newPn);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorApplication.delayCall += () =>
        {
            Selection.activeGameObject = nodeGO;
            EditorGUIUtility.PingObject(nodeGO);
            Repaint();
        };
    }

    // ── Zone list ─────────────────────────────────────────────────────

    void DrawZoneList(GameObject root)
    {
        var zonesRoot    = FindZonesRoot(root);
        int zoneToDelete = -1;
        LevelEditorZone pendingAddPointZone = null;
        int             pointToDelete       = -1;
        LevelEditorZone pointToDeleteZone   = null;

        if (zonesRoot == null || zonesRoot.childCount == 0)
        {
            GUILayout.Label("  No zones — add above.", EditorStyles.miniLabel);
        }
        else
        {
            for (int i = 0; i < zonesRoot.childCount; i++)
            {
                var zTr  = zonesRoot.GetChild(i);
                var zone = zTr.GetComponent<LevelEditorZone>();
                if (zone == null) continue;

                if (!_zoneFoldouts.ContainsKey(i)) _zoneFoldouts[i] = false;

                string typeTag = zone.type == LevelEditorZone.ZoneType.Lane
                    ? $"Lane  {zone.GetPoints().Count} pts"
                    : $"Circle  r={zone.radius:F1}";

                GUILayout.BeginHorizontal();
                _zoneFoldouts[i] = EditorGUILayout.Foldout(_zoneFoldouts[i], $"Zone {i + 1}  [{typeTag}]", true);

                if (GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(32)))
                    Selection.activeGameObject = zTr.gameObject;

                if (zone.type == LevelEditorZone.ZoneType.Lane)
                    if (GUILayout.Button("+Pt", EditorStyles.miniButton, GUILayout.Width(30)))
                        pendingAddPointZone = zone;

                GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                    zoneToDelete = i;
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                if (_zoneFoldouts[i] && zone.type == LevelEditorZone.ZoneType.Lane)
                {
                    var pts = zone.GetPoints();
                    if (pts.Count == 0)
                        GUILayout.Label("     (no points)", EditorStyles.miniLabel);
                    else
                        for (int pi = 0; pi < pts.Count; pi++)
                        {
                            var lp = pts[pi];
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(20);
                            GUILayout.Label(
                                $"Pt {pi}  ({lp.transform.position.x:F1}, {lp.transform.position.y:F1})",
                                EditorStyles.miniLabel, GUILayout.MinWidth(100));

                            GUILayout.Label("w", EditorStyles.miniLabel, GUILayout.Width(10));
                            float newWidth = EditorGUILayout.FloatField(lp.width, GUILayout.Width(38));
                            if (!Mathf.Approximately(newWidth, lp.width))
                            {
                                Undo.RecordObject(lp, "Change Lane Point Width");
                                lp.width = Mathf.Max(0.1f, newWidth);
                                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                            }

                            if (GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(32)))
                            {
                                Selection.activeGameObject = lp.gameObject;
                                EditorGUIUtility.PingObject(lp.gameObject);
                            }

                            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
                            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                            { pointToDelete = pi; pointToDeleteZone = zone; }
                            GUI.backgroundColor = Color.white;
                            GUILayout.EndHorizontal();
                        }
                }
            }
        }

        // Deferred
        if (zoneToDelete >= 0 && zonesRoot != null && zoneToDelete < zonesRoot.childCount)
        {
            _zoneFoldouts.Remove(zoneToDelete);
            Undo.DestroyObjectImmediate(zonesRoot.GetChild(zoneToDelete).gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Repaint();
        }

        if (pointToDelete >= 0 && pointToDeleteZone != null)
        {
            var pts = pointToDeleteZone.GetPoints();
            if (pointToDelete < pts.Count)
            {
                Undo.DestroyObjectImmediate(pts[pointToDelete].gameObject);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Repaint();
            }
        }

        if (pendingAddPointZone != null)
        {
            var pts     = pendingAddPointZone.GetPoints();
            Vector3 pos = pts.Count > 0
                ? pts[pts.Count - 1].transform.position + Vector3.right
                : pendingAddPointZone.transform.position;
            float lastWidth = pts.Count > 0 ? pts[pts.Count - 1].width : 3f;
            Undo.RegisterFullObjectHierarchyUndo(pendingAddPointZone.gameObject, "Add Lane Point");
            var newPt = pendingAddPointZone.AddPoint(pos, lastWidth);
            EditorApplication.delayCall += () =>
            {
                Selection.activeGameObject = newPt.gameObject;
                EditorGUIUtility.PingObject(newPt.gameObject);
                Repaint();
            };
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Repaint();
        }
    }

    // ── Load ──────────────────────────────────────────────────────────

    void LoadLevel(int levelNumber)
    {
        if (_activeLevel > 0 && _activeLevel != levelNumber && FindRoot() != null)
            if (!EditorUtility.DisplayDialog("Switch Level",
                $"Discard unsaved edits to Level {_activeLevel} and load Level {levelNumber}?",
                "Switch", "Cancel"))
                return;

        var data = ReadJSON(levelNumber);
        if (data == null)
        {
            EditorUtility.DisplayDialog("Not Found",
                $"Resources/Definitions/Levels/level_{levelNumber}.json not found.", "OK");
            return;
        }

        ClearRoot();

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Load Level");

        // Background
        if (!string.IsNullOrEmpty(data.backgroundSprite))
        {
            var bgGO = new GameObject("[Background]");
            Undo.RegisterCreatedObjectUndo(bgGO, "Create Background");
            bgGO.transform.SetParent(root.transform);
            bgGO.transform.position = new Vector3(data.backgroundX, data.backgroundY, 0f);
            var sr = bgGO.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -100;
            var sp = LoadSpriteAnywhere(data.backgroundSprite);
            if (sp != null) sr.sprite = sp;
            else Debug.LogWarning($"[LevelEditor] Sprite not found: {data.backgroundSprite}");
        }

        // Routes
        if (data.paths != null)
        {
            for (int pi = 0; pi < data.paths.Length; pi++)
            {
                var pathData = data.paths[pi];
                var pathGO   = new GameObject($"Path_{pi}");
                Undo.RegisterCreatedObjectUndo(pathGO, $"Create Path_{pi}");
                pathGO.transform.SetParent(root.transform);
                var lep = pathGO.AddComponent<LevelEditorPath>();
                lep.spawnerIndex = pathData.spawnerIndex;

                var nodeMap = new Dictionary<string, PathNode>();
                if (pathData.nodes != null)
                {
                    foreach (var nd in pathData.nodes)
                    {
                        var go = new GameObject(nd.id);
                        Undo.RegisterCreatedObjectUndo(go, $"Create node {nd.id}");
                        go.transform.SetParent(pathGO.transform);
                        go.transform.position = new Vector3(nd.x, nd.y, 0f);
                        nodeMap[nd.id] = go.AddComponent<PathNode>();
                    }
                    foreach (var nd in pathData.nodes)
                    {
                        if (nd.next == null) continue;
                        var pn = nodeMap[nd.id];
                        foreach (var nxtId in nd.next)
                            if (nodeMap.TryGetValue(nxtId, out var nxt))
                                pn.connections.Add(nxt);
                    }
                }

                _pathFoldouts[pi] = false;
            }
        }

        // Zones
        var zonesGO = new GameObject(ZonesName);
        Undo.RegisterCreatedObjectUndo(zonesGO, "Create Zones");
        zonesGO.transform.SetParent(root.transform);

        if (data.placementZones != null)
        {
            for (int zi = 0; zi < data.placementZones.Length; zi++)
            {
                var zd  = data.placementZones[zi];
                var zGO = new GameObject($"Zone_{zi + 1}");
                Undo.RegisterCreatedObjectUndo(zGO, $"Create Zone_{zi + 1}");
                zGO.transform.SetParent(zonesGO.transform);
                var zone = zGO.AddComponent<LevelEditorZone>();

                if (zd.type == "lane" && zd.points != null && zd.points.Length >= 2)
                {
                    zone.type = LevelEditorZone.ZoneType.Lane;
                    foreach (var p in zd.points)
                        zone.AddPoint(new Vector3(p.x, p.y, 0f), p.width);
                }
                else
                {
                    zone.type   = LevelEditorZone.ZoneType.Circle;
                    zone.radius = zd.radius;
                    zGO.transform.position = new Vector3(zd.x, zd.y, 0f);
                }
            }
        }

        // Waves
        _waveData = ReadWaves(levelNumber) ?? new WaveCollection();
        _waveFoldouts.Clear();
        _groupExpanded.Clear();
        WaveGraphWindow.Refresh(_waveData, _unitIds);

        // Unit IDs for dropdown
        _unitIds = LoadUnitIds();

        _activeLevel = levelNumber;
        SessionState.SetInt(SessionKey, levelNumber);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        SceneView.FrameLastActiveSceneView();
        Repaint();
        Debug.Log($"[LevelEditor] Loaded Level {levelNumber}.");
    }

    // ── Save background ───────────────────────────────────────────────

    void SaveBackgroundPosition(int levelNumber, GameObject root)
    {
        var bgTr = root.transform.Find("[Background]");
        if (bgTr == null) { EditorUtility.DisplayDialog("No Background", "No [Background] found.", "OK"); return; }
        var data = ReadJSON(levelNumber) ?? MakeBlank(levelNumber);
        data.backgroundX = Round(bgTr.position.x);
        data.backgroundY = Round(bgTr.position.y);
        WriteJSON(levelNumber, data);
        EditorUtility.DisplayDialog("Saved", $"Background saved  ({data.backgroundX}, {data.backgroundY})", "OK");
    }

    // ── Save routes ───────────────────────────────────────────────────

    void SaveRoutes(int levelNumber, GameObject root)
    {
        var data  = ReadJSON(levelNumber) ?? MakeBlank(levelNumber);
        var paths = new List<PathData>();

        foreach (Transform pathTr in root.transform)
        {
            var lep = pathTr.GetComponent<LevelEditorPath>();
            if (lep == null) continue;

            var pnToId = new Dictionary<PathNode, string>();
            foreach (Transform nodeTr in pathTr)
            {
                var pn = nodeTr.GetComponent<PathNode>();
                if (pn != null) pnToId[pn] = nodeTr.name;
            }

            var nodes = new List<NodeData>();
            foreach (Transform nodeTr in pathTr)
            {
                var pn = nodeTr.GetComponent<PathNode>();
                if (pn == null) continue;
                var nextIds = new List<string>();
                foreach (var conn in pn.connections)
                    if (conn != null && pnToId.TryGetValue(conn, out var nid))
                        nextIds.Add(nid);
                nodes.Add(new NodeData
                {
                    id   = nodeTr.name,
                    x    = Round(nodeTr.position.x),
                    y    = Round(nodeTr.position.y),
                    next = nextIds.ToArray(),
                });
            }
            paths.Add(new PathData { spawnerIndex = lep.spawnerIndex, nodes = nodes.ToArray() });
        }

        data.paths = paths.ToArray();
        WriteJSON(levelNumber, data);
        EditorUtility.DisplayDialog("Saved", $"Level {levelNumber} routes saved.", "OK");
    }

    // ── Save zones ────────────────────────────────────────────────────

    void SaveZones(int levelNumber, GameObject root)
    {
        var data      = ReadJSON(levelNumber) ?? MakeBlank(levelNumber);
        var zonesRoot = FindZonesRoot(root);
        var zones     = new List<ZoneData>();

        if (zonesRoot != null)
        {
            foreach (Transform zTr in zonesRoot)
            {
                var zone = zTr.GetComponent<LevelEditorZone>();
                if (zone == null) continue;

                if (zone.type == LevelEditorZone.ZoneType.Lane)
                {
                    var pts    = zone.GetPoints();
                    var ptData = new VertexData[pts.Count];
                    for (int i = 0; i < pts.Count; i++)
                    {
                        var lp = pts[i];
                        ptData[i] = new VertexData
                        {
                            x     = Round(lp.transform.position.x),
                            y     = Round(lp.transform.position.y),
                            width = Round(lp.width),
                        };
                    }
                    float zoneWidth = ptData.Length > 0 ? ptData[0].width : 3f;
                    zones.Add(new ZoneData { type = "lane", width = zoneWidth, points = ptData });
                }
                else
                {
                    zones.Add(new ZoneData
                    {
                        type   = "circle",
                        x      = Round(zTr.position.x),
                        y      = Round(zTr.position.y),
                        radius = Round(zone.radius),
                    });
                }
            }
        }

        data.placementZones = zones.ToArray();
        WriteJSON(levelNumber, data);
        EditorUtility.DisplayDialog("Saved", $"Level {levelNumber} zones saved.", "OK");
    }

    // ── Add zone ──────────────────────────────────────────────────────

    void AddZone(GameObject root, LevelEditorZone.ZoneType zoneType)
    {
        var zonesRoot = FindZonesRoot(root);
        if (zonesRoot == null) return;

        int idx = zonesRoot.childCount + 1;
        var zGO = new GameObject($"Zone_{idx}");
        Undo.RegisterCreatedObjectUndo(zGO, "Add Zone");
        zGO.transform.SetParent(zonesRoot.transform);
        zGO.transform.position = Vector3.zero;

        var zone  = zGO.AddComponent<LevelEditorZone>();
        zone.type = zoneType;

        if (zoneType == LevelEditorZone.ZoneType.Lane)
        {
            zone.AddPoint(new Vector3(-2f, 0f, 0f), 3f);
            zone.AddPoint(new Vector3( 2f, 0f, 0f), 3f);
        }
        else
            zone.radius = 2f;

        _zoneFoldouts[idx - 1] = true;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = zGO;
        EditorGUIUtility.PingObject(zGO);
        Repaint();
    }

    // ── Helpers ───────────────────────────────────────────────────────

    static List<PathNode> CollectNodes(Transform pathTr)
    {
        var list = new List<PathNode>();
        foreach (Transform child in pathTr)
        {
            var pn = child.GetComponent<PathNode>();
            if (pn != null) list.Add(pn);
        }
        return list;
    }

    static Sprite LoadSpriteAnywhere(string spritePath)
    {
        var sp = Resources.Load<Sprite>(spritePath);
        if (sp != null) return sp;
        string[] roots = { "Assets/Resources/", "Assets/" };
        string[] exts  = { ".png", ".jpg", ".jpeg", ".psd", ".tga" };
        foreach (var r in roots)
            foreach (var ext in exts)
            {
                var loaded = AssetDatabase.LoadAssetAtPath<Sprite>($"{r}{spritePath}{ext}");
                if (loaded != null) return loaded;
            }
        return null;
    }

    LevelData ReadJSON(int n)
    {
        var ta = Resources.Load<TextAsset>($"Definitions/Levels/level_{n}");
        return ta != null ? JsonUtility.FromJson<LevelData>(ta.text) : null;
    }

    void WriteJSON(int n, LevelData data)
    {
        string dir  = Path.Combine(Application.dataPath, "Resources", "Definitions", "Levels");
        string file = Path.Combine(dir, $"level_{n}.json");
        Directory.CreateDirectory(dir);
        File.WriteAllText(file, JsonUtility.ToJson(data, true));
        AssetDatabase.Refresh();
        Debug.Log($"[LevelEditor] Saved level_{n}.json");
    }

    // ── Wave list ─────────────────────────────────────────────────────

    // Per-group expand (timestamps list) state
    readonly Dictionary<(int, int), bool> _groupExpanded = new();

    static readonly Color SpawnerDividerColor = new Color(0.35f, 0.35f, 0.45f, 1f);
    static readonly Color DropLineColor       = new Color(0.3f, 0.7f, 1f, 1f);

    void DrawWaveList()
    {
        if (_waveData == null || _waveData.waves == null || _waveData.waves.Count == 0)
        {
            GUILayout.Label("  No waves — click + Wave above.", EditorStyles.miniLabel);
            return;
        }

        var  evt       = Event.current;
        bool isRepaint = evt.type == EventType.Repaint;

        int  waveToDelete   = -1;
        int  addGroupToWave = -1;
        (int wave, int grp) groupToDelete = (-1, -1);
        bool dataChanged = false;

        for (int wi = 0; wi < _waveData.waves.Count; wi++)
        {
            var wave = _waveData.waves[wi];
            if (!_waveFoldouts.ContainsKey(wi)) _waveFoldouts[wi] = true;

            float waveEndTime = 0f;
            foreach (var g in wave.groups)
                waveEndTime = Mathf.Max(waveEndTime, g.EndTime);

            // ── Wave header ───────────────────────────────────────────
            GUILayout.BeginHorizontal();
            _waveFoldouts[wi] = EditorGUILayout.Foldout(
                _waveFoldouts[wi],
                $"Wave {wi + 1}  ({wave.groups.Count} group{(wave.groups.Count == 1 ? "" : "s")})  ends {waveEndTime:F1}s",
                true);
            GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
            if (GUILayout.Button("+Group", EditorStyles.miniButton, GUILayout.Width(54)))
                addGroupToWave = wi;
            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                waveToDelete = wi;
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            // ── Group rows (with spawner sections + drag) ─────────────
            if (_waveFoldouts[wi])
            {
                if (!_groupTopYs.ContainsKey(wi)) _groupTopYs[wi] = new List<float>();
                var topYs = _groupTopYs[wi];
                while (topYs.Count < wave.groups.Count) topYs.Add(0f);

                if (wave.groups.Count == 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    GUILayout.Label("(empty)", EditorStyles.miniLabel);
                    GUILayout.EndHorizontal();
                }

                int lastSpawner = int.MinValue;

                for (int gi = 0; gi < wave.groups.Count; gi++)
                {
                    var g   = wave.groups[gi];
                    var key = (wi, gi);
                    if (!_groupExpanded.ContainsKey(key)) _groupExpanded[key] = false;

                    // ── Spawner section divider ────────────────────────
                    if (g.spawnerIndex != lastSpawner)
                    {
                        GUILayout.Space(4);
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(14);
                        var divStyle = new GUIStyle(EditorStyles.miniLabel);
                        divStyle.normal.textColor = new Color(0.55f, 0.75f, 1f);
                        GUILayout.Label($"── Spawner {g.spawnerIndex} ──────────────────", divStyle);
                        GUILayout.EndHorizontal();
                        lastSpawner = g.spawnerIndex;
                    }

                    // Capture top Y for this group
                    GUILayout.Space(0);
                    if (isRepaint && gi < topYs.Count)
                        topYs[gi] = GUILayoutUtility.GetLastRect().yMax;

                    // Drop indicator above this group
                    if (_dragGroupWave == wi && _dropGroupIdx == gi && isRepaint && gi < topYs.Count)
                        EditorGUI.DrawRect(new Rect(14, topYs[gi] - 1f, EditorGUIUtility.currentViewWidth - 14f, 2f), DropLineColor);

                    // ── Group row ──────────────────────────────────────
                    bool isDragging = _dragGroupWave == wi && _dragGroupIdx == gi;

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(14);

                    // Drag handle
                    var hStyle = new GUIStyle(EditorStyles.label);
                    hStyle.normal.textColor = isDragging ? new Color(0.3f, 0.7f, 1f) : new Color(0.55f, 0.55f, 0.55f);
                    var hRect = GUILayoutUtility.GetRect(new GUIContent("≡"), hStyle,
                        GUILayout.Width(16), GUILayout.Height(16));
                    EditorGUIUtility.AddCursorRect(hRect, MouseCursor.Pan);
                    if (isRepaint) GUI.Label(hRect, "≡", hStyle);

                    if (evt.type == EventType.MouseDown && hRect.Contains(evt.mousePosition))
                    {
                        _dragGroupWave = wi;
                        _dragGroupIdx  = gi;
                        _dropGroupIdx  = gi;
                        evt.Use();
                    }

                    if (isDragging)
                    {
                        // Dim the row while dragging
                        var dimStyle = new GUIStyle(EditorStyles.miniLabel);
                        dimStyle.normal.textColor = new Color(0.4f, 0.4f, 0.4f);
                        GUILayout.Label($"{g.unitDefinitionId}  x{g.count}  @{g.startTime:F1}s", dimStyle);
                    }
                    else
                    {
                        int curIdx = System.Array.IndexOf(_unitIds, g.unitDefinitionId);
                        if (curIdx < 0) curIdx = 0;
                        int newUnitIdx = EditorGUILayout.Popup(curIdx, _unitIds, GUILayout.MinWidth(110));
                        if (newUnitIdx != curIdx && _unitIds.Length > 0)
                        { g.unitDefinitionId = _unitIds[newUnitIdx]; dataChanged = true; }

                        GUILayout.Label("x", EditorStyles.miniLabel, GUILayout.Width(10));
                        int newCount = EditorGUILayout.IntField(g.count, GUILayout.Width(30));
                        if (newCount != g.count) { g.count = Mathf.Max(1, newCount); dataChanged = true; }

                        GUILayout.Label("Ivl", EditorStyles.miniLabel, GUILayout.Width(20));
                        float newIvl = EditorGUILayout.FloatField(g.spawnInterval, GUILayout.Width(36));
                        if (!Mathf.Approximately(newIvl, g.spawnInterval)) { g.spawnInterval = Mathf.Max(0.05f, newIvl); dataChanged = true; }

                        GUILayout.Label("@", EditorStyles.miniLabel, GUILayout.Width(10));
                        float newStart = EditorGUILayout.FloatField(g.startTime, GUILayout.Width(36));
                        if (!Mathf.Approximately(newStart, g.startTime)) { g.startTime = Mathf.Max(0f, newStart); dataChanged = true; }

                        GUILayout.Label("Spn", EditorStyles.miniLabel, GUILayout.Width(24));
                        int newSpn = EditorGUILayout.IntField(g.spawnerIndex, GUILayout.Width(24));
                        if (newSpn != g.spawnerIndex) { g.spawnerIndex = newSpn; dataChanged = true; }

                        var endStyle = new GUIStyle(EditorStyles.miniLabel);
                        endStyle.normal.textColor = new Color(0.55f, 0.85f, 0.55f);
                        GUILayout.Label($"→{g.EndTime:F1}s", endStyle, GUILayout.Width(44));

                        bool wasExp = _groupExpanded[key];
                        _groupExpanded[key] = GUILayout.Toggle(wasExp, "▾", EditorStyles.miniButton, GUILayout.Width(20));

                        GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
                        if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                            groupToDelete = (wi, gi);
                        GUI.backgroundColor = Color.white;
                    }

                    GUILayout.EndHorizontal();

                    // Timestamp expand list
                    if (!isDragging && _groupExpanded[key])
                    {
                        for (int si = 0; si < g.count; si++)
                        {
                            float t = g.startTime + si * g.spawnInterval;
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(36);
                            var tsStyle = new GUIStyle(EditorStyles.miniLabel);
                            tsStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
                            GUILayout.Label($"{t:F2}s  {g.unitDefinitionId}", tsStyle);
                            GUILayout.EndHorizontal();
                        }
                    }
                }

                // Bottom Y of this wave's group list (for drop indicator after last group)
                GUILayout.Space(0);
                if (isRepaint) _groupBotY[wi] = GUILayoutUtility.GetLastRect().yMax;

                // Drop indicator after last group
                if (_dragGroupWave == wi && _dropGroupIdx == wave.groups.Count && isRepaint
                    && _groupBotY.TryGetValue(wi, out float botY))
                    EditorGUI.DrawRect(new Rect(14, botY, EditorGUIUtility.currentViewWidth - 14f, 2f), DropLineColor);

                // ── Group drag events ──────────────────────────────────
                if (_dragGroupWave == wi)
                {
                    if (evt.type == EventType.MouseDrag)
                    {
                        _dropGroupIdx = ComputeGroupDropIdx(wi, evt.mousePosition.y);
                        Repaint();
                        evt.Use();
                    }
                    else if (evt.type == EventType.MouseUp)
                    {
                        int from = _dragGroupIdx;
                        int to   = _dropGroupIdx;
                        if (from >= 0 && to >= 0 && from != to && to <= wave.groups.Count)
                        {
                            var moved = wave.groups[from];
                            wave.groups.RemoveAt(from);
                            int insertAt = to > from ? to - 1 : to;
                            wave.groups.Insert(insertAt, moved);
                            dataChanged = true;
                        }
                        _dragGroupWave = -1;
                        _dragGroupIdx  = -1;
                        _dropGroupIdx  = -1;
                        Repaint();
                        evt.Use();
                    }
                }
            }

            GUILayout.Space(4);
        }

        // ── Deferred mutations ────────────────────────────────────────
        if (waveToDelete >= 0 && waveToDelete < _waveData.waves.Count)
        {
            _waveData.waves.RemoveAt(waveToDelete);
            _waveFoldouts.Remove(waveToDelete);
            dataChanged = true;
            Repaint();
        }

        if (groupToDelete.wave >= 0 && groupToDelete.wave < _waveData.waves.Count)
        {
            var grps = _waveData.waves[groupToDelete.wave].groups;
            if (groupToDelete.grp >= 0 && groupToDelete.grp < grps.Count)
            {
                grps.RemoveAt(groupToDelete.grp);
                _groupExpanded.Remove(groupToDelete);
                dataChanged = true;
                Repaint();
            }
        }

        if (addGroupToWave >= 0 && addGroupToWave < _waveData.waves.Count)
        {
            float nextStart = 0f;
            foreach (var g in _waveData.waves[addGroupToWave].groups)
                nextStart = Mathf.Max(nextStart, g.EndTime);

            _waveData.waves[addGroupToWave].groups.Add(new WaveEntry
            {
                unitDefinitionId = _unitIds.Length > 0 ? _unitIds[0] : "basic_enemy",
                count            = 5,
                spawnInterval    = 1.0f,
                startTime        = nextStart,
                spawnerIndex     = 0,
            });
            _waveFoldouts[addGroupToWave] = true;
            dataChanged = true;
            Repaint();
        }

        if (dataChanged)
            WaveGraphWindow.Refresh(_waveData, _unitIds);
    }

    int ComputeGroupDropIdx(int waveIdx, float mouseY)
    {
        if (!_groupTopYs.TryGetValue(waveIdx, out var topYs)) return 0;
        int count = _waveData.waves[waveIdx].groups.Count;
        for (int i = 0; i < count; i++)
        {
            float topY = i < topYs.Count ? topYs[i] : float.MaxValue;
            float botY = i + 1 < topYs.Count ? topYs[i + 1]
                       : (_groupBotY.TryGetValue(waveIdx, out var b) ? b : float.MaxValue);
            if (mouseY < (topY + botY) * 0.5f) return i;
        }
        return count;
    }

    void AddWave()
    {
        if (_waveData == null) _waveData = new WaveCollection();
        _waveData.waves.Add(new WaveDefinition());
        _waveFoldouts[_waveData.waves.Count - 1] = true;
        Repaint();
    }

    void SaveWaves(int levelNumber)
    {
        if (_waveData == null) { EditorUtility.DisplayDialog("No Waves", "No wave data to save.", "OK"); return; }
        string dir  = Path.Combine(Application.dataPath, "Resources", "Definitions", "Levels");

        // Write standalone waves file (editor source of truth)
        File.WriteAllText(Path.Combine(dir, $"waves_{levelNumber}.json"), JsonUtility.ToJson(_waveData, true));

        // Also push waves into level_{n}.json so the game sees the same data
        var levelData = ReadJSON(levelNumber);
        if (levelData != null)
        {
            levelData.waves = _waveData.waves.ToArray();
            WriteJSON(levelNumber, levelData);
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Saved",
            $"waves_{levelNumber}.json + level_{levelNumber}.json updated  ({_waveData.waves.Count} waves).", "OK");
        Debug.Log($"[LevelEditor] Saved waves_{levelNumber}.json and level_{levelNumber}.json");
    }

    WaveCollection ReadWaves(int n)
    {
        // Prefer standalone waves file
        var ta = Resources.Load<TextAsset>($"Definitions/Levels/waves_{n}");
        if (ta != null) return JsonUtility.FromJson<WaveCollection>(ta.text);

        // Fall back to waves embedded in level file
        var ld = ReadJSON(n);
        if (ld?.waves != null && ld.waves.Length > 0)
        {
            var col = new WaveCollection();
            col.waves.AddRange(ld.waves);
            return col;
        }
        return null;
    }

    string[] LoadUnitIds()
    {
        var ta = Resources.Load<TextAsset>("Definitions/units");
        if (ta == null) return new[] { "basic_enemy" };
        var col = JsonUtility.FromJson<UnitDefCollection>(ta.text);
        if (col?.units == null || col.units.Count == 0) return new[] { "basic_enemy" };
        var ids = new string[col.units.Count];
        for (int i = 0; i < col.units.Count; i++) ids[i] = col.units[i].id;
        return ids;
    }

    [System.Serializable] class UnitDefCollection { public List<UnitDefStub> units; }
    [System.Serializable] class UnitDefStub       { public string id; }

    // ── JSON helpers ──────────────────────────────────────────────────

    static LevelData MakeBlank(int n) => new LevelData { id = $"level_{n}", displayName = $"Level {n}" };
    static float Round(float v) => Mathf.Round(v * 100f) / 100f;

    GameObject FindRoot()            => GameObject.Find(RootName);
    Transform  FindZonesRoot(GameObject root) => root.transform.Find(ZonesName);

    void ClearRoot()
    {
        var root = FindRoot();
        if (root != null) Undo.DestroyObjectImmediate(root);
        _activeLevel = 0;
        _zoneFoldouts.Clear();
        _pathFoldouts.Clear();
        _waveFoldouts.Clear();
        _waveData = null;
        SessionState.SetInt(SessionKey, 0);
        Repaint();
    }
}
