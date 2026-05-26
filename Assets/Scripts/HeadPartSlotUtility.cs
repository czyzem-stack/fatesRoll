/// <summary>Maps RPG Tiny Hero head prefab names to equipment slots.</summary>
public static class HeadPartSlotUtility
{
    public static EquipmentSlotType ResolveSlot(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
            return EquipmentSlotType.HeadBase;

        string id = prefabName;

        if (id.StartsWith("Eye") || id.StartsWith("Eyebrow") || id.StartsWith("AC03_"))
            return EquipmentSlotType.HeadEyes;

        if (id.StartsWith("Mouth") ||
            id.StartsWith("AC10_") ||
            id.StartsWith("AC11_") ||
            id.StartsWith("AC01_") ||
            id.StartsWith("AC02_") ||
            id.StartsWith("AC05_") ||
            id.StartsWith("AC07_") ||
            id.StartsWith("AC09_"))
            return EquipmentSlotType.HeadFacial;

        if (id.StartsWith("Hat") ||
            id.StartsWith("Hair") ||
            id.StartsWith("HeadArmor") ||
            id.StartsWith("Head03_") ||
            id.StartsWith("Head04_") ||
            id.StartsWith("Head05_") ||
            id.StartsWith("AC04_") ||
            id.StartsWith("AC06_") ||
            id.StartsWith("AC08_") ||
            id.StartsWith("BackPack"))
            return EquipmentSlotType.HeadHelmet;

        if (id.StartsWith("Head01") || id.StartsWith("Head02"))
            return EquipmentSlotType.HeadBase;

        return EquipmentSlotType.HeadHelmet;
    }

    public static bool IsHeadSlot(EquipmentSlotType slot) =>
        slot == EquipmentSlotType.HeadBase ||
        slot == EquipmentSlotType.HeadEyes ||
        slot == EquipmentSlotType.HeadHelmet ||
        slot == EquipmentSlotType.HeadFacial;
}
