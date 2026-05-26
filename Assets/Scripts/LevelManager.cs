using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <remarks>Inherits <see cref="GameServiceBehaviour{T}"/> — auto-registers in Awake via <see cref="GameServices"/>.</remarks>
public class LevelManager : GameServiceBehaviour<LevelManager>
{

    [Header("UI References")]
    public Slider xpSlider;
    public TextMeshProUGUI levelText;

    private int currentLevel = 1;
    private float currentXP = 0f;
    private float xpToNextLevel;

    private void Start()
    {
        CalculateXPRequirement();
        UpdateUI();
    }

    public int AddXP(float amount)
    {
        currentXP += amount;
        GlobalSettings.LogGameplay($"LevelManager: Gained {amount} XP. Total: {currentXP}/{xpToNextLevel}");

        int levelsGained = 0;
        while (currentXP >= xpToNextLevel)
        {
            LevelUp();
            levelsGained++;
        }

        UpdateUI();
        return levelsGained;
    }

    private void LevelUp()
    {
        currentXP -= xpToNextLevel;
        currentLevel++;
        CalculateXPRequirement();
        
        GlobalSettings.LogGameplay($"LevelManager: LEVELED UP! Now Level {currentLevel}");

        if (RogueLiteManager.Instance != null)
            RogueLiteManager.Instance.EnqueueLevelUp(currentLevel);

        var hero = GameServices.Hero;
        if (hero != null)
            hero.PlayLevelUpCelebration();
    }

    public int CurrentLevel => currentLevel;

    private void CalculateXPRequirement()
    {
        var settings = GlobalSettings.Instance;
        float baseXp = settings != null ? settings.baseXPForLevel1 : 50f;
        float mult = settings != null ? settings.xpExponentialMultiplier : 1.2f;
        xpToNextLevel = baseXp * Mathf.Pow(mult, currentLevel - 1);
    }

    private void UpdateUI()
    {
        if (xpSlider != null)
        {
            xpSlider.maxValue = xpToNextLevel;
            xpSlider.value = currentXP;
        }

        if (levelText != null)
        {
            levelText.text = currentLevel.ToString();
        }
    }

    [ContextMenu("Auto-Assign UI")]
    public void AutoAssignUI()
    {
        GameObject sliderGO = GameObject.Find("MainUI_Canvas/HUD_Profile/Slider_Top");
        if (sliderGO != null) xpSlider = sliderGO.GetComponent<Slider>();

        GameObject textGO = GameObject.Find("MainUI_Canvas/HUD_Profile/LevelBadge/Text");
        if (textGO != null) levelText = textGO.GetComponent<TextMeshProUGUI>();
        
        UpdateUI();
    }
}
