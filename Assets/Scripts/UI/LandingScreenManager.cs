using UnityEngine;
using UnityEngine.SceneManagement;

public class LandingScreenManager : MonoBehaviour
{
    public string gameSceneName = "GameScene";

    public void OnPlayPressed() => SceneManager.LoadScene(gameSceneName);
}
