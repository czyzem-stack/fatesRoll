using System;
using UnityEngine;

/// <summary>Per-slot stat tier tracking — generation uses tier as the +N value for each stat line.</summary>
[Serializable]
public class EquipmentSlotStatProgression
{
    public EquipmentSlotType slot;
    public int strengthTier = 1;
    public int agilityTier = 1;
    public int vitalityTier = 1;
    public int luckTier = 1;

    public int GetTier(EquipmentPrimaryStat stat) => stat switch
    {
        EquipmentPrimaryStat.Strength => strengthTier,
        EquipmentPrimaryStat.Agility => agilityTier,
        EquipmentPrimaryStat.Vitality => vitalityTier,
        EquipmentPrimaryStat.Luck => luckTier,
        _ => 1
    };

    public void AdvanceTier(EquipmentPrimaryStat stat, int increment, int maxTier)
    {
        switch (stat)
        {
            case EquipmentPrimaryStat.Strength:
                strengthTier = Mathf.Clamp(strengthTier + increment, 1, maxTier);
                break;
            case EquipmentPrimaryStat.Agility:
                agilityTier = Mathf.Clamp(agilityTier + increment, 1, maxTier);
                break;
            case EquipmentPrimaryStat.Vitality:
                vitalityTier = Mathf.Clamp(vitalityTier + increment, 1, maxTier);
                break;
            case EquipmentPrimaryStat.Luck:
                luckTier = Mathf.Clamp(luckTier + increment, 1, maxTier);
                break;
        }
    }
}
