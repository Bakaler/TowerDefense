using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Landing-scene profile picker: one card per save slot. Existing profiles
/// show name/stars/progress and load on click; empty slots offer inline
/// name entry to create a new profile. Delete is two-click confirmed.
/// </summary>
public class ProfileSelectScreen : MonoBehaviour
{
    public string mainMenuSceneName = "MainMenuScene";

    const float CARD_W = 380f, CARD_H = 320f, CARD_GAP = 40f;

    GameObject _canvasRoot;

    void Start() => Rebuild();

    void Rebuild()
    {
        if (_canvasRoot != null) Destroy(_canvasRoot);
        _canvasRoot = UICanvasFactory.CreateCanvas("--- PROFILE UI ---", sortingOrder: 20);

        UIControlFactory.Label(_canvasRoot, "Header", 0f, 120f, 700f, 50f,
            "SELECT PROFILE", UIControlFactory.TextColor, 34, TextAnchor.MiddleCenter, bold: true);

        float totalW = SaveManager.MaxProfiles * CARD_W + (SaveManager.MaxProfiles - 1) * CARD_GAP;
        float startX = -totalW * 0.5f + CARD_W * 0.5f;

        for (int slot = 0; slot < SaveManager.MaxProfiles; slot++)
            BuildCard(slot, startX + slot * (CARD_W + CARD_GAP), -120f);
    }

    void BuildCard(int slot, float x, float y)
    {
        var card    = UIControlFactory.Panel($"Slot{slot}", _canvasRoot, x, y, CARD_W, CARD_H, UIControlFactory.PanelColor);
        var summary = SaveManager.GetProfileSummary(slot);

        if (summary.exists) BuildExistingCard(card, slot, summary);
        else                BuildEmptyCard(card, slot);
    }

    // ── Existing profile ──────────────────────────────────────────────

    void BuildExistingCard(GameObject card, int slot, SaveManager.ProfileSummary summary)
    {
        UIControlFactory.Label(card, "Name", 0f, 95f, CARD_W - 80f, 50f,
            summary.name, UIControlFactory.TitleColor, 32, TextAnchor.MiddleCenter, bold: true);
        UIControlFactory.Label(card, "Stars", 0f, 40f, CARD_W - 40f, 34f,
            $"★ {summary.totalStars}", new Color(1f, 0.85f, 0.3f), 24);
        UIControlFactory.Label(card, "Levels", 0f, 5f, CARD_W - 40f, 30f,
            $"{summary.levelsBeaten} level{(summary.levelsBeaten == 1 ? "" : "s")} beaten", UIControlFactory.TextDim, 18);

        string played = FormatLastPlayed(summary.lastPlayedUtc);
        if (played != null)
            UIControlFactory.Label(card, "Played", 0f, -25f, CARD_W - 40f, 26f, played, UIControlFactory.TextDim, 15);

        var (playBtn, _) = UIControlFactory.Button(card, "Select", 0f, -95f, CARD_W - 80f, 60f,
            UIControlFactory.ButtonGreen, "PLAY", 26);
        playBtn.onClick.AddListener(() =>
        {
            SaveManager.SetActiveProfile(slot);
            SceneManager.LoadScene(mainMenuSceneName);
        });

        // Delete: first click arms it, second confirms
        var (delBtn, delLbl) = UIControlFactory.Button(card, "Delete",
            CARD_W * 0.5f - 24f, CARD_H * 0.5f - 24f, 36f, 36f,
            new Color(0.35f, 0.10f, 0.10f, 1f), "×", 20);
        bool armed = false;
        delBtn.onClick.AddListener(() =>
        {
            if (!armed)
            {
                armed = true;
                delLbl.text = "!";
                delBtn.image.color = UIControlFactory.ButtonRed;
                return;
            }
            SaveManager.DeleteProfile(slot);
            Rebuild();
        });
    }

    static string FormatLastPlayed(string utcIso)
    {
        if (string.IsNullOrEmpty(utcIso)) return null;
        return System.DateTime.TryParse(utcIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? $"Last played {dt.ToLocalTime():d MMM yyyy}"
            : null;
    }

    // ── Empty slot / creation ─────────────────────────────────────────

    void BuildEmptyCard(GameObject card, int slot)
    {
        var (newBtn, _) = UIControlFactory.Button(card, "New", 0f, 0f, CARD_W - 80f, 70f,
            new Color(0.14f, 0.20f, 0.30f, 1f), "+  NEW PROFILE", 22);
        newBtn.onClick.AddListener(() => ShowCreateForm(card, slot, newBtn.gameObject));
    }

    void ShowCreateForm(GameObject card, int slot, GameObject newBtn)
    {
        newBtn.SetActive(false);

        UIControlFactory.Label(card, "Prompt", 0f, 80f, CARD_W - 40f, 34f,
            "Profile name", UIControlFactory.TextColor, 20);

        var input = MakeNameInput(card, 0f, 30f, CARD_W - 80f, 52f, $"Profile {slot + 1}");

        var (createBtn, _) = UIControlFactory.Button(card, "Create", 0f, -50f, CARD_W - 80f, 54f,
            UIControlFactory.ButtonGreen, "CREATE", 22);
        createBtn.onClick.AddListener(() =>
        {
            SaveManager.CreateProfile(slot, input.text);
            SaveManager.SetActiveProfile(slot);
            SceneManager.LoadScene(mainMenuSceneName);
        });

        var (cancelBtn, _) = UIControlFactory.Button(card, "Cancel", 0f, -115f, CARD_W - 80f, 44f,
            new Color(0.25f, 0.25f, 0.32f, 1f), "CANCEL", 18);
        cancelBtn.onClick.AddListener(Rebuild);

        input.Select();
        input.ActivateInputField();
    }

    static InputField MakeNameInput(GameObject parent, float x, float y, float w, float h, string placeholder)
    {
        var go  = UIControlFactory.Panel("NameInput", parent, x, y, w, h, new Color(0.03f, 0.03f, 0.05f, 1f));
        var input = go.AddComponent<InputField>();

        var text = UIControlFactory.Label(go, "Text", 0f, 0f, w - 20f, h, "", Color.white, 22, TextAnchor.MiddleLeft);
        text.supportRichText = false;
        var ph = UIControlFactory.Label(go, "Placeholder", 0f, 0f, w - 20f, h, placeholder,
            new Color(0.4f, 0.4f, 0.5f, 1f), 22, TextAnchor.MiddleLeft);

        input.textComponent  = text;
        input.placeholder    = ph;
        input.characterLimit = 16;
        input.targetGraphic  = go.GetComponent<Image>();
        return input;
    }
}
