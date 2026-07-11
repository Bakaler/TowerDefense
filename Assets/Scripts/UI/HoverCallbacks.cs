using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Attach next to any raycast-target Graphic for simple pointer enter/exit callbacks.</summary>
public class HoverCallbacks : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action onEnter;
    public System.Action onExit;

    public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
    public void OnPointerExit(PointerEventData e)  => onExit?.Invoke();
}
