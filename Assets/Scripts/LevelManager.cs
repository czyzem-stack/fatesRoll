using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <remarks>Inherits <see cref="GameServiceBehaviour{T}"/> — auto-registers in Awake via <see cref="GameServices"/>.</remarks>
public class LevelManager : GameServiceBehaviour<LevelManager>
{

    [Header("UI References")]
    public Slider xpSlider;
    public TextMeshProUGUI levelText;

    private int currentLevel = 1;
    private float currentXP = 0f;
    private float xpToNextLevel;
    private float lastDisplayedXP = -1f;
    private float lastDisplayedXpMax = -1f;
    private int lastDisplayedLevel = -1;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    protected override void Start()
    {
        base.Start();
        CalculateXPRequirement();
        AutoAssignUI();
        UpdateUI();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AutoAssignUI();
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
            if (Mathf.Abs(currentXP - lastDisplayedXP) > 0.01f ||
                Mathf.Abs(xpToNextLevel - lastDisplayedXpMax) > 0.01f)
            {
                if (xpSlider.maxValue != xpToNextLevel)
                    xpSlider.maxValue = xpToNextLevel;
                xpSlider.value = currentXP;
                lastDisplayedXP = currentXP;
                lastDisplayedXpMax = xpToNextLevel;
            }
        }

        if (levelText != null && currentLevel != lastDisplayedLevel)
        {
            levelText.text = currentLevel.ToString();
            lastDisplayedLevel = currentLevel;
        }
    }

    [ContextMenu("Auto-Assign UI")]
    public void AutoAssignUI()
    {
        string[] sliderPaths =
        {
            "MainUI_Canvas/HUD_Profile/Slider_Top",
            "MainUI_Canvas/HUD_Profile/Slider_LevelProfile/Slider_Top",
        };

        foreach (string path in sliderPaths)
        {
            GameObject sliderGO = GameObject.Find(path);
            if (sliderGO == null)
                continue;
            xpSlider = sliderGO.GetComponent<Slider>();
            if (xpSlider != null)
                break;
        }

        string[] levelTextPaths =
        {
            "MainUI_Canvas/HUD_Profile/LevelBadge/Text",
            "MainUI_Canvas/HUD_Profile/LevelBadge/Text (TMP)",
        };

        foreach (string path in levelTextPaths)
        {
            GameObject textGO = GameObject.Find(path);
            if (textGO == null)
                continue;
            levelText = textGO.GetComponent<TextMeshProUGUI>();
            if (levelText != null)
                break;
        }

        UpdateUI();
    }
}
