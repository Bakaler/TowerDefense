using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the main-menu canvas and the screen stack. Screens live as sibling
/// components on this GameObject and build their panels lazily into
/// CanvasRoot; Push/Back handles navigation (Escape = Back).
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public string levelSelectSceneName = "LevelSelectionScene";
    public string landingSceneName     = "LandingScene";

    public GameObject CanvasRoot { get; private set; }

    readonly Stack<MenuScreen> _stack = new();

    void Start()
    {
        AudioManager.PlayMusicEvent("music_menu");
        CanvasRoot = UICanvasFactory.CreateCanvasWithBackground("--- MENU UI ---", UICanvasFactory.MenuBackground);
        Push(Get<MainMenuScreen>());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Back();
    }

    /// <summary>Returns the screen component of that type, adding it if missing.</summary>
    public T Get<T>() where T : MenuScreen
    {
        var screen = GetComponent<T>();
        return screen != null ? screen : gameObject.AddComponent<T>();
    }

    public void Push(MenuScreen screen)
    {
        if (_stack.Count > 0 && !screen.IsOverlay) _stack.Peek().Close();
        _stack.Push(screen);
        screen.Open(this);
    }

    public void Back()
    {
        if (_stack.Count <= 1) return;   // root screen stays
        _stack.Pop().Close();
        _stack.Peek().Open(this);
    }
}
