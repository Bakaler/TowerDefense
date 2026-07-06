using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelSelectManager : MonoBehaviour
{
    public string gameSceneName           = "GameScene";
    public string modifierSelectSceneName = "ModifierSelectScene";

    // Card layout
    const float CARD_W    = 340f;
    const float CARD_H    = 340f;
    const float IMG_H     = 180f;
    const float LABEL_H   =  40f;
    const float DIFF_H    =  90f;
    const float STAR_H    =  30f;
    const float CARD_GAP  =  40f;

    static readonly Color C_Locked   = new Color(0.08f, 0.08f, 0.12f, 1f);
    static readonly Color C_Unlocked = new Color(0.10f, 0.12f, 0.18f, 1f);
    static readonly Color C_Easy     = new Color(0.15f, 0.55f, 0.25f, 1f);
    static readonly Color C_Medium   = new Color(0.65f, 0.45f, 0.10f, 1f);
    static readonly Color C_Hard     = new Color(0.55f, 0.12f, 0.12f, 1f);
    static readonly Color C_Star     = new Color(1.00f, 0.85f, 0.20f, 1f);
    static readonly Color C_StarOff  = new Color(0.30f, 0.30f, 0.35f, 1f);

    GameObject _canvas;

    void Start() => BuildUI();

    void BuildUI()
    {
        if (_canvas != null) Destroy(_canvas);
        BuildUIInternal();
    }

    void Rebuild()
    {
        if (_canvas != null) Destroy(_canvas);
        BuildUIInternal();
    }

    void BuildUIInternal()
    {
        var canvasGO               = new GameObject("LevelSelectCanvas");
        _canvas = canvasGO;
        var canvas                 = canvasGO.AddComponent<Canvas>();
        canvas.renderMode          = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder        = 10;
        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var bg = MakeRect("Background", canvasGO, 0, 0, 0, 0, fullStretch: true);
        bg.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.10f, 1f);

        AddText(MakeRect("Title", canvasGO, 0, 380f, 900f, 80f),
            "SELECT LEVEL", new Color(0.95f, 0.88f, 0.55f, 1f), 64, bold: true);

        var levels = LoadLevelData();
        float totalW = levels.Length * CARD_W + (levels.Length - 1) * CARD_GAP;
        float startX = -totalW * 0.5f + CARD_W * 0.5f;

        for (int i = 0; i < levels.Length; i++)
        {
            int       levelNum  = i + 1;
            LevelData data      = levels[i];
            float     x         = startX + i * (CARD_W + CARD_GAP);
            bool      unlocked  = SaveManager.IsLevelUnlocked(levelNum);
            int       stars     = SaveManager.GetStars(levelNum);

            // Card
            var card    = MakeRect($"Level{levelNum}Card", canvasGO, x, 30f, CARD_W, CARD_H);
            var cardImg = card.AddComponent<Image>();
            cardImg.color = unlocked ? C_Unlocked : C_Locked;

            // Map image
            float imgY = (CARD_H * 0.5f) - IMG_H * 0.5f - 5f;
            var imgGO  = MakeRect("MapImage", card, 0, imgY, CARD_W, IMG_H);
            var imgComp = imgGO.AddComponent<Image>();
            imgComp.preserveAspect = true;
            imgComp.raycastTarget  = false;
            if (!string.IsNullOrEmpty(data.backgroundSprite))
            {
                var sprite = LoadFirstSprite(data.backgroundSprite);
                if (sprite != null) imgComp.sprite = sprite;
                else imgComp.color = new Color(0.15f, 0.20f, 0.30f, 1f);
            }
            else imgComp.color = new Color(0.15f, 0.20f, 0.30f, 1f);

            // Lock overlay
            if (!unlocked)
            {
                var lockGO = MakeRect("Lock", card, 0, imgY, CARD_W, IMG_H);
                lockGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
                AddText(MakeRect("LockTxt", lockGO, 0, 0, CARD_W, IMG_H),
                    "LOCKED", new Color(0.6f, 0.6f, 0.6f, 1f), 32, bold: true, stretchToParent: true);
            }

            // Level name
            float labelY = imgY - IMG_H * 0.5f - LABEL_H * 0.5f;
            var labelGO = MakeRect("LabelBg", card, 0, labelY, CARD_W, LABEL_H);
            labelGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            AddText(MakeRect("LabelText", labelGO, 0, 0, CARD_W, LABEL_H),
                data.displayName, Color.white, 22, bold: true, stretchToParent: true);

            // Stars row
            float starY = labelY - LABEL_H * 0.5f - STAR_H * 0.5f - 4f;
            float starSpacing = 40f;
            for (int s = 0; s < 3; s++)
            {
                float sx = (s - 1) * starSpacing;
                var starGO = MakeRect($"Star{s}", card, sx, starY, 32f, 28f);
                AddText(starGO, "★", s < stars ? C_Star : C_StarOff, 24, bold: false);
            }

            // Difficulty buttons (only if unlocked and difficulties defined)
            if (unlocked && data.difficulties != null && data.difficulties.Length > 0)
            {
                float diffY     = -(CARD_H * 0.5f) + DIFF_H * 0.5f + 6f;
                float btnW      = (CARD_W - 16f) / data.difficulties.Length;
                Color[] colors  = { C_Easy, C_Medium, C_Hard };

                for (int d = 0; d < data.difficulties.Length; d++)
                {
                    int        diffIdx = d;
                    DifficultyDef diff = data.difficulties[d];
                    Color      col     = d < colors.Length ? colors[d] : C_Hard;
                    float      bx      = -CARD_W * 0.5f + 8f + btnW * d + btnW * 0.5f;

                    var btnGO  = MakeRect($"Diff{d}", card, bx, diffY, btnW - 4f, DIFF_H - 8f);
                    var btnImg = btnGO.AddComponent<Image>();
                    btnImg.color = col;
                    var btn    = btnGO.AddComponent<Button>();
                    btn.targetGraphic = btnImg;
                    var bCols  = btn.colors;
                    bCols.highlightedColor = col + new Color(0.12f, 0.12f, 0.12f, 0f);
                    bCols.pressedColor     = col - new Color(0.10f, 0.10f, 0.10f, 0f);
                    btn.colors = bCols;
                    AddText(MakeRect("Label", btnGO, 0, 0, btnW - 4f, DIFF_H - 8f),
                        diff.label, Color.white, 16, bold: true, stretchToParent: true);

                    int capturedLevel = levelNum;
                    btn.onClick.AddListener(() =>
                    {
                        LevelSelection.SelectedLevel      = capturedLevel;
                        LevelSelection.SelectedDifficulty = diffIdx;
                        LevelSelection.EnemyHpMult    = diff.enemyHpMult;
                        LevelSelection.EnemySpeedMult = diff.enemySpeedMult;
                        LevelSelection.GoldMult       = diff.goldMult;
                        LevelSelection.BountyMult     = diff.bountyMult;
                        bool hasModifiers = HasModifiers(data);
                        ModifierSelection.Clear();
                        SceneManager.LoadScene(hasModifiers ? modifierSelectSceneName : gameSceneName);
                    });
                }
            }
            else if (unlocked)
            {
                // No difficulties defined — just click the card to play
                var btn = card.AddComponent<Button>();
                btn.targetGraphic = cardImg;
                var cols = btn.colors;
                cols.highlightedColor = C_Unlocked + new Color(0.08f, 0.08f, 0.08f, 0f);
                cols.pressedColor     = C_Unlocked - new Color(0.05f, 0.05f, 0.05f, 0f);
                btn.colors = cols;
                int capturedLevel = levelNum;
                btn.onClick.AddListener(() =>
                {
                    LevelSelection.SelectedLevel      = capturedLevel;
                    LevelSelection.SelectedDifficulty = 0;
                    LevelSelection.EnemyHpMult    = 1f;
                    LevelSelection.EnemySpeedMult = 1f;
                    LevelSelection.GoldMult       = 1f;
                    LevelSelection.BountyMult     = 1f;
                    bool hasModifiers = data.modifierColumns != null && data.modifierColumns.Length > 0;
                    ModifierSelection.Clear();
                    SceneManager.LoadScene(hasModifiers ? modifierSelectSceneName : gameSceneName);
                });
            }
        }

        // Cheat — unlock all levels
        var cheatGO  = MakeRect("CheatBtn", canvasGO, -840f, -480f, 160f, 36f);
        var cheatImg = cheatGO.AddComponent<Image>(); cheatImg.color = new Color(0.18f, 0.18f, 0.22f, 0.7f);
        var cheatBtn = cheatGO.AddComponent<Button>(); cheatBtn.targetGraphic = cheatImg;
        AddText(MakeRect("Label", cheatGO, 0, 0, 160f, 36f), "unlock all", new Color(0.5f, 0.5f, 0.6f, 1f), 14, bold: false, stretchToParent: true);
        cheatBtn.onClick.AddListener(() => { SaveManager.UnlockAllLevels(); Rebuild(); });

        // Back button
        var backGO  = MakeRect("BackBtn", canvasGO, 0, -390f, 180f, 48f);
        var backImg = backGO.AddComponent<Image>(); backImg.color = new Color(0.28f, 0.12f, 0.12f, 1f);
        var backBtn = backGO.AddComponent<Button>(); backBtn.targetGraphic = backImg;
        AddText(MakeRect("Label", backGO, 0, 0, 180f, 48f), "BACK", Color.white, 22, bold: true, stretchToParent: true);
        backBtn.onClick.AddListener(() => SceneManager.LoadScene("MainMenuScene"));
    }

    LevelData[] LoadLevelData()
    {
        var list = new System.Collections.Generic.List<LevelData>();
        for (int i = 1; i <= 20; i++)
        {
            var asset = Resources.Load<TextAsset>($"Definitions/Levels/level_{i}");
            if (asset == null) break;
            var data = JsonUtility.FromJson<LevelData>(asset.text);
            if (data != null) list.Add(data);
        }
        return list.ToArray();
    }

    static Sprite LoadFirstSprite(string path)
    {
        var sprites = Resources.LoadAll<Sprite>(path);
        if (sprites != null && sprites.Length > 0) return sprites[0];
        return Resources.Load<Sprite>(path);
    }

    static bool HasModifiers(LevelData data)
    {
        if (data?.modifierColumns != null && data.modifierColumns.Length > 0) return true;
        return Resources.Load<TextAsset>("Definitions/modifier_columns") != null;
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
        return txt;
    }
}
