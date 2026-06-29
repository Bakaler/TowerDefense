using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelSelectManager : MonoBehaviour
{
    public string gameSceneName           = "GameScene";
    public string modifierSelectSceneName = "ModifierSelectScene";

    // Card layout
    const float CARD_W   = 340f;
    const float CARD_H   = 280f;
    const float IMG_H    = 220f;
    const float LABEL_H  =  50f;
    const float CARD_GAP =  40f;

    void Start() => BuildUI();

    void BuildUI()
    {
        var canvasGO               = new GameObject("LevelSelectCanvas");
        var canvas                 = canvasGO.AddComponent<Canvas>();
        canvas.renderMode          = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder        = 10;
        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Dark background
        var bg = MakeRect("Background", canvasGO, 0, 0, 0, 0, fullStretch: true);
        bg.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.10f, 1f);

        // Title
        AddText(MakeRect("Title", canvasGO, 0, 350f, 900f, 80f),
            "SELECT LEVEL", new Color(0.95f, 0.88f, 0.55f, 1f), 64, bold: true);

        var levels = LoadLevelData();

        float totalW = levels.Length * CARD_W + (levels.Length - 1) * CARD_GAP;
        float startX = -totalW * 0.5f + CARD_W * 0.5f;

        for (int i = 0; i < levels.Length; i++)
        {
            int      levelNum = i + 1;
            LevelData data    = levels[i];
            float    x        = startX + i * (CARD_W + CARD_GAP);

            // Card container — invisible, just a Button hit area
            var card    = MakeRect($"Level{levelNum}Card", canvasGO, x, 30f, CARD_W, CARD_H);
            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.10f, 0.12f, 0.18f, 1f);
            var btn     = card.AddComponent<Button>();
            btn.targetGraphic = cardImg;
            var cols    = btn.colors;
            cols.highlightedColor = new Color(0.18f, 0.22f, 0.32f, 1f);
            cols.pressedColor     = new Color(0.05f, 0.06f, 0.10f, 1f);
            btn.colors = cols;

            // Map image (top portion of card)
            var imgGO  = MakeRect("MapImage", card, 0, (CARD_H - IMG_H) * 0.5f, CARD_W, IMG_H);
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

            // Title label (bottom strip) — bg and text on separate GOs (share CanvasRenderer limit)
            var labelGO = MakeRect("LabelBg", card, 0, -(CARD_H * 0.5f - LABEL_H * 0.5f), CARD_W, LABEL_H);
            var labelBg = labelGO.AddComponent<Image>();
            labelBg.color         = new Color(0f, 0f, 0f, 0.55f);
            labelBg.raycastTarget = false;
            AddText(MakeRect("LabelText", labelGO, 0, 0, CARD_W, LABEL_H),
                data.displayName, Color.white, 26, bold: true, stretchToParent: true);

            btn.onClick.AddListener(() =>
            {
                LevelSelection.SelectedLevel = levelNum;
                bool hasModifiers = data.modifierColumns != null && data.modifierColumns.Length > 0;
                ModifierSelection.Clear();
                SceneManager.LoadScene(hasModifiers ? modifierSelectSceneName : gameSceneName);
            });
        }

        // Back button
        var backGO  = MakeRect("BackBtn", canvasGO, 0, -340f, 180f, 48f);
        var backImg = backGO.AddComponent<Image>(); backImg.color = new Color(0.28f, 0.12f, 0.12f, 1f);
        var backBtn = backGO.AddComponent<Button>(); backBtn.targetGraphic = backImg;
        AddText(MakeRect("Label", backGO, 0, 0, 180f, 48f), "BACK", Color.white, 22, bold: true, stretchToParent: true);
        backBtn.onClick.AddListener(() => SceneManager.LoadScene("LandingScene"));
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
