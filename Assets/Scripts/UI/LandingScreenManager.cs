using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Landing scene entry point: plays menu music and hosts the runtime-built
/// profile picker (ProfileSelectScreen), which routes into the main menu.
/// </summary>
public class LandingScreenManager : MonoBehaviour
{
    public string mainMenuSceneName = "MainMenuScene";

    void Start()
    {
        AudioManager.PlayMusicEvent("music_menu");

        var picker = GetComponent<ProfileSelectScreen>();
        if (picker == null) picker = gameObject.AddComponent<ProfileSelectScreen>();
        picker.mainMenuSceneName = mainMenuSceneName;
    }

    /// <summary>Legacy hook for the old scene's PLAY button — now routes to the main menu.</summary>
    public void OnPlayPressed() => ScreenFader.LoadScene(mainMenuSceneName);
}
