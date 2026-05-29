using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TalentUIController : MonoBehaviour
{
    [Header("Top Bar")]
    public TextMeshProUGUI topCoinText;

    [Header("Level Panel")]
    public Slider xpSlider;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI xpPercentText;

    [Header("Upgrade Button")]
    public Button upgradeButton;
    public TextMeshProUGUI upgradeCostText;

    [Header("Roulette")]
    public Transform rolesRoot;
    public float spinDuration = 2f;
    public int minSpins = 3;

    [Header("Stat Bonuses")]
    public List<TextMeshProUGUI> statBonusTexts; // 10 texts corresponding to categories

    private GameObject[] roleHighlights;
    private bool isSpinning = false;
    private PowerScoreDisplay powerScoreDisplay;

    private void Awake()
    {
        InitializeRoles();
        powerScoreDisplay = GetComponentInChildren<PowerScoreDisplay>(true);
    }

    private void OnEnable()
    {
        UpdateUI();
    }

    private void Update()
    {
        if (!isSpinning)
        {
            UpdateResources();
        }
    }

    private void InitializeRoles()
    {
        if (rolesRoot == null) return;
        
        int childCount = rolesRoot.childCount;
        roleHighlights = new GameObject[childCount];
        for (int i = 0; i < childCount; i++)
        {
            Transform role = rolesRoot.GetChild(i);
            Transform focus = role.Find("RoleButton_Focus");
            Transform normal = role.Find("RoleButton");
            
            if (focus != null) roleHighlights[i] = focus.gameObject;
            
            // Set default state: all normal active, all focus inactive
            if (normal != null) normal.gameObject.SetActive(true);
            if (focus != null) focus.gameObject.SetActive(false);
        }
    }

    public void UpdateUI()
    {
        UpdateResources();
        UpdateLevel();
        UpdateUpgradeButton();
        UpdateStatBonuses();
    }

    private void UpdateResources()
    {
        if (topCoinText != null && LootManager.Instance != null)
            topCoinText.text = LootManager.Instance.CurrentGold.ToString("N0");
    }

    private void UpdateLevel()
    {
        if (LevelManager.Instance == null) return;
        
        float currentXP = LevelManager.Instance.CurrentXP;
        float maxXp = LevelManager.Instance.XPToNextLevel;

        if (xpSlider != null)
        {
            xpSlider.maxValue = maxXp;
            xpSlider.value = currentXP;
        }

        if (levelText != null)
        {
            levelText.text = LevelManager.Instance.CurrentLevel.ToString();
        }

        if (xpPercentText != null && maxXp > 0)
        {
            float pct = (currentXP / maxXp) * 100f;
            xpPercentText.text = pct.ToString("F1") + "%";
        }
    }

    private void UpdateUpgradeButton()
    {
        if (upgradeButton == null || !GameServices.TryGet(out TalentManager talents))
            return;

        int cost = talents.GetCurrentCost();
        if (upgradeCostText != null)
            upgradeCostText.text = cost.ToString();

        upgradeButton.interactable = talents.CanAffordUpgrade() && !isSpinning;
    }

    private void UpdateStatBonuses()
    {
        if (statBonusTexts == null || !GameServices.TryGet(out TalentManager talents))
            return;

        for (int i = 0; i < statBonusTexts.Count; i++)
        {
            if (statBonusTexts[i] == null) continue;
            float bonus = talents.GetTotalBonus(i);
            statBonusTexts[i].text = "+" + bonus.ToString("F0");
            statBonusTexts[i].color = bonus > 0 ? Color.green : Color.white;
        }
    }

    public void OnUpgradeClicked()
    {
        if (isSpinning)
            return;

        if (!GameServices.TryGet(out TalentManager talents))
            return;

        if (powerScoreDisplay != null)
            powerScoreDisplay.Lock();

        int resultCategory = talents.PerformUpgrade();
        if (resultCategory != -1)
            StartCoroutine(RouletteRoutine(resultCategory));
    }

    private IEnumerator RouletteRoutine(int targetIndex)
    {
        isSpinning = true;
        upgradeButton.interactable = false;

        // Reset highlights
        foreach (var h in roleHighlights) if (h != null) h.SetActive(false);

        int currentIndex = 0;
        float currentInterval = 0.05f;
        int totalSteps = (minSpins * roleHighlights.Length) + targetIndex;
        int currentStep = 0;

        while (currentStep < totalSteps)
        {
            if (roleHighlights[currentIndex] != null) roleHighlights[currentIndex].SetActive(false);
            
            currentIndex = (currentIndex + 1) % roleHighlights.Length;
            currentStep++;

            if (roleHighlights[currentIndex] != null) roleHighlights[currentIndex].SetActive(true);

            // Ease out interval
            float progress = (float)currentStep / totalSteps;
            currentInterval = Mathf.Lerp(0.05f, 0.4f, progress * progress);

            yield return new WaitForSeconds(currentInterval);
        }

        // Finish
        isSpinning = false;
        if (powerScoreDisplay != null) powerScoreDisplay.UnlockAndShowDelta();
        UpdateUI();
    }
}
