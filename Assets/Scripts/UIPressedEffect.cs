using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UIPressedEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private float pressedScale = 0.95f;
    [SerializeField] private Color pressedColor = new Color(0.7f, 0.7f, 0.7f, 1.0f);
    
    private Vector3 originalScale;
    private List<Graphic> targetGraphics = new List<Graphic>();
    private Dictionary<Graphic, Color> originalColors = new Dictionary<Graphic, Color>();

    void Awake()
    {
        originalScale = transform.localScale;
        GetComponentsInChildren(true, targetGraphics);
        foreach (var g in targetGraphics)
        {
            originalColors[g] = g.color;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        transform.localScale = originalScale * pressedScale;
        foreach (var g in targetGraphics)
        {
            if (g != null) g.color = originalColors[g] * pressedColor;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetVisuals();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetVisuals();
    }

    private void ResetVisuals()
    {
        transform.localScale = originalScale;
        foreach (var g in targetGraphics)
        {
            if (g != null && originalColors.ContainsKey(g)) g.color = originalColors[g];
        }
    }
}
