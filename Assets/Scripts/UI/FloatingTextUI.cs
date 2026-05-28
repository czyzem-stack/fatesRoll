using UnityEngine;
using TMPro;
using System.Collections;

public class FloatingTextUI : MonoBehaviour
{
    private TextMeshProUGUI textMesh;
    private float floatSpeed = 100.0f;
    private float fadeDuration = 0.5f;
    private float lifeTime = 1.2f;
    private Color startColor;

    public void Setup(string text, Color color)
    {
        textMesh = GetComponent<TextMeshProUGUI>();
        if (textMesh == null) textMesh = gameObject.AddComponent<TextMeshProUGUI>();
        
        textMesh.text = text;
        textMesh.color = color;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.raycastTarget = false;
        startColor = color;
        
        transform.localScale = Vector3.zero;
        StartCoroutine(LifecycleRoutine());
    }

    private IEnumerator LifecycleRoutine()
    {
        // Pop in
        float t = 0;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(0f, 1.2f, t / 0.2f);
            transform.localScale = new Vector3(s, s, s);
            yield return null;
        }
        transform.localScale = Vector3.one;

        // Float and Fade
        float elapsed = 0;
        while (elapsed < lifeTime)
        {
            elapsed += Time.deltaTime;
            transform.position += Vector3.up * floatSpeed * Time.deltaTime;

            if (elapsed > (lifeTime - fadeDuration))
            {
                float alpha = 1.0f - ((elapsed - (lifeTime - fadeDuration)) / fadeDuration);
                textMesh.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
