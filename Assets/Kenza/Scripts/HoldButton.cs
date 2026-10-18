using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// Reusable UI button that reports Press, Hold (every frame) and Release.
/// Put it on any UI element with a Graphic (Image) that has "Raycast Target" enabled.
/// Works with touch (multi-touch safe) and with the mouse in the Editor.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HoldButton : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Events")]
    public UnityEvent onPress;    // fired once when the finger goes down
    public UnityEvent onHold;     // fired every frame while held
    public UnityEvent onRelease;  // fired once when the finger lifts or slides off

    [Header("Visual feedback (optional)")]
    [Tooltip("Scale applied while pressed, so kids can see the button reacting.")]
    [SerializeField] private float pressedScale = 0.9f;

    public bool IsHeld { get; private set; }

    private int activePointerId = int.MinValue;
    private Vector3 normalScale;

    private void Awake()
    {
        normalScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsHeld) return;                 // ignore a second finger on the same button
        IsHeld = true;
        activePointerId = eventData.pointerId;
        transform.localScale = normalScale * pressedScale;
        onPress.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (IsHeld && eventData.pointerId == activePointerId) Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Finger slid off the button: treat as release so the car never gets "stuck" accelerating.
        if (IsHeld && eventData.pointerId == activePointerId) Release();
    }

    private void Update()
    {
        if (IsHeld) onHold.Invoke();
    }

    private void OnDisable()
    {
        // If the button is hidden/disabled while held (pause menu, scene change), release cleanly.
        if (IsHeld) Release();
    }

    private void Release()
    {
        IsHeld = false;
        activePointerId = int.MinValue;
        transform.localScale = normalScale;
        onRelease.Invoke();
    }
}
