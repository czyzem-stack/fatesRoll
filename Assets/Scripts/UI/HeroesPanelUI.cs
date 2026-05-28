using UnityEngine;
using TMPro;
using System.Linq;

public class HeroesPanelUI : MonoBehaviour
{
    [Header("Resources")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI gemText;

    [Header("Hero Info")]
    public TextMeshProUGUI steveLevelText;

    [Header("UI Elements")]
    public GameObject activeHeroGlow;
    public UnityEngine.UI.Button selectButton;

    [Header("Cards to manage")]
    public GameObject addCard1;
    public GameObject addCard2;
    public GameObject[] lockCards;

    private void OnEnable()
    {
        UpdateUI();
    }

    private void Update()
    {
        // Keep resources in sync if they change while panel is open
        if (LootManager.Instance != null)
        {
            if (goldText != null) goldText.text = LootManager.Instance.CurrentGold.ToString("N0");
            if (gemText != null) gemText.text = LootManager.Instance.CurrentGems.ToString("N0");
        }
    }

    public void UpdateUI()
    {
        // 1. Resources
        if (LootManager.Instance != null)
        {
            if (goldText != null) goldText.text = LootManager.Instance.CurrentGold.ToString("N0");
            if (gemText != null) gemText.text = LootManager.Instance.CurrentGems.ToString("N0");
        }

        // 2. Hero Level
        if (LevelManager.Instance != null && steveLevelText != null)
            steveLevelText.text = "LV." + LevelManager.Instance.CurrentLevel.ToString();

        // 3. UI State — Steve is the active hero
        if (selectButton != null) selectButton.interactable = false;
        if (activeHeroGlow != null) activeHeroGlow.SetActive(true);

        // 4. Manage cards
        if (addCard1 != null) addCard1.SetActive(false);
        if (addCard2 != null) addCard2.SetActive(false);
        
        // Ensure we have enough cards to scroll, keeping locks active
        foreach (var lockCard in lockCards)
        {
            if (lockCard != null) lockCard.SetActive(true);
        }
    }
}
