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
        if (cachedGlobalHUD == null)
            ResolveGlobalHudCache();
        ToggleGlobalHUD(false);
        UpdateUI();
    }

    private void OnDisable()
    {
        ToggleGlobalHUD(true);
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

    private void Update()
    {
        if (LootManager.Instance != null)
        {
            if (goldText != null) goldText.text = LootManager.Instance.CurrentGold.ToString("N0");
            if (gemText != null) gemText.text = LootManager.Instance.CurrentGems.ToString("N0");
        }
    }

    public void UpdateUI()
    {
        if (LootManager.Instance != null)
        {
            if (goldText != null) goldText.text = LootManager.Instance.CurrentGold.ToString("N0");
            if (gemText != null) gemText.text = LootManager.Instance.CurrentGems.ToString("N0");
        }
    }
}
