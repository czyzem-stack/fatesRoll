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
    public TextMeshProUGUI regenTimerText;

    [Header("Floating Text Settings")]
    public Color energyColor = new Color(0.75f, 0.25f, 0.75f, 1f); // Vibrant Purple

    private int currentEnergy;
    private float nextRegenTime;

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
        var settings = GlobalSettings.Instance;
        currentEnergy = settings != null ? settings.startingEnergy : 60;
        nextRegenTime = Time.time + (settings != null ? settings.energyRegenInterval : 15f);
        UpdateUI();
    }

    public void SpawnFloatingEnergyText(int amount)
    {
        var hero = Object.FindAnyObjectByType<HeroController>();
        if (hero != null)
        {
            GameObject go = new GameObject("FloatingText_Energy");
            go.transform.position = hero.transform.position + Vector3.up * 2.2f;
            
            var ft = go.AddComponent<FloatingText>();
            ft.Setup($"{amount} energy", energyColor);
        }
    }

    private void Update()
    {
        UpdateRegeneration();
        UpdateDisplay();
    }

    private void UpdateRegeneration()
    {
        if (currentEnergy >= GlobalSettings.Instance.maxEnergy) return;

        if (Time.time >= nextRegenTime)
        {
            AddEnergy(GlobalSettings.Instance.energyRegenAmount);
            nextRegenTime = Time.time + GlobalSettings.Instance.energyRegenInterval;
        }
    }

    private void UpdateDisplay()
    {
        // Main text (inside the box) now shows the actual energy
        if (energyText != null)
        {
            energyText.text = $"{currentEnergy}/{GlobalSettings.Instance.maxEnergy}";
        }

        // Small text (below the box) now shows the timer
        if (regenTimerText != null)
        {
            if (currentEnergy >= GlobalSettings.Instance.maxEnergy)
            {
                regenTimerText.text = "Energy Full";
            }
            else
            {
                float timeRemaining = nextRegenTime - Time.time;
                regenTimerText.text = $"More energy in {Mathf.CeilToInt(timeRemaining)}s";
            }
        }
    }

    public bool HasEnergy(int amount)
    {
        return currentEnergy >= amount;
    }

    public void Deplete(int amount)
    {
        bool wasAtMax = currentEnergy >= GlobalSettings.Instance.maxEnergy;
        
        currentEnergy -= amount;
        if (currentEnergy < 0) currentEnergy = 0;
        
        if (wasAtMax && currentEnergy < GlobalSettings.Instance.maxEnergy)
        {
            nextRegenTime = Time.time + GlobalSettings.Instance.energyRegenInterval;
        }

        UpdateUI();
        SpawnFloatingEnergyText(amount);
        GlobalSettings.LogGameplay($"EnergyManager: Depleted {amount}. Remaining: {currentEnergy}");
    }

    public void AddEnergy(int amount)
    {
        currentEnergy += amount;
        if (currentEnergy > GlobalSettings.Instance.maxEnergy)
            currentEnergy = GlobalSettings.Instance.maxEnergy;
        UpdateDisplay();
    }

    public void RestoreFull()
    {
        currentEnergy = GlobalSettings.Instance.maxEnergy;
        nextRegenTime = Time.time + GlobalSettings.Instance.energyRegenInterval;
        UpdateDisplay();
    }

    private void UpdateUI()
    {
        UpdateDisplay();
    }

    [ContextMenu("Auto-Assign UI")]
    public void AutoAssignUI()
    {
        GameObject go = GameObject.Find("MainUI_Canvas/HUD_Resources/HUD_Item_Energy/Energy/Text (TMP)");
        if (go != null)
        {
            energyText = go.GetComponent<TextMeshProUGUI>();
        }

        GameObject totalGo = GameObject.Find("MainUI_Canvas/HUD_Resources/HUD_Item_Energy/Energy/EnergyTotal_Text");
        if (totalGo != null)
        {
            regenTimerText = totalGo.GetComponent<TextMeshProUGUI>();
        }
        
        UpdateDisplay();
    }
}
