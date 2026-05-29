using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

/// <remarks>Inherits <see cref="GameServiceBehaviour{T}"/> — auto-registers in Awake via <see cref="GameServices"/>.</remarks>
public class EnergyManager : GameServiceBehaviour<EnergyManager>
{

    [Header("UI References")]
    public TextMeshProUGUI energyText;
    public TextMeshProUGUI regenTimerText;

    [Header("Floating Text Settings")]
    public Color energyColor = new Color(0.75f, 0.25f, 0.75f, 1f); // Vibrant Purple

    private int currentEnergy;
    private int talentMaxEnergyBonus;
    private float nextRegenTime;

    public int GetMaxEnergy()
    {
        var settings = GlobalSettings.Instance;
        return (settings != null ? settings.maxEnergy : 60) + talentMaxEnergyBonus;
    }

    /// <summary>Raises max energy from talent upgrades and grants the same amount of current energy.</summary>
    public void AddMaxEnergyBonus(int bonus)
    {
        if (bonus <= 0)
            return;

        talentMaxEnergyBonus += bonus;
        currentEnergy += bonus;

        int max = GetMaxEnergy();
        if (currentEnergy > max)
            currentEnergy = max;

        UpdateDisplay();
        GlobalSettings.LogGameplay($"EnergyManager: +{bonus} max energy (cap {max}, current {currentEnergy}).");
    }

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
        var settings = GlobalSettings.Instance;
        currentEnergy = settings != null ? settings.startingEnergy : 60;
        nextRegenTime = Time.time + GetEffectiveRegenInterval();
        UpdateUI();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // DDOL manager must rebind HUD references after Title -> main transitions.
        AutoAssignUI();
        UpdateUI();
    }

    public void SpawnFloatingEnergyText(int amount)
    {
        var hero = GameServices.Hero;
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
        var settings = GlobalSettings.Instance;
        if (settings == null)
            return;
        if (currentEnergy >= GetMaxEnergy())
            return;

        if (Time.time >= nextRegenTime)
        {
            AddEnergy(settings.energyRegenAmount);
            nextRegenTime = Time.time + GetEffectiveRegenInterval();
        }
    }

    private void UpdateDisplay()
    {
        // Main text (inside the box) now shows the actual energy
        if (energyText != null)
        {
            int max = GetMaxEnergy();
            energyText.text = $"{currentEnergy}/{max}";
        }

        // Small text (below the box) now shows the timer
        if (regenTimerText != null)
        {
            int max = GetMaxEnergy();
            if (currentEnergy >= max)
            {
                regenTimerText.text = "Energy Full";
            }
            else
            {
                float timeRemaining = nextRegenTime - Time.time;
                regenTimerText.text = $"More energy in {Mathf.CeilToInt(Mathf.Max(0f, timeRemaining))}s";
            }
        }
    }

    public bool HasEnergy(int amount)
    {
        return currentEnergy >= amount;
    }

    public void Deplete(int amount)
    {
        int maxEnergy = GetMaxEnergy();
        bool wasAtMax = currentEnergy >= maxEnergy;
        
        currentEnergy -= amount;
        if (currentEnergy < 0) currentEnergy = 0;
        
        if (wasAtMax && currentEnergy < maxEnergy)
        {
            nextRegenTime = Time.time + GetEffectiveRegenInterval();
        }

        UpdateUI();
        SpawnFloatingEnergyText(amount);
        GlobalSettings.LogGameplay($"EnergyManager: Depleted {amount}. Remaining: {currentEnergy}");
    }

    public void AddEnergy(int amount)
    {
        int maxEnergy = GetMaxEnergy();
        currentEnergy += amount;
        if (currentEnergy > maxEnergy)
            currentEnergy = maxEnergy;
        UpdateDisplay();
    }

    public void RestoreFull()
    {
        currentEnergy = GetMaxEnergy();
        nextRegenTime = Time.time + GetEffectiveRegenInterval();
        UpdateDisplay();
    }

    private void UpdateUI()
    {
        UpdateDisplay();
    }

    private static float GetEffectiveRegenInterval()
    {
        if (RogueLiteManager.Instance != null)
            return RogueLiteManager.Instance.GetEffectiveEnergyRegenInterval();

        var settings = GlobalSettings.Instance;
        return settings != null ? settings.energyRegenInterval : 15f;
    }

    [ContextMenu("Auto-Assign UI")]
    public void AutoAssignUI()
    {
        string[] energyPaths = {
            "MainUI_Canvas/Resources/HUD_Item_Energy/Energy/Text (TMP)",
            "MainUI_Canvas/HUD_Resources/HUD_Item_Energy/Energy/Text (TMP)"
        };
        foreach (var path in energyPaths)
        {
            GameObject go = GameObject.Find(path);
            if (go != null)
            {
                energyText = go.GetComponent<TextMeshProUGUI>();
                if (energyText != null) break;
            }
        }

        string[] timerPaths = {
            "MainUI_Canvas/Resources/HUD_Item_Energy/Energy/EnergyTotal_Text",
            "MainUI_Canvas/HUD_Resources/HUD_Item_Energy/Energy/EnergyTotal_Text"
        };
        foreach (var path in timerPaths)
        {
            GameObject totalGo = GameObject.Find(path);
            if (totalGo != null)
            {
                regenTimerText = totalGo.GetComponent<TextMeshProUGUI>();
                if (regenTimerText != null) break;
            }
        }
        
        UpdateDisplay();
    }
}
