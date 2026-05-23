using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class UIButtonHoldProxy : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public float holdDuration = 1.0f;
    public UnityEvent onLongPress;
    public UnityEvent onClick;

    private bool isDown = false;
    private float downTime = 0f;
    private bool longPressTriggered = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        isDown = true;
        downTime = Time.time;
        longPressTriggered = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDown && !longPressTriggered)
        {
            onClick?.Invoke();
        }
        isDown = false;
    }

    void Update()
    {
        if (isDown && !longPressTriggered)
        {
            if (Time.time - downTime >= holdDuration)
            {
                longPressTriggered = true;
                onLongPress?.Invoke();
            }
        }
    }
}
