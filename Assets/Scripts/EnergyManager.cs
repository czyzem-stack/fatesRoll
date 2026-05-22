using UnityEngine;
using TMPro;

public class EnergyManager : MonoBehaviour
{
    private static EnergyManager _instance;
    public static EnergyManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindAnyObjectByType<EnergyManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("EnergyManager");
                    _instance = go.AddComponent<EnergyManager>();
                }
            }
            return _instance;
        }
    }

    [Header("UI References")]
    public TextMeshProUGUI energyText;

    private int currentEnergy;

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
        currentEnergy = GlobalSettings.Instance.startingEnergy;
        UpdateUI();
    }

    public void Deplete(int amount)
    {
        currentEnergy -= amount;
        if (currentEnergy < 0) currentEnergy = 0;
        UpdateUI();
        Debug.Log($"EnergyManager: Depleted {amount}. Remaining: {currentEnergy}");
    }

    public void AddEnergy(int amount)
    {
        currentEnergy += amount;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (energyText != null)
        {
            energyText.text = $"{currentEnergy} / {GlobalSettings.Instance.maxEnergy}";
        }
    }

    [ContextMenu("Auto-Assign UI")]
    public void AutoAssignUI()
    {
        GameObject go = GameObject.Find("MainUI_Canvas/HUD_Resources/HUD_Item_Energy/Energy/Text (TMP)");
        if (go != null)
        {
            energyText = go.GetComponent<TextMeshProUGUI>();
            UpdateUI();
        }
    }
}
