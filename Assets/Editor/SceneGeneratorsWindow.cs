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
    static readonly string[] _tabs = { "Wave Scene", "Landing Scene" };
    int    _tab;
    string _gameSceneName = "GameScene";

    [MenuItem("TowerDefense/Scene Generators")]
    public static void Open() => GetWindow<SceneGeneratorsWindow>("Scene Generators");

    void OnGUI()
    {
        _tab = GUILayout.Toolbar(_tab, _tabs);
        GUILayout.Space(10);

        if (_tab == 0) DrawWaveScene();
        else           DrawLandingScene();
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
            "Generates a standalone landing / main-menu scene.\n" +
            "Set the game scene name below, then click Generate.",
            MessageType.Info);

        GUILayout.Space(8);
        _gameSceneName = EditorGUILayout.TextField("Game Scene Name", _gameSceneName);
        GUILayout.Space(8);

        if (GUILayout.Button("Generate Landing Scene", GUILayout.Height(36)))
            GenerateLandingScene();
    }

    void GenerateLandingScene()
    {
        if (!EditorUtility.DisplayDialog(
            "Generate Landing Scene",
            $"This will reset all objects in the active scene and build a landing screen.\n\nGame scene: \"{_gameSceneName}\"\n\nProceed?",
            "Yes", "Cancel"))
            return;

        Undo.SetCurrentGroupName("Generate Landing Scene");
        int undoGroup = Undo.GetCurrentGroup();

        // ── Clear existing objects ────────────────────────────────────
        foreach (string n in new[]
        {
            "--- GLOBAL LIGHT ---", "--- LANDING UI ---", "[LandingManager]"
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
        var lsm              = mgr.AddComponent<LandingScreenManager>();
        lsm.gameSceneName    = _gameSceneName;

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
        titleTxt.text      = "Another Tower Defense Game";
        titleTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize  = 96;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color     = new Color(0.95f, 0.88f, 0.55f, 1f);

        // ── Subtitle ──────────────────────────────────────────────────
        var sub    = MakeRect("Subtitle", canvasGO, 0f, 60f, 600f, 50f);
        var subTxt = sub.AddComponent<UnityEngine.UI.Text>();
        subTxt.text      = "Battle Between Two";
        subTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subTxt.fontSize  = 28;
        subTxt.alignment = TextAnchor.MiddleCenter;
        subTxt.color     = new Color(0.75f, 0.75f, 0.85f, 1f);

        // ── Play button ───────────────────────────────────────────────
        var btnGO   = MakeRect("PlayButton", canvasGO, 0f, -80f, 280f, 70f);
        var btnImg  = btnGO.AddComponent<UnityEngine.UI.Image>();
        btnImg.color = new Color(0.18f, 0.55f, 0.22f, 1f);
        var btn     = btnGO.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = btnImg;
        var cols    = btn.colors;
        cols.highlightedColor = new Color(0.25f, 0.72f, 0.30f, 1f);
        cols.pressedColor     = new Color(0.12f, 0.38f, 0.15f, 1f);
        btn.colors  = cols;
        UnityEventTools.AddPersistentListener(btn.onClick, lsm.OnPlayPressed);

        var btnLbl    = MakeRect("Label", btnGO, 0f, 0f, 0f, 0f);
        var btnLblRT  = btnLbl.GetComponent<RectTransform>();
        btnLblRT.anchorMin = Vector2.zero;
        btnLblRT.anchorMax = Vector2.one;
        btnLblRT.offsetMin = Vector2.zero;
        btnLblRT.offsetMax = Vector2.zero;
        var btnTxt    = btnLbl.AddComponent<UnityEngine.UI.Text>();
        btnTxt.text      = "PLAY";
        btnTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnTxt.fontSize  = 42;
        btnTxt.fontStyle = FontStyle.Bold;
        btnTxt.alignment = TextAnchor.MiddleCenter;
        btnTxt.color     = Color.white;

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[SceneGenerators] Landing scene built. Play button loads \"{_gameSceneName}\".");
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
