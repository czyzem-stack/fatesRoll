using System.Collections.Generic;

/// <summary>
/// The eight player-facing equipment slots (paper doll + inventory rules).
/// Head cosmetics (eyes, facial, base skull) are defaults on Steve — not chest loot slots.
/// </summary>
public static class EquipmentSlots
{
    /// <summary>Eight slots chest loot can roll (catalog-backed gear).</summary>
    public static readonly EquipmentSlotType[] ChestLootSlots =
    {
        EquipmentSlotType.MainHand,
        EquipmentSlotType.BodyArmor,
        EquipmentSlotType.HeadHelmet,
        EquipmentSlotType.Cape,
        EquipmentSlotType.Ring,
        EquipmentSlotType.Necklace,
        EquipmentSlotType.Boots,
        EquipmentSlotType.Gloves
    };

    /// <summary>All slots the equipment panel equips (includes head cosmetics).</summary>
    public static readonly EquipmentSlotType[] PlayerSlots =
    {
        EquipmentSlotType.MainHand,
        EquipmentSlotType.OffHand,
        EquipmentSlotType.BodyArmor,
        EquipmentSlotType.HeadBase,
        EquipmentSlotType.HeadEyes,
        EquipmentSlotType.HeadHelmet,
        EquipmentSlotType.HeadFacial,
        EquipmentSlotType.Cape,
        EquipmentSlotType.Ring,
        EquipmentSlotType.Necklace,
        EquipmentSlotType.Boots,
        EquipmentSlotType.Gloves
    };

    /// <summary>
    /// Maps GUI Pro paper doll order: EquipSlot_R (4) then EquipSlot_L (4).
    /// </summary>
    public static readonly EquipmentSlotType[] PaperDollSlotOrder =
    {
        EquipmentSlotType.MainHand,
        EquipmentSlotType.Gloves,
        EquipmentSlotType.Ring,
        EquipmentSlotType.Boots,
        EquipmentSlotType.HeadHelmet,
        EquipmentSlotType.BodyArmor,
        EquipmentSlotType.Cape,
        EquipmentSlotType.Necklace
    };

    public static bool IsChestLootSlot(EquipmentSlotType slot)
    {
        foreach (var chestSlot in ChestLootSlots)
        {
            if (chestSlot == slot)
                return true;
        }

        return false;
    }

    public static bool IsPlayerSlot(EquipmentSlotType slot)
    {
        foreach (var playerSlot in PlayerSlots)
        {
            if (playerSlot == slot)
                return true;
        }

        return false;
    }

    public static string GetDisplayName(EquipmentSlotType slot) => slot switch
    {
        EquipmentSlotType.MainHand => "Weapon",
        EquipmentSlotType.BodyArmor => "Armor",
        EquipmentSlotType.HeadHelmet => "Head",
        EquipmentSlotType.Cape => "Cape",
        EquipmentSlotType.Ring => "Ring",
        EquipmentSlotType.Necklace => "Necklace",
        EquipmentSlotType.Boots => "Boots",
        EquipmentSlotType.Gloves => "Gloves",
        _ => slot.ToString()
    };

    public static EquipmentChestCategory GetDefaultChestCategory(EquipmentSlotType slot) => slot switch
    {
        EquipmentSlotType.MainHand => EquipmentChestCategory.Weapon,
        EquipmentSlotType.BodyArmor => EquipmentChestCategory.Armor,
        EquipmentSlotType.HeadHelmet => EquipmentChestCategory.Armor,
        EquipmentSlotType.Cape => EquipmentChestCategory.Armor,
        EquipmentSlotType.Ring => EquipmentChestCategory.Accessory,
        EquipmentSlotType.Necklace => EquipmentChestCategory.Accessory,
        EquipmentSlotType.Boots => EquipmentChestCategory.Accessory,
        EquipmentSlotType.Gloves => EquipmentChestCategory.Accessory,
        _ => EquipmentChestCategory.Accessory
    };
}
