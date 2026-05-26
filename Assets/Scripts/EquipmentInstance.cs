using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EquipmentInstance
{
    public EquipmentItemDefinition definition;
    public List<EquipmentStatBonus> statBonuses = new List<EquipmentStatBonus>();
    public int powerTier;

    public EquipmentInstance() { }

    public EquipmentInstance(EquipmentItemDefinition definition, List<EquipmentStatBonus> bonuses, int powerTier)
    {
        this.definition = definition;
        this.powerTier = powerTier;
        statBonuses = bonuses ?? new List<EquipmentStatBonus>();
    }

    public string DisplayName =>
        definition != null ? definition.displayName : "Unknown";

    public string BuildChoiceLabel()
    {
        string stats = EquipmentStatRoller.FormatBonusList(statBonuses);
        return string.IsNullOrEmpty(stats) ? DisplayName : $"{DisplayName}\n{stats}";
    }
}
