using UnityEngine;
using TMPro;

public class PowerScoreDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI powerScoreText;
    private float displayedScore = -1;
    private bool isLocked = false;

    private void OnEnable()
    {
        UpdateDisplay();
    }

    private void Update()
    {
        if (!isLocked)
        {
            UpdateDisplay();
        }
    }

    public void Lock()
    {
        isLocked = true;
    }

    public void UnlockAndShowDelta()
    {
        isLocked = false;
        float oldScore = displayedScore;
        UpdateDisplay();
        float newScore = displayedScore;

        if (newScore > oldScore && oldScore >= 0)
        {
            ShowFloatingText("+" + Mathf.RoundToInt(newScore - oldScore).ToString("N0"));
        }
    }

    private void UpdateDisplay()
    {
        if (powerScoreText == null) return;
        
        var hero = GameServices.Hero;
        if (hero == null)
        {
            powerScoreText.text = "0";
            displayedScore = 0;
            return;
        }

        var stats = hero.GetComponent<PlayerStats>();
        if (stats == null)
        {
            powerScoreText.text = "0";
            displayedScore = 0;
            return;
        }

        displayedScore = stats.PowerScore;
        powerScoreText.text = displayedScore.ToString("N0");
    }

    private void ShowFloatingText(string text)
    {
        GameObject go = new GameObject("PowerScoreFloatingText");
        go.transform.SetParent(transform.parent, false);
        // Position it above the score text. 
        // Since we are in UI, we can use the position of this object as a base.
        go.transform.position = transform.position + Vector3.up * 50f; 
        
        var ft = go.AddComponent<FloatingTextUI>();
        ft.Setup(text, Color.green);
    }
}
