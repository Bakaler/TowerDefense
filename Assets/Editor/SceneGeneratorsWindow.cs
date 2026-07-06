using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine.Rendering.Universal;

/// <summary>
/// TowerDefense → Scene Generators
/// Tab 0: Wave Scene  (formerly TowerDefense/Setup Basic Wave Scene)
/// Tab 1: Landing Scene
/// </summary>
public class SceneGeneratorsWindow : EditorWindow
{
    static readonly string[] _tabs = { "Wave Scene", "Landing Scene", "Main Menu Scene", "Level Select Scene", "Modifier Select Scene" };
    int    _tab;
    string _mainMenuSceneName      = "MainMenuScene";
    string _landingSceneName       = "LandingScene";
    string _levelSelectSceneName   = "LevelSelectionScene";
    string _modifierSelectSceneName = "ModifierSelectScene";
    string _gameSceneName          = "GameScene";

    [MenuItem("TowerDefense/Scene Generators")]
    public static void Open() => GetWindow<SceneGeneratorsWindow>("Scene Generators");

    void OnGUI()
    {
        _tab = GUILayout.Toolbar(_tab, _tabs);
        GUILayout.Space(10);

        if      (_tab == 0) DrawWaveScene();
        else if (_tab == 1) DrawLandingScene();
        else if (_tab == 2) DrawMainMenuScene();
        else if (_tab == 3) DrawLevelSelectScene();
        else                DrawModifierSelectScene();
    }

    // ── Tab 0: Wave Scene ─────────────────────────────────────────────

    void DrawWaveScene()
    {
        EditorGUILayout.HelpBox(
            "Creates a blank-canvas scene with all manager objects.\n" +
            "Press Play → Level 1 loads automatically.\n" +
            "Press 1 / 2 / 3 in Play mode to switch levels.",
            MessageType.Info);

        GUILayout.Space(8);

        if (GUILayout.Button("Generate Wave Scene", GUILayout.Height(36)))
            SceneBootstrap.SetupScene();
    }

    // ── Tab 1: Landing Scene ──────────────────────────────────────────

    void DrawLandingScene()
    {
        EditorGUILayout.HelpBox(
            "Generates the landing scene: title + runtime-built profile picker.\n" +
            "Selecting a profile loads the main menu scene.",
            MessageType.Info);

        GUILayout.Space(8);
        _mainMenuSceneName = EditorGUILayout.TextField("Main Menu Scene Name", _mainMenuSceneName);
        GUILayout.Space(8);

        if (GUILayout.Button("Generate Landing Scene", GUILayout.Height(36)))
            GenerateLandingScene();
    }

    void GenerateLandingScene()
    {
        if (!EditorUtility.DisplayDialog(
            "Generate Landing Scene",
            $"This will reset all objects in the active scene and build a landing screen.\n\nMain menu scene: \"{_mainMenuSceneName}\"\n\nProceed?",
            "Yes", "Cancel"))
            return;

        Undo.SetCurrentGroupName("Generate Landing Scene");
        int undoGroup = Undo.GetCurrentGroup();

        // ── Clear existing objects ────────────────────────────────────
        foreach (string n in new[]
        {
            "--- GLOBAL LIGHT ---", "--- LANDING UI ---", "[LandingManager]",
            "[MainMenuController]", "[Libraries]",   // in case a menu scene was the starting point
        })
        {
            var existing = GameObject.Find(n);
            if (existing != null) Undo.DestroyObjectImmediate(existing);
        }

        // ── Global light ──────────────────────────────────────────────
        var lightRoot = new GameObject("--- GLOBAL LIGHT ---");
        Undo.RegisterCreatedObjectUndo(lightRoot, "Create Global Light");
        var light      = lightRoot.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;
        light.color     = Color.white;

        // ── Camera ────────────────────────────────────────────────────
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.orthographic       = true;
            cam.orthographicSize   = 7f;
            cam.backgroundColor    = new Color(0.05f, 0.05f, 0.10f, 1f);
        }

        // ── LandingManager ────────────────────────────────────────────
        var mgr    = new GameObject("[LandingManager]");
        Undo.RegisterCreatedObjectUndo(mgr, "Create LandingManager");
        var lsm               = mgr.AddComponent<LandingScreenManager>();
        lsm.mainMenuSceneName = _mainMenuSceneName;

        // ── Canvas ────────────────────────────────────────────────────
        var canvasGO = new GameObject("--- LANDING UI ---");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Landing Canvas");

        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler                 = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // ── EventSystem (required for UI button clicks) ───────────────
        foreach (var existing in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(existing.gameObject);
        var esGO = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // ── Background panel ──────────────────────────────────────────
        var bg   = MakeRect("Background", canvasGO, 0f, 0f, 0f, 0f);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        var bgImg      = bg.AddComponent<UnityEngine.UI.Image>();
        bgImg.color    = new Color(0.05f, 0.05f, 0.10f, 1f);
        bgImg.raycastTarget = false;    

        // ── Title ─────────────────────────────────────────────────────
        var title    = MakeRect("Title", canvasGO, 0f, 160f, 900f, 160f);
        var titleTxt = title.AddComponent<UnityEngine.UI.Text>();
        titleTxt.text      = "Zen TD";
        titleTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize  = 96;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color     = new Color(0.95f, 0.88f, 0.55f, 1f);

        // ── Subtitle ──────────────────────────────────────────────────
        var sub    = MakeRect("Subtitle", canvasGO, 0f, 60f, 600f, 50f);
        var subTxt = sub.AddComponent<UnityEngine.UI.Text>();
        subTxt.text      = "Balance";
        subTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subTxt.fontSize  = 28;
        subTxt.alignment = TextAnchor.MiddleCenter;
        subTxt.color     = new Color(0.75f, 0.75f, 0.85f, 1f);

        // Profile picker UI is built at runtime by ProfileSelectScreen
        // (added by LandingScreenManager on Start).

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EnsureSceneInBuildSettings();

        Debug.Log($"[SceneGenerators] Landing scene built. Profile select loads \"{_mainMenuSceneName}\".");
    }

    // ── Tab 2: Main Menu Scene ────────────────────────────────────────

    void DrawMainMenuScene()
    {
        EditorGUILayout.HelpBox(
            "Generates the main menu scene (Play / Settings / Achievements / Journal).\n" +
            "Run this in a blank scene saved as MainMenuScene.\n" +
            "All UI is built at runtime by MainMenuController.",
            MessageType.Info);

        GUILayout.Space(8);
        _levelSelectSceneName = EditorGUILayout.TextField("Level Select Scene Name", _levelSelectSceneName);
        _landingSceneName     = EditorGUILayout.TextField("Landing Scene Name", _landingSceneName);
        GUILayout.Space(8);

        if (GUILayout.Button("Generate Main Menu Scene", GUILayout.Height(36)))
            GenerateMainMenuScene();
    }

    void GenerateMainMenuScene()
    {
        if (!EditorUtility.DisplayDialog(
            "Generate Main Menu Scene",
            "This will reset the active scene and build the main menu.\nProceed?",
            "Yes", "Cancel"))
            return;

        Undo.SetCurrentGroupName("Generate Main Menu Scene");
        int undoGroup = Undo.GetCurrentGroup();

        // Also clears landing-scene objects — a common starting point is
        // duplicating LandingScene, which must not keep its profile picker.
        foreach (string n in new[]
        {
            "--- GLOBAL LIGHT ---", "[MainMenuController]", "[Libraries]", "EventSystem",
            "[LandingManager]", "--- LANDING UI ---",
        })
        {
            var e = GameObject.Find(n);
            if (e != null) Undo.DestroyObjectImmediate(e);
        }

        // Global light
        var lightRoot = new GameObject("--- GLOBAL LIGHT ---");
        Undo.RegisterCreatedObjectUndo(lightRoot, "Create Global Light");
        var light      = lightRoot.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;

        // Camera
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.orthographic       = true;
            cam.orthographicSize   = 7f;
            cam.backgroundColor    = new Color(0.05f, 0.05f, 0.10f, 1f);
        }

        // MainMenuController — builds all UI at runtime
        var mgr = new GameObject("[MainMenuController]");
        Undo.RegisterCreatedObjectUndo(mgr, "Create MainMenuController");
        var mmc = mgr.AddComponent<MainMenuController>();
        mmc.levelSelectSceneName = _levelSelectSceneName;
        mmc.landingSceneName     = _landingSceneName;

        // Definition libraries — the Journal reads towers/units from these;
        // ability/effect libraries feed TowerStatResolver's damage/cooldown.
        var libs = new GameObject("[Libraries]");
        Undo.RegisterCreatedObjectUndo(libs, "Create Libraries");
        libs.AddComponent<TowerDefinitionLibrary>();
        libs.AddComponent<UnitDefinitionLibrary>();
        libs.AddComponent<AbilityLibrary>();
        libs.AddComponent<EffectLibrary>();

        // EventSystem
        foreach (var existing in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(existing.gameObject);
        var esGO = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EnsureSceneInBuildSettings();

        Debug.Log("[SceneGenerators] Main menu scene built.");
    }

    /// <summary>Adds the active (saved) scene to Build Settings so LoadScene-by-name works.</summary>
    static void EnsureSceneInBuildSettings()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            // Unsaved scene can't be registered — without this, LoadScene-by-name
            // fails at runtime and the menu buttons appear to do nothing.
            EditorUtility.DisplayDialog(
                "Scene not in Build Settings",
                "This scene has never been saved, so it was NOT added to Build Settings.\n\n" +
                "Save the scene (Ctrl+S), then either regenerate or add it via File → Build Settings → Add Open Scenes.",
                "OK");
            return;
        }

        foreach (var s in EditorBuildSettings.scenes)
            if (s.path == scene.path) return;

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
        {
            new EditorBuildSettingsScene(scene.path, true)
        };
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[SceneGenerators] Added \"{scene.path}\" to Build Settings.");
    }

    // ── Tab 2: Level Select Scene ─────────────────────────────────────

    void DrawLevelSelectScene()
    {
        EditorGUILayout.HelpBox(
            "Generates a level-select scene that reads level_N.json files at runtime.\n" +
            "Run this in a blank scene saved as LevelSelectScene.",
            MessageType.Info);

        GUILayout.Space(8);
        _gameSceneName = EditorGUILayout.TextField("Game Scene Name", _gameSceneName);
        GUILayout.Space(8);

        if (GUILayout.Button("Generate Level Select Scene", GUILayout.Height(36)))
            GenerateLevelSelectScene();
    }

    void GenerateLevelSelectScene()
    {
        if (!EditorUtility.DisplayDialog(
            "Generate Level Select Scene",
            "This will reset the active scene and build a level-select screen.\nProceed?",
            "Yes", "Cancel"))
            return;

        Undo.SetCurrentGroupName("Generate Level Select Scene");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (string n in new[] { "--- GLOBAL LIGHT ---", "[LevelSelectManager]", "EventSystem" })
        {
            var e = GameObject.Find(n);
            if (e != null) Undo.DestroyObjectImmediate(e);
        }

        // Global light
        var lightRoot = new GameObject("--- GLOBAL LIGHT ---");
        Undo.RegisterCreatedObjectUndo(lightRoot, "Create Global Light");
        var light      = lightRoot.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
        light.intensity = 1f;

        // Camera
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.orthographic       = true;
            cam.orthographicSize   = 7f;
            cam.backgroundColor    = new Color(0.05f, 0.05f, 0.10f, 1f);
        }

        // LevelSelectManager — builds all UI at runtime
        var mgr = new GameObject("[LevelSelectManager]");
        Undo.RegisterCreatedObjectUndo(mgr, "Create LevelSelectManager");
        var lsm = mgr.AddComponent<LevelSelectManager>();
        lsm.gameSceneName = _gameSceneName;

        // EventSystem
        foreach (var existing in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(existing.gameObject);
        var esGO = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[SceneGenerators] Level select scene built.");
    }

    // ── Tab 3: Modifier Select Scene ──────────────────────────────────

    void DrawModifierSelectScene()
    {
        EditorGUILayout.HelpBox(
            "Generates the modifier selection scene shown between level select and the game.\n" +
            "Run this in a blank scene saved as ModifierSelectScene.",
            MessageType.Info);

        GUILayout.Space(8);
        _gameSceneName = EditorGUILayout.TextField("Game Scene Name", _gameSceneName);
        GUILayout.Space(8);

        if (GUILayout.Button("Generate Modifier Select Scene", GUILayout.Height(36)))
            GenerateModifierSelectScene();
    }

    void GenerateModifierSelectScene()
    {
        if (!EditorUtility.DisplayDialog(
            "Generate Modifier Select Scene",
            "This will reset the active scene and build a modifier-select screen.\nProceed?",
            "Yes", "Cancel"))
            return;

        Undo.SetCurrentGroupName("Generate Modifier Select Scene");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (string n in new[] { "--- GLOBAL LIGHT ---", "[ModifierSelectManager]", "EventSystem" })
        {
            var e = GameObject.Find(n);
            if (e != null) Undo.DestroyObjectImmediate(e);
        }

        var lightRoot   = new GameObject("--- GLOBAL LIGHT ---");
        Undo.RegisterCreatedObjectUndo(lightRoot, "Create Global Light");
        var light       = lightRoot.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
        light.intensity = 1f;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.orthographic       = true;
            cam.orthographicSize   = 7f;
            cam.backgroundColor    = new Color(0.05f, 0.05f, 0.10f, 1f);
        }

        var mgr = new GameObject("[ModifierSelectManager]");
        Undo.RegisterCreatedObjectUndo(mgr, "Create ModifierSelectManager");
        var msm = mgr.AddComponent<ModifierSelectManager>();
        msm.gameSceneName = _gameSceneName;

        foreach (var existing in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(existing.gameObject);
        var esGO = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[SceneGenerators] Modifier select scene built.");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    static GameObject MakeRect(string name, GameObject parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform, false);
        var rt              = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);
        return go;
    }
}
