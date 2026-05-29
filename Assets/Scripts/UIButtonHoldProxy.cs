using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class UIButtonHoldProxy : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public float holdDuration = 1.0f;
    public UnityEvent onLongPress;
    public UnityEvent onClick;
    public UnityEvent onPointerDown;
    public UnityEvent onPointerUp;

    private bool isDown = false;
    private float downTime = 0f;
    private bool longPressTriggered = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        isDown = true;
        downTime = Time.unscaledTime;
        longPressTriggered = false;
        onPointerDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDown && !longPressTriggered)
        {
            onClick?.Invoke();
        }
        isDown = false;
        onPointerUp?.Invoke();
    }

    void Update()
    {
        if (isDown && !longPressTriggered)
        {
            if (Time.unscaledTime - downTime >= holdDuration)
            {
                longPressTriggered = true;
                onLongPress?.Invoke();
            }
        }
    }
}
