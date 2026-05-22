using UnityEngine;
using TMPro;

public class QAVersionDisplay : MonoBehaviour
{
    void Start()
    {
        var text = GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            string version = GitVersionProvider.GetBundleVersion();
            string hash = GitVersionProvider.GetShortHash();
            text.text = $"QA: Dev Build {version} ({hash})";
        }
    }
}
