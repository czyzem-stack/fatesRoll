using UnityEngine;
using System.Collections.Generic;

public class TalentManager : MonoBehaviour
{
    public static TalentManager Instance { get; private set; }

    [Header("Cost Settings")]
    public int baseCost = 25;
    public int costIncrease = 10;
    public int currentUpgradeLevel = 0;

    [Header("Bonus Values")]
    public float healthBonus = 50f;
    public float primaryStatBonus = 2f; // Str, Agi, Vit, Luck
    public float dodgeBonus = 1f;
    public float critChanceBonus = 1f;
    public float critDamageBonus = 5f;
    public int energyMaxBonus = 5;
    public int coinGainBonus = 1;

    // Track total bonuses applied
    public Dictionary<int, float> totalBonuses = new Dictionary<int, float>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        for (int i = 0; i < 10; i++) totalBonuses[i] = 0;
    }

    public int GetCurrentCost()
    {
        return baseCost + (currentUpgradeLevel * costIncrease);
    }

    public bool CanAffordUpgrade()
    {
        return LootManager.Instance != null && LootManager.Instance.CurrentGold >= GetCurrentCost();
    }

    public int PerformUpgrade()
    {
        int cost = GetCurrentCost();
        if (LootManager.Instance == null || !LootManager.Instance.TrySpendGold(cost))
            return -1;

        int categoryIndex = Random.Range(0, 10);
        ApplyBonus(categoryIndex);
        currentUpgradeLevel++;
        
        return categoryIndex;
    }

    private void ApplyBonus(int index)
    {
        var stats = GameServices.Hero != null ? GameServices.Hero.GetComponent<PlayerStats>() : null;
        
        switch (index)
        {
            case 0: // Health
                if (stats != null) stats.talentMaxHP += healthBonus;
                totalBonuses[0] += healthBonus;
                break;
            case 1: // Strength
                if (stats != null) stats.talentStrength += primaryStatBonus;
                totalBonuses[1] += primaryStatBonus;
                break;
            case 2: // Agility
                if (stats != null) stats.talentAgility += primaryStatBonus;
                totalBonuses[2] += primaryStatBonus;
                break;
            case 3: // Vitality
                if (stats != null) stats.talentVitality += primaryStatBonus;
                totalBonuses[3] += primaryStatBonus;
                break;
            case 4: // Luck
                if (stats != null) stats.talentLuck += primaryStatBonus;
                totalBonuses[4] += primaryStatBonus;
                break;
            case 5: // Dodge
                if (stats != null) stats.talentDodge += dodgeBonus;
                totalBonuses[5] += dodgeBonus;
                break;
            case 6: // Crit Damage
                if (stats != null) stats.talentCritDamage += critDamageBonus;
                totalBonuses[6] += critDamageBonus;
                break;
            case 7: // Crit Chance
                if (stats != null) stats.talentCritChance += critChanceBonus;
                totalBonuses[7] += critChanceBonus;
                break;
            case 8: // Energy
                // Need a way to increase max energy in GlobalSettings or EnergyManager
                // For now just track it
                totalBonuses[8] += energyMaxBonus;
                break;
            case 9: // Coin
                if (LootManager.Instance != null) LootManager.Instance.goldPerCoin += coinGainBonus;
                totalBonuses[9] += coinGainBonus;
                break;
        }

        if (stats != null) stats.CalculateAllDerivedStats();
    }

    public float GetTotalBonus(int index)
    {
        return totalBonuses.ContainsKey(index) ? totalBonuses[index] : 0f;
    }
}
