using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    private TextMeshPro textMesh;
    private float floatSpeed = 1.5f;
    private float fadeDuration = 1.0f;
    private float lifeTime = 1.5f;
    private Color startColor;

    public void Setup(string text, Color color)
    {
        textMesh = GetComponent<TextMeshPro>();
        if (textMesh == null) textMesh = gameObject.AddComponent<TextMeshPro>();
        
        textMesh.font = TMP_Settings.defaultFontAsset;
        textMesh.text = text;
        textMesh.color = color;
        textMesh.fontSize = 6;
        textMesh.alignment = TextAlignmentOptions.Center;
        startColor = color;
        
        Destroy(gameObject, lifeTime);
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
