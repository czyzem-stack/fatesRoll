using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EquipmentInstance
{
    public string instanceId;
    public EquipmentItemDefinition definition;
    public List<EquipmentStatBonus> statBonuses = new List<EquipmentStatBonus>();
    public int powerTier;
    public string iconKey;

    [NonSerialized] public Sprite runtimeIcon;

    public EquipmentInstance() { }

    public EquipmentInstance(EquipmentItemDefinition definition, List<EquipmentStatBonus> bonuses, int powerTier)
    {
        this.definition = definition;
        this.powerTier = powerTier;
        statBonuses = bonuses ?? new List<EquipmentStatBonus>();
        instanceId = Guid.NewGuid().ToString("N");
    }

    public EquipmentInstance(
        EquipmentItemDefinition definition,
        List<EquipmentStatBonus> bonuses,
        int powerTier,
        Sprite icon)
    {
        this.definition = definition;
        this.powerTier = powerTier;
        statBonuses = bonuses ?? new List<EquipmentStatBonus>();
        instanceId = Guid.NewGuid().ToString("N");
        runtimeIcon = icon;
        iconKey = icon != null ? icon.name : string.Empty;
    }

    public void EnsureInstanceId()
    {
        if (string.IsNullOrEmpty(instanceId))
            instanceId = Guid.NewGuid().ToString("N");
    }

    public string DisplayName =>
        definition != null ? definition.displayName : "Unknown";

    public string SlotDisplayName =>
        definition != null ? EquipmentSlots.GetDisplayName(definition.slot) : string.Empty;

    public string GetStatLine(int index)
    {
        if (statBonuses == null || index < 0 || index >= statBonuses.Count)
            return string.Empty;
        return statBonuses[index].FormatShort();
    }

    public string BuildChoiceLabel()
    {
        string stats = EquipmentStatRoller.FormatBonusList(statBonuses);
        return string.IsNullOrEmpty(stats) ? DisplayName : $"{DisplayName}\n{stats}";
    }

    /// <summary>Multi-line chest popup body: slot, name, stat lines.</summary>
    public string BuildChestDetailText()
    {
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(SlotDisplayName))
            lines.Add(SlotDisplayName);
        lines.Add(DisplayName);
        if (statBonuses != null)
        {
            foreach (var bonus in statBonuses)
                lines.Add(bonus.FormatShort());
        }

        return string.Join("\n", lines);
    }
}
