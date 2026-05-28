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
        // Cache HUD on Awake to avoid Find calls in OnDisable
        var hudGO = GameObject.Find("MainUI_Canvas/Resources");
        if (hudGO != null) cachedGlobalHUD = hudGO;
    }

    private void OnEnable()
    {
        ToggleGlobalHUD(false);
        UpdateUI();
    }

    private void OnDisable()
    {
        ToggleGlobalHUD(true);
    }

    private void ToggleGlobalHUD(bool visible)
    {
        if (cachedGlobalHUD != null)
        {
            cachedGlobalHUD.SetActive(visible);
        }
        else
        {
            // Fallback if cache is null, but avoid deep path find which causes assertions in OnDisable
            var hud = GameObject.Find("Resources");
            if (hud != null) hud.SetActive(visible);
        }
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
