using UnityEngine;
using TMPro;

public class ShopPanelUI : MonoBehaviour
{
    [Header("Resources")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI gemText;

    private GameObject cachedGlobalHUD;

    private void Awake()
    {
        ResolveGlobalHudCache();
    }

    private void OnEnable()
    {
        LootManager.BalanceChanged += HandleBalanceChanged;
        if (cachedGlobalHUD == null)
            ResolveGlobalHudCache();
        ToggleGlobalHUD(false);
        UpdateUI();
    }

    private void OnDisable()
    {
        LootManager.BalanceChanged -= HandleBalanceChanged;
        ToggleGlobalHUD(true);
    }

    private void HandleBalanceChanged()
    {
        UpdateUI();
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
        if (!GameServices.TryGet(out LootManager loot))
            return;

        if (goldText != null)
            goldText.text = loot.CurrentGold.ToString("N0");
        if (gemText != null)
            gemText.text = loot.CurrentGems.ToString("N0");
    }
}
