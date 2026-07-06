using UnityEngine;

/// <summary>
/// Base class for main-menu screens. Each screen builds its panel lazily on
/// first open and toggles it thereafter; MainMenuController drives navigation
/// via its screen stack.
/// </summary>
public abstract class MenuScreen : MonoBehaviour
{
    protected MainMenuController Controller { get; private set; }
    GameObject _panel;

    /// <summary>Overlays (popups) render on top without closing the screen beneath.</summary>
    public virtual bool IsOverlay => false;

    public void Open(MainMenuController controller)
    {
        Controller = controller;
        if (_panel == null) _panel = Build(controller.CanvasRoot);
        _panel.SetActive(true);
        Refresh();
    }

    public void Close() => _panel?.SetActive(false);

    /// <summary>Builds and returns the screen's root panel (called once).</summary>
    protected abstract GameObject Build(GameObject canvasRoot);

    /// <summary>Called every time the screen is (re)opened — update dynamic state here.</summary>
    protected virtual void Refresh() { }
}
