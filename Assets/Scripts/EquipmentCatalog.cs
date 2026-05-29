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

    public List<EquipmentItemDefinition> GetBySlot(EquipmentSlotType slot, bool playerSlotsOnly = false)
    {
        var list = new List<EquipmentItemDefinition>();
        foreach (var item in items)
        {
            if (item == null)
                continue;
            if (item.slot != slot)
                continue;
            if (playerSlotsOnly && !EquipmentSlots.IsPlayerSlot(item.slot))
                continue;
            list.Add(item);
        }

        return list;
    }

    public EquipmentItemDefinition GetRandomBySlot(EquipmentSlotType slot)
    {
        var pool = GetBySlot(slot);
        if (pool == null || pool.Count == 0)
            return null;
        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    public bool HasAnyForPlayerSlots()
    {
        foreach (var slot in EquipmentSlots.PlayerSlots)
        {
            if (GetBySlot(slot).Count > 0)
                return true;
        }

        return false;
    }
}
