using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the modifier selection UI at runtime from the selected level's modifierColumns.
/// One column per group; player picks one option per column, then clicks Confirm.
/// </summary>
public class ModifierSelectManager : MonoBehaviour
{
    public string gameSceneName = "GameScene";

    // Layout
    const float COL_W      = 220f;
    const float COL_H      = 560f;
    const float COL_GAP    = 30f;
    const float CARD_H     = 90f;
    const float CARD_GAP   = 12f;

    ModifierDef[]  _picks;
    ModifierColumn[] _columns;
    ModifierDef[]  _levelMods;   // forced level-specific modifiers — always applied
    bool           _cheatUnlocked;

    // Type "STARS" to unlock all columns for this run
    static readonly KeyCode[] CheatSequence = { KeyCode.S, KeyCode.T, KeyCode.A, KeyCode.R, KeyCode.S };
    int _cheatProgress;

    void Start()
    {
        var data = LoadLevelData();
        var cols = data?.modifierColumns != null && data.modifierColumns.Length > 0
            ? data.modifierColumns
            : LoadSharedColumns();

        _levelMods = data?.levelModifiers ?? System.Array.Empty<ModifierDef>();

        if (cols == null || cols.Length == 0)
        {
            // No choices to make — still hand out the forced level modifiers
            ModifierSelection.Clear();
            foreach (var mod in _levelMods) ModifierSelection.Add(mod);
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        _columns = cols;
        _picks   = new ModifierDef[_columns.Length];
        BuildUI(_columns);
    }

    static ModifierColumn[] LoadSharedColumns()
    {
        var ta = Resources.Load<TextAsset>("Definitions/modifier_columns");
        if (ta == null) return null;
        var wrapper = JsonUtility.FromJson<SharedColumnsWrapper>(ta.text);
        return wrapper?.modifierColumns;
    }

    [System.Serializable]
    class SharedColumnsWrapper { public ModifierColumn[] modifierColumns; }

    void Update()
    {
        if (_cheatUnlocked) return;
        foreach (KeyCode kc in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (!Input.GetKeyDown(kc)) continue;
            if (kc == CheatSequence[_cheatProgress])
            {
                _cheatProgress++;
                if (_cheatProgress >= CheatSequence.Length)
                {
                    _cheatUnlocked = true;
                    _cheatProgress = 0;
                    RebuildWithCheat();
                }
            }
            else
            {
                _cheatProgress = kc == CheatSequence[0] ? 1 : 0;
            }
            break;
        }
    }

    void RebuildWithCheat()
    {
        // Destroy existing canvas and rebuild with all columns unlocked
        var existing = GameObject.Find("ModifierCanvas");
        if (existing != null) Destroy(existing);
        _picks = new ModifierDef[_columns.Length];
        _cheatUnlocked = true;
        BuildUI(_columns);
    }

    void BuildUI(ModifierColumn[] columns)
    {
        var canvasGO               = new GameObject("ModifierCanvas");
        var canvas                 = canvasGO.AddComponent<Canvas>();
        canvas.renderMode          = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder        = 10;
        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Dark background
        var bg = MakeRect("BG", canvasGO, 0, 0, 0, 0, fullStretch: true);
        bg.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.10f, 1f);

        // Title
        AddText(MakeRect("Title", canvasGO, 0, 440f, 900f, 70f),
            "CHOOSE YOUR MODIFIERS", new Color(0.95f, 0.88f, 0.55f, 1f), 52, bold: true);

        // Columns
        float totalW = columns.Length * COL_W + (columns.Length - 1) * COL_GAP;
        float startX = -totalW * 0.5f + COL_W * 0.5f;

        _cardImages = new Image[columns.Length][];
        _passImages = new Image[columns.Length];

        int totalStars   = SaveManager.TotalStarsAllLevels();
        int[] thresholds = StarManager.ColumnThresholds;

        for (int ci = 0; ci < columns.Length; ci++)
        {
            int colIdx    = ci;
            var col       = columns[ci];
            float x       = startX + ci * (COL_W + COL_GAP);
            int threshold = ci < thresholds.Length ? thresholds[ci] : int.MaxValue;
            bool unlocked = _cheatUnlocked || totalStars >= threshold;

            var colGO  = MakeRect($"Col{ci}", canvasGO, x, 0f, COL_W, COL_H);
            colGO.AddComponent<Image>().color = unlocked
                ? new Color(0.08f, 0.10f, 0.15f, 1f)
                : new Color(0.06f, 0.06f, 0.08f, 1f);

            if (!unlocked)
            {
                // Lock overlay
                var lockGO  = MakeRect("Lock", colGO, 0, 0, COL_W, COL_H);
                lockGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
                AddText(MakeRect("LockLbl", colGO, 0, 0, COL_W - 20f, 80f),
                    $"🔒\n{totalStars}/{threshold}★\nto unlock",
                    new Color(0.65f, 0.65f, 0.75f, 1f), 16, bold: true, stretchToParent: false);
                _cardImages[ci] = new Image[col.options.Length];
                _picks[ci] = null;
                continue;
            }

            float slotH      = CARD_H + CARD_GAP;
            int   totalCards = col.options.Length + 1; // +1 for Pass
            float startY     = (totalCards - 1) * slotH * 0.5f;

            // Pass card (default selected)
            var passGO  = MakeRect("Pass", colGO, 0, startY, COL_W - 20f, CARD_H);
            var passImg = passGO.AddComponent<Image>();
            passImg.color      = COL_PASS_SEL;
            _passImages[ci]    = passImg;
            _picks[ci]         = null;

            var passBtn = passGO.AddComponent<Button>();
            passBtn.targetGraphic = passImg;
            var passBtnCols = passBtn.colors;
            passBtnCols.highlightedColor = COL_PASS_SEL + new Color(0.05f, 0.05f, 0.05f, 0f);
            passBtnCols.pressedColor     = COL_PASS;
            passBtnCols.normalColor      = Color.white;
            passBtn.colors = passBtnCols;
            AddText(MakeRect("Label", passGO, 0, 0, COL_W - 30f, CARD_H),
                "PASS", new Color(0.70f, 0.85f, 0.80f, 1f), 20, bold: true, stretchToParent: true);
            passBtn.onClick.AddListener(() => OnPass(colIdx));

            _cardImages[ci] = new Image[col.options.Length];

            for (int oi = 0; oi < col.options.Length; oi++)
            {
                int       optIdx = oi;
                ModifierDef opt  = col.options[oi];
                float     y      = startY - (oi + 1) * slotH;

                var cardGO  = MakeRect($"Opt{oi}", colGO, 0, y, COL_W - 20f, CARD_H);
                var cardImg = cardGO.AddComponent<Image>();
                cardImg.color = COL_DEFAULT;
                _cardImages[ci][oi] = cardImg;

                var btn = cardGO.AddComponent<Button>();
                btn.targetGraphic = cardImg;
                var btnCols = btn.colors;
                btnCols.highlightedColor = new Color(0.22f, 0.28f, 0.42f, 1f);
                btnCols.pressedColor     = new Color(0.08f, 0.10f, 0.16f, 1f);
                btnCols.normalColor      = Color.white;
                btn.colors = btnCols;

                AddText(MakeRect("Name", cardGO, 0, 16f, COL_W - 30f, 44f),
                    opt.displayName, Color.white, 15, bold: true);
                AddText(MakeRect("Desc", cardGO, 0, -18f, COL_W - 30f, 38f),
                    opt.description, new Color(0.75f, 0.80f, 0.90f, 1f), 12, bold: false);

                btn.onClick.AddListener(() => OnPick(colIdx, optIdx, columns[colIdx].options[optIdx]));
            }
        }

        // Level modifiers — forced picks pinned under the columns, above Confirm.
        // No Button component: always selected, cannot be deselected.
        if (_levelMods != null && _levelMods.Length > 0)
        {
            AddText(MakeRect("LevelModHeader", canvasGO, 0, -308f, 700f, 26f),
                "— LEVEL MODIFIER —", new Color(0.95f, 0.88f, 0.55f, 1f), 18, bold: true);

            const float LM_W = 480f, LM_H = 72f, LM_GAP = 24f;
            float lmTotalW = _levelMods.Length * LM_W + (_levelMods.Length - 1) * LM_GAP;
            float lmStartX = -lmTotalW * 0.5f + LM_W * 0.5f;

            for (int i = 0; i < _levelMods.Length; i++)
            {
                var mod    = _levelMods[i];
                var cardGO = MakeRect($"LevelMod{i}", canvasGO, lmStartX + i * (LM_W + LM_GAP), -355f, LM_W, LM_H);
                cardGO.AddComponent<Image>().color = COL_SELECTED;

                AddText(MakeRect("Name", cardGO, 0, 14f, LM_W - 24f, 34f),
                    $"★ {mod.displayName}", new Color(1f, 0.95f, 0.75f, 1f), 17, bold: true);
                AddText(MakeRect("Desc", cardGO, 0, -16f, LM_W - 24f, 30f),
                    mod.description, new Color(0.80f, 0.90f, 0.82f, 1f), 12, bold: false);
            }
        }

        // Confirm button
        var confirmGO  = MakeRect("ConfirmBtn", canvasGO, 0, -440f, 260f, 60f);
        var confirmImg = confirmGO.AddComponent<Image>();
        confirmImg.color = new Color(0.15f, 0.45f, 0.20f, 1f);
        var confirmBtn = confirmGO.AddComponent<Button>();
        confirmBtn.targetGraphic = confirmImg;
        AddText(MakeRect("Label", confirmGO, 0, 0, 260f, 60f), "CONFIRM", Color.white, 28, bold: true, stretchToParent: true);
        confirmBtn.onClick.AddListener(OnConfirm);

        // Back button
        var backGO  = MakeRect("BackBtn", canvasGO, -700f, -440f, 160f, 48f);
        var backImg = backGO.AddComponent<Image>(); backImg.color = new Color(0.28f, 0.12f, 0.12f, 1f);
        var backBtn = backGO.AddComponent<Button>(); backBtn.targetGraphic = backImg;
        AddText(MakeRect("Label", backGO, 0, 0, 160f, 48f), "BACK", Color.white, 20, bold: true, stretchToParent: true);
        backBtn.onClick.AddListener(() => ScreenFader.LoadScene("LevelSelectionScene"));
    }

    // All card images grouped by column for highlight management
    Image[][] _cardImages;
    Image[]   _passImages;

    static readonly Color COL_DEFAULT  = new Color(0.14f, 0.17f, 0.26f, 1f);
    static readonly Color COL_SELECTED = new Color(0.15f, 0.50f, 0.25f, 1f);
    static readonly Color COL_PASS     = new Color(0.22f, 0.22f, 0.30f, 1f);
    static readonly Color COL_PASS_SEL = new Color(0.18f, 0.45f, 0.42f, 1f);

    void OnPick(int colIdx, int optIdx, ModifierDef mod)
    {
        _picks[colIdx] = mod;

        if (_passImages != null && _passImages[colIdx] != null)
            _passImages[colIdx].color = COL_PASS;

        for (int i = 0; i < _cardImages[colIdx].Length; i++)
            _cardImages[colIdx][i].color = i == optIdx ? COL_SELECTED : COL_DEFAULT;
    }

    void OnPass(int colIdx)
    {
        _picks[colIdx] = null;

        if (_passImages != null && _passImages[colIdx] != null)
            _passImages[colIdx].color = COL_PASS_SEL;

        for (int i = 0; i < _cardImages[colIdx].Length; i++)
            _cardImages[colIdx][i].color = COL_DEFAULT;
    }

    void OnConfirm()
    {
        ModifierSelection.Clear();
        foreach (var pick in _picks)
            if (pick != null) ModifierSelection.Add(pick);

        // Forced level modifiers always ride along
        if (_levelMods != null)
            foreach (var mod in _levelMods) ModifierSelection.Add(mod);

        ScreenFader.LoadScene(gameSceneName);
    }

    LevelData LoadLevelData()
    {
        var ta = Resources.Load<TextAsset>($"Definitions/Levels/level_{LevelSelection.SelectedLevel}");
        if (ta == null) return null;
        return JsonUtility.FromJson<LevelData>(ta.text);
    }

    static GameObject MakeRect(string name, GameObject parent, float x, float y, float w, float h, bool fullStretch = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        if (fullStretch)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta        = new Vector2(w, h);
        }
        return go;
    }

    static Text AddText(GameObject go, string content, Color color, int size, bool bold, bool stretchToParent = false)
    {
        if (stretchToParent)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
        }
        var txt       = go.AddComponent<Text>();
        txt.text      = content;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = size;
        txt.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = color;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        return txt;
    }
}
