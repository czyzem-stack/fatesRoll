using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FatesRoll/Equipment Catalog", fileName = "EquipmentCatalog")]
public class EquipmentCatalog : ScriptableObject
{
    public List<EquipmentItemDefinition> items = new List<EquipmentItemDefinition>();

    public List<EquipmentItemDefinition> GetByCategory(EquipmentChestCategory category)
    {
        var list = new List<EquipmentItemDefinition>();
        foreach (var item in items)
        {
            if (item != null && item.chestCategory == category)
                list.Add(item);
        }
        return list;
    }

    public List<EquipmentItemDefinition> GetBySlot(EquipmentSlotType slot)
    {
        var list = new List<EquipmentItemDefinition>();
        foreach (var item in items)
        {
            if (item != null && item.slot == slot)
                list.Add(item);
        }
        return list;
    }

    public EquipmentItemDefinition FindById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        foreach (var item in items)
        {
            if (item != null && item.itemId == id)
                return item;
        }

        return null;
    }
}
