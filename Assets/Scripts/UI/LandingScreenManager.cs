using UnityEngine;
using UnityEngine.SceneManagement;

public class LandingScreenManager : MonoBehaviour
{
    public string levelSelectSceneName = "LevelSelectionScene";

    void Start() => AudioManager.PlayMusicEvent("music_menu");

    public void OnPlayPressed() => SceneManager.LoadScene(levelSelectSceneName);
}
