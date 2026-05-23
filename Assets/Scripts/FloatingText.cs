using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    private TextMeshPro textMesh;
    private float floatSpeed = 5.0f;
    private float fadeDuration = 0.2f;
    private float lifeTime = 0.5f;
    private Color startColor;

    public void Setup(string text, Color color)
    {
        textMesh = GetComponent<TextMeshPro>();
        if (textMesh == null) textMesh = gameObject.AddComponent<TextMeshPro>();
        
        textMesh.font = TMP_Settings.defaultFontAsset;
        textMesh.text = text;
        textMesh.color = color;
        textMesh.fontSize = 7;
        textMesh.alignment = TextAlignmentOptions.Center;
        startColor = color;
        
        // Manual scale pop
        transform.localScale = Vector3.one * 0.5f;
        StartCoroutine(PopScale());

        Destroy(gameObject, lifeTime);
    }

    private System.Collections.IEnumerator PopScale()
    {
        float t = 0;
        Vector3 startScale = Vector3.one * 0.5f;
        Vector3 targetScale = Vector3.one * 1.2f;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, targetScale, t / 0.1f);
            yield return null;
        }
        transform.localScale = targetScale;
    }

    void Update()
    {
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;
        
        // Look at camera
        if (Camera.main != null)
        {
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                             Camera.main.transform.rotation * Vector3.up);
        }

        // Fade out
        lifeTime -= Time.deltaTime;
        if (lifeTime < fadeDuration)
        {
            float alpha = lifeTime / fadeDuration;
            textMesh.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
        }
    }
}
