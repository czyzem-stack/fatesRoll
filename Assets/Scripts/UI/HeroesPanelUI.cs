using UnityEngine;
using TMPro;

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

    private GameObject cachedGlobalHUD;

    private void Awake()
    {
        ResolveGlobalHudCache();
    }

    private void OnEnable()
    {
        LootManager.BalanceChanged += HandleBalanceChanged;
        LevelManager.ProgressChanged += HandleLevelChanged;
        if (cachedGlobalHUD == null)
            ResolveGlobalHudCache();
        ToggleGlobalHUD(false);
        UpdateUI();
    }

    private void OnDisable()
    {
        LootManager.BalanceChanged -= HandleBalanceChanged;
        LevelManager.ProgressChanged -= HandleLevelChanged;
        ToggleGlobalHUD(true);
    }

    private void HandleBalanceChanged()
    {
        UpdateResources();
    }

    private void HandleLevelChanged()
    {
        UpdateHeroLevel();
    }

    private void ResolveGlobalHudCache()
    {
        cachedGlobalHUD = MainUiHud.FindGlobalResourcesHud();
    }

    private void ToggleGlobalHUD(bool visible)
    {
        if (cachedGlobalHUD == null)
            ResolveGlobalHudCache();
        if (cachedGlobalHUD != null)
            cachedGlobalHUD.SetActive(visible);
    }

    public void UpdateUI()
    {
        UpdateResources();
        UpdateHeroLevel();

        if (selectButton != null)
            selectButton.interactable = false;
        if (activeHeroGlow != null)
            activeHeroGlow.SetActive(true);

        if (addCard1 != null)
            addCard1.SetActive(false);
        if (addCard2 != null)
            addCard2.SetActive(false);

        if (lockCards != null)
        {
            foreach (var lockCard in lockCards)
            {
                if (lockCard != null)
                    lockCard.SetActive(true);
            }
        }
    }

    private void UpdateResources()
    {
        if (!GameServices.TryGet(out LootManager loot))
            return;

        if (goldText != null)
            goldText.text = loot.CurrentGold.ToString("N0");
        if (gemText != null)
            gemText.text = loot.CurrentGems.ToString("N0");
    }

    private void UpdateHeroLevel()
    {
        if (steveLevelText == null || !GameServices.TryGet(out LevelManager levels))
            return;

        steveLevelText.text = "LV." + levels.CurrentLevel;
    }
}
