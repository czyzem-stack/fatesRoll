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

    public static float GetPowerScore(IReadOnlyList<EquipmentStatBonus> bonuses)
    {
        if (bonuses == null || bonuses.Count == 0)
            return 0f;

        float sum = 0f;
        foreach (var b in bonuses)
            sum += b.amount;
        return sum;
    }

    /// <summary>
    /// Rolls two stat lines that are strictly better than what Steve wears in that slot
    /// (e.g. equipped +1/+1 sword → at least +2/+1).
    /// </summary>
    public static List<EquipmentStatBonus> RollTwoOfFourUpgradeFrom(
        EquipmentInstance equippedInSlot,
        float bonusPerStat)
    {
        var rolled = RollTwoOfFour(bonusPerStat);
        if (equippedInSlot?.statBonuses == null || equippedInSlot.statBonuses.Count == 0)
            return rolled;

        float baseAmount = Mathf.Max(1f, Mathf.Round(bonusPerStat));
        float equippedScore = GetPowerScore(equippedInSlot.statBonuses);
        var result = new List<EquipmentStatBonus>();

        int copyCount = Mathf.Min(2, equippedInSlot.statBonuses.Count);
        for (int i = 0; i < copyCount; i++)
        {
            var b = equippedInSlot.statBonuses[i];
            result.Add(new EquipmentStatBonus(b.stat, Mathf.Max(b.amount, baseAmount)));
        }

        if (result.Count < 2)
        {
            foreach (var extra in rolled)
            {
                if (IndexOfStat(result, extra.stat) < 0)
                    result.Add(new EquipmentStatBonus(extra.stat, Mathf.Max(extra.amount, baseAmount)));
                if (result.Count >= 2)
                    break;
            }
        }

        while (result.Count < 2)
        {
            var fill = RollTwoOfFour(bonusPerStat);
            foreach (var extra in fill)
            {
                if (IndexOfStat(result, extra.stat) < 0)
                {
                    result.Add(new EquipmentStatBonus(extra.stat, Mathf.Max(extra.amount, baseAmount)));
                    break;
                }
            }
        }

        BumpHighestStat(result, 1f);

        int guard = 0;
        while (GetPowerScore(result) <= equippedScore && guard++ < 8)
            BumpHighestStat(result, 1f);

        return result;
    }

    private static int IndexOfStat(List<EquipmentStatBonus> list, EquipmentPrimaryStat stat)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].stat == stat)
                return i;
        }

        return -1;
    }

    private static void BumpHighestStat(List<EquipmentStatBonus> bonuses, float delta)
    {
        if (bonuses == null || bonuses.Count == 0)
            return;

        int idx = 0;
        float max = bonuses[0].amount;
        for (int i = 1; i < bonuses.Count; i++)
        {
            if (bonuses[i].amount > max)
            {
                max = bonuses[i].amount;
                idx = i;
            }
        }

        var b = bonuses[idx];
        bonuses[idx] = new EquipmentStatBonus(b.stat, b.amount + delta);
    }
}
