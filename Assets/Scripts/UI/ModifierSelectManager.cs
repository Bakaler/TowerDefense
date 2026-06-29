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

    ModifierDef[] _picks;   // one slot per column, null = nothing chosen yet

    void Start()
    {
        var data = LoadLevelData();
        if (data == null || data.modifierColumns == null || data.modifierColumns.Length == 0)
        {
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        _picks = new ModifierDef[data.modifierColumns.Length];
        BuildUI(data.modifierColumns);
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

        for (int ci = 0; ci < columns.Length; ci++)
        {
            int colIdx = ci;
            var col    = columns[ci];
            float x    = startX + ci * (COL_W + COL_GAP);

            var colGO  = MakeRect($"Col{ci}", canvasGO, x, 0f, COL_W, COL_H);
            colGO.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.15f, 1f);

            float slotH  = CARD_H + CARD_GAP;
            float startY = (col.options.Length - 1) * slotH * 0.5f;

            _cardImages[ci] = new Image[col.options.Length];

            for (int oi = 0; oi < col.options.Length; oi++)
            {
                int       optIdx = oi;
                ModifierDef opt  = col.options[oi];
                float     y      = startY - oi * slotH;

                var cardGO  = MakeRect($"Opt{oi}", colGO, 0, y, COL_W - 20f, CARD_H);
                var cardImg = cardGO.AddComponent<Image>();
                cardImg.color = COL_DEFAULT;
                _cardImages[ci][oi] = cardImg;

                var btn = cardGO.AddComponent<Button>();
                btn.targetGraphic = cardImg;
                var btnCols = btn.colors;
                btnCols.highlightedColor = new Color(0.22f, 0.28f, 0.42f, 1f);
                btnCols.pressedColor     = new Color(0.08f, 0.10f, 0.16f, 1f);
                btnCols.normalColor      = Color.white;  // tint multiplied with image color
                btn.colors = btnCols;

                // Name
                AddText(MakeRect("Name", cardGO, 0, 18f, COL_W - 30f, 30f),
                    opt.displayName, Color.white, 18, bold: true);

                // Description
                AddText(MakeRect("Desc", cardGO, 0, -12f, COL_W - 30f, 40f),
                    opt.description, new Color(0.75f, 0.80f, 0.90f, 1f), 13, bold: false);

                btn.onClick.AddListener(() => OnPick(colIdx, optIdx, columns[colIdx].options[optIdx]));
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
        backBtn.onClick.AddListener(() => SceneManager.LoadScene("LevelSelectionScene"));
    }

    // All card images grouped by column for highlight management
    Image[][] _cardImages;

    static readonly Color COL_DEFAULT  = new Color(0.14f, 0.17f, 0.26f, 1f);
    static readonly Color COL_SELECTED = new Color(0.15f, 0.50f, 0.25f, 1f);

    void OnPick(int colIdx, int optIdx, ModifierDef mod)
    {
        _picks[colIdx] = mod;

        // Reset all cards in column, highlight chosen one
        for (int i = 0; i < _cardImages[colIdx].Length; i++)
            _cardImages[colIdx][i].color = i == optIdx ? COL_SELECTED : COL_DEFAULT;
    }

    void OnConfirm()
    {
        ModifierSelection.Clear();
        foreach (var pick in _picks)
            if (pick != null) ModifierSelection.Add(pick);

        SceneManager.LoadScene(gameSceneName);
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
