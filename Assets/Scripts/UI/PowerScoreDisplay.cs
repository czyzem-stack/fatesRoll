using UnityEngine;
using TMPro;

public class PowerScoreDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI powerScoreText;

    private void OnEnable()
    {
        UpdateDisplay();
    }

    private void Update()
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (powerScoreText == null) return;
        
        var hero = GameServices.Hero;
        if (hero == null)
        {
            powerScoreText.text = "0";
            return;
        }

        var stats = hero.GetComponent<PlayerStats>();
        if (stats == null)
        {
            powerScoreText.text = "0";
            return;
        }

        powerScoreText.text = stats.PowerScore.ToString("N0");
    }
}
