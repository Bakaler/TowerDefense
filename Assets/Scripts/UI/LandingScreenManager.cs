using UnityEngine;
using UnityEngine.SceneManagement;

public class LandingScreenManager : MonoBehaviour
{
    public string levelSelectSceneName = "LevelSelectionScene";

    public void OnPlayPressed() => SceneManager.LoadScene(levelSelectSceneName);
}
