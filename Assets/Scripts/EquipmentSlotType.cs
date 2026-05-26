/// <summary>Equipment slot on the hero. Stat-only slots have no visual.</summary>
public enum EquipmentSlotType
{
    MainHand,
    OffHand,
    /// <summary>Base skull mesh (Head01_Male, etc.).</summary>
    HeadBase,
    Head = HeadBase,
    /// <summary>Eyes, eyebrows, glasses (Eye*, Eyebrow*, AC03_*).</summary>
    HeadEyes,
    /// <summary>Hats, helmets, hair, head armor (Hat*, HeadArmor*, Hair*).</summary>
    HeadHelmet,
    /// <summary>Mouth, mustaches, face decals (Mouth*, AC10_*, AC11_*).</summary>
    HeadFacial,
    BodyArmor,
    Cape,
    Ring,
    Necklace,
    Boots,
    Gloves
}

/// <summary>Which loot pool a chest button draws from.</summary>
public enum EquipmentChestCategory
{
    Weapon,
    Armor,
    Accessory
}

public enum EquipmentPrimaryStat
{
    Strength,
    Agility,
    Vitality,
    Luck
}
