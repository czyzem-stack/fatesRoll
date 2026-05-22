using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelManager : MonoBehaviour
{
    private static LevelManager _instance;
    public static LevelManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindAnyObjectByType<LevelManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("LevelManager");
                    _instance = go.AddComponent<LevelManager>();
                }
            }
            return _instance;
        }
    }

    [Header("UI References")]
    public Slider xpSlider;
    public TextMeshProUGUI levelText;

    private int currentLevel = 1;
    private float currentXP = 0f;
    private float xpToNextLevel;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Start()
    {
        CalculateXPRequirement();
        UpdateUI();
    }

    public bool AddXP(float amount)
    {
        currentXP += amount;
        Debug.Log($"LevelManager: Gained {amount} XP. Total: {currentXP}/{xpToNextLevel}");

        bool didLevelUp = false;
        while (currentXP >= xpToNextLevel)
        {
            LevelUp();
            didLevelUp = true;
        }

        UpdateUI();
        return didLevelUp;
    }

    private void LevelUp()
    {
        currentXP -= xpToNextLevel;
        currentLevel++;
        CalculateXPRequirement();
        
        Debug.Log($"LevelManager: LEVELED UP! Now Level {currentLevel}");

        // Play Level Up Animation on Steve
        var hero = Object.FindAnyObjectByType<HeroController>();
        if (hero != null)
        {
            var anim = hero.GetComponent<Animator>();
            if (anim != null)
            {
                anim.SetTrigger("LevelUp");
            }
        }
    }

    private void CalculateXPRequirement()
    {
        var settings = GlobalSettings.Instance;
        // Formula: base * (multiplier ^ (level-1))
        xpToNextLevel = settings.baseXPForLevel1 * Mathf.Pow(settings.xpExponentialMultiplier, currentLevel - 1);
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
