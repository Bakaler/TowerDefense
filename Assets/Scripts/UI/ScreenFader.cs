using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Black fade between scenes. Self-bootstraps and persists; every scene load
/// fades in from black automatically, and ScreenFader.LoadScene(name) fades
/// out before loading. Falls back to a plain load if the fader is missing.
/// Uses unscaled time so pause (timeScale 0) can't stall a transition.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    const float FadeTime = 0.2f;

    static ScreenFader _instance;

    Image     _black;
    bool      _fading;
    Coroutine _fadeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[ScreenFader]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ScreenFader>();
    }

    void Awake()
    {
        var canvasGO = new GameObject("FadeCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;   // above everything, toasts included

        var imgGO = new GameObject("Black");
        imgGO.transform.SetParent(canvasGO.transform, false);
        var rt       = imgGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _black = imgGO.AddComponent<Image>();
        _black.color = new Color(0f, 0f, 0f, 1f);   // first frame starts black → fade in

        SceneManager.sceneLoaded += OnSceneLoaded;
        StartFade(1f, 0f);
    }

    void OnDestroy()
    {
        if (_instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartFade(_black.color.a, 0f);

    void StartFade(float from, float to)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(Fade(from, to));
    }

    /// <summary>Fade to black, then load. Safe to call from anywhere.</summary>
    public static void LoadScene(string sceneName)
    {
        if (_instance == null || _instance._fading) { SceneManager.LoadScene(sceneName); return; }
        _instance.StartCoroutine(_instance.FadeAndLoad(sceneName));
    }

    IEnumerator FadeAndLoad(string sceneName)
    {
        yield return Fade(_black.color.a, 1f);
        SceneManager.LoadScene(sceneName);   // sceneLoaded handler fades back in
    }

    IEnumerator Fade(float from, float to)
    {
        _fading = true;
        _black.raycastTarget = true;   // block clicks mid-transition
        for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
        {
            _black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / FadeTime));
            yield return null;
        }
        _black.color         = new Color(0f, 0f, 0f, to);
        _black.raycastTarget = to > 0.01f;
        _fading = false;
    }
}
