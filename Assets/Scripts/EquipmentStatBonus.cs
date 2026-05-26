using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct EquipmentStatBonus
{
    public EquipmentPrimaryStat stat;
    public float amount;

    public EquipmentStatBonus(EquipmentPrimaryStat stat, float amount)
    {
        this.stat = stat;
        this.amount = amount;
    }

    public string FormatShort()
    {
        string abbr = stat switch
        {
            EquipmentPrimaryStat.Strength => "STR",
            EquipmentPrimaryStat.Agility => "AGI",
            EquipmentPrimaryStat.Vitality => "VIT",
            EquipmentPrimaryStat.Luck => "LUCK",
            _ => stat.ToString()
        };
        return $"+{amount:0.#} {abbr}";
    }
}

public static class EquipmentStatRoller
{
    /// <summary>Pick two distinct stats from STR/AGI/VIT/LUCK with scaled amounts.</summary>
    public static List<EquipmentStatBonus> RollTwoOfFour(float bonusPerStat)
    {
        var stats = new List<EquipmentPrimaryStat>
        {
            EquipmentPrimaryStat.Strength,
            EquipmentPrimaryStat.Agility,
            EquipmentPrimaryStat.Vitality,
            EquipmentPrimaryStat.Luck
        };

        int first = UnityEngine.Random.Range(0, stats.Count);
        var firstStat = stats[first];
        stats.RemoveAt(first);
        int second = UnityEngine.Random.Range(0, stats.Count);

        float amount = Mathf.Max(1f, Mathf.Round(bonusPerStat));
        return new List<EquipmentStatBonus>
        {
            new EquipmentStatBonus(firstStat, amount),
            new EquipmentStatBonus(stats[second], amount)
        };
    }

    public static string FormatBonusList(IReadOnlyList<EquipmentStatBonus> bonuses)
    {
        if (bonuses == null || bonuses.Count == 0)
            return string.Empty;

        var parts = new List<string>(bonuses.Count);
        foreach (var b in bonuses)
            parts.Add(b.FormatShort());
        return string.Join(", ", parts);
    }
}
