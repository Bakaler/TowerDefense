using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Slide-in toast shown the moment an achievement is earned. Lives on the
/// AchievementManager object; queues multiple earns and shows them one at a
/// time on its own high-sorting canvas (above the victory overlay).
/// </summary>
public class AchievementToastUI : MonoBehaviour
{
    const float ToastW    = 460f;
    const float SlideTime = 0.35f;
    const float HoldTime  = 3.0f;

    readonly Queue<AchievementDefinition> _queue = new();
    bool _draining;

    void OnEnable()  => AchievementManager.OnAchievementEarned += Enqueue;
    void OnDisable() => AchievementManager.OnAchievementEarned -= Enqueue;

    void Enqueue(AchievementDefinition def)
    {
        _queue.Enqueue(def);
        if (!_draining) StartCoroutine(Drain());
    }

    IEnumerator Drain()
    {
        _draining = true;
        var canvasRoot = UICanvasFactory.CreateCanvas("--- ACHIEVEMENT TOAST ---", sortingOrder: 200);
        DontDestroyOnLoad(canvasRoot);

        while (_queue.Count > 0)
            yield return ShowToast(canvasRoot, _queue.Dequeue());

        Destroy(canvasRoot);
        _draining = false;
    }

    IEnumerator ShowToast(GameObject canvasRoot, AchievementDefinition def)
    {
        var view = AchievementBannerFactory.Create(canvasRoot, def, ToastW);
        view.SetEarned(true);

        // Small "earned" caption above the banner
        UIControlFactory.Label(view.Root, "Caption", 0f, AchievementBannerFactory.Height * 0.5f + 16f,
            ToastW, 26f, "ACHIEVEMENT EARNED", new Color(1f, 0.85f, 0.3f, 1f), 15,
            TextAnchor.MiddleCenter, bold: true);

        // Anchor top-right; slide in from off-screen
        var rt   = view.Root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        float shownX = -24f, hiddenX = ToastW + 40f;
        float y      = -70f;

        for (float t = 0f; t < SlideTime; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / SlideTime);
            rt.anchoredPosition = new Vector2(Mathf.Lerp(hiddenX, shownX, k), y);
            yield return null;
        }
        rt.anchoredPosition = new Vector2(shownX, y);

        yield return new WaitForSecondsRealtime(HoldTime);

        for (float t = 0f; t < SlideTime; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / SlideTime);
            rt.anchoredPosition = new Vector2(Mathf.Lerp(shownX, hiddenX, k), y);
            yield return null;
        }

        Destroy(view.Root);
    }
}
