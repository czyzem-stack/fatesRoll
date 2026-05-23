using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    private TextMeshPro textMesh;
    private float floatSpeed = 2.0f;
    private float fadeDuration = 0.3f;
    private float lifeTime = 0.8f;
    private Color startColor;

    public void Setup(string text, Color color)
    {
        textMesh = GetComponent<TextMeshPro>();
        if (textMesh == null) textMesh = gameObject.AddComponent<TextMeshPro>();
        
        // Ensure we have a font, otherwise TMP might render nothing or a giant block
        textMesh.font = TMP_Settings.defaultFontAsset;
        textMesh.text = text;
        textMesh.color = color;
        
        // Font size 3 is about 1/10th of a meter tall in world space by default
        textMesh.fontSize = 3; 
        textMesh.alignment = TextAlignmentOptions.Center;
        startColor = color;
        
        // Start small and pop up
        transform.localScale = Vector3.zero;
        StartCoroutine(PopScale());

        Destroy(gameObject, lifeTime);
    }

    private System.Collections.IEnumerator PopScale()
    {
        float t = 0;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(0f, 1.2f, t / 0.15f);
            transform.localScale = new Vector3(s, s, s);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        if (textMesh == null) return;

        // Move up slowly
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;
        
        // Face the camera directly
        if (Camera.main != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }

        // Fade out at the end of life
        lifeTime -= Time.deltaTime;
        if (lifeTime < fadeDuration)
        {
            float alpha = Mathf.Clamp01(lifeTime / fadeDuration);
            textMesh.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
        }
    }
}
