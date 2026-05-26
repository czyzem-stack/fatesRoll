#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EquipmentCatalogBuilder
{
    private const string CatalogPath = "Assets/Data/Equipment/EquipmentCatalog.asset";
    private const string ItemsFolder = "Assets/Data/Equipment/Items";
    private const string HeroesWeapons = "Assets/Heroes/Prefab/Weapons";
    private const string HeroesHead = "Assets/Heroes/Prefab/HeadParts";
    private const string Mc02Path = "Assets/Heroes/Prefab/ModularCharacters/MC02.prefab";

    [MenuItem("FatesRoll/Equipment/Build Catalog From Heroes Pack")]
    public static void BuildCatalog()
    {
        EnsureFolder(ItemsFolder);
        EnsureFolder(Path.GetDirectoryName(CatalogPath));

        var catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<EquipmentCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.items.Clear();
        int created = 0;

        created += ScanWeapons(catalog);
        created += ScanShields(catalog);
        created += ScanHeadParts(catalog);
        created += ScanBodyArmorFromMc02(catalog);
        created += ScanCloaksFromMc02(catalog);
        created += CreateStatOnlyAccessories(catalog);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Equipment catalog built: {catalog.items.Count} items ({created} new SO assets). Path: {CatalogPath}");
    }

    private static int ScanWeapons(EquipmentCatalog catalog)
    {
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { HeroesWeapons }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);
            if (file.StartsWith("Shield"))
                continue;
            if (file == "Bows" || file == "Arrows")
                continue;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var slot = file.StartsWith("THS") ? EquipmentSlotType.MainHand : EquipmentSlotType.MainHand;
            var item = GetOrCreateItem(file, file.Replace('_', ' '), slot, EquipmentChestCategory.Weapon, prefab, null, false);
            catalog.items.Add(item);
            n++;
        }

        return n;
    }

    private static int ScanShields(EquipmentCatalog catalog)
    {
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("Shield t:Prefab", new[] { HeroesWeapons }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var item = GetOrCreateItem(file, file, EquipmentSlotType.OffHand, EquipmentChestCategory.Weapon, prefab, null, false);
            catalog.items.Add(item);
            n++;
        }

        return n;
    }

    private static int ScanHeadParts(EquipmentCatalog catalog)
    {
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { HeroesHead }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var item = GetOrCreateItem(file, file.Replace('_', ' '), EquipmentSlotType.Head, EquipmentChestCategory.Armor, prefab, null, false);
            catalog.items.Add(item);
            n++;
        }

        return n;
    }

    private static int ScanBodyArmorFromMc02(EquipmentCatalog catalog)
    {
        int n = 0;
        var mc02 = AssetDatabase.LoadAssetAtPath<GameObject>(Mc02Path);
        if (mc02 == null)
            return 0;

        foreach (var t in mc02.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Body") || t.name.Length > 7)
                continue;

            string id = t.name;
            string display = $"Armor {id}";
            var item = GetOrCreateItem(id, display, EquipmentSlotType.BodyArmor, EquipmentChestCategory.Armor, null, id, true);
            catalog.items.Add(item);
            n++;
        }

        return n;
    }

    private static int ScanCloaksFromMc02(EquipmentCatalog catalog)
    {
        int n = 0;
        var mc02 = AssetDatabase.LoadAssetAtPath<GameObject>(Mc02Path);
        if (mc02 == null)
            return 0;

        foreach (var t in mc02.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Cloak") || t.name.Contains("Bone"))
                continue;
            if (t.name.Length > 7)
                continue;

            string id = t.name;
            var item = GetOrCreateItem(id, id, EquipmentSlotType.Cape, EquipmentChestCategory.Armor, null, id, true);
            catalog.items.Add(item);
            n++;
        }

        return n;
    }

    private static int CreateStatOnlyAccessories(EquipmentCatalog catalog)
    {
        int n = 0;
        n += AddAccessory(catalog, "ring_iron", "Iron Ring", EquipmentSlotType.Ring);
        n += AddAccessory(catalog, "ring_gold", "Gold Ring", EquipmentSlotType.Ring);
        n += AddAccessory(catalog, "necklace_jade", "Jade Necklace", EquipmentSlotType.Necklace);
        n += AddAccessory(catalog, "necklace_ruby", "Ruby Necklace", EquipmentSlotType.Necklace);
        n += AddAccessory(catalog, "boots_leather", "Leather Boots", EquipmentSlotType.Boots);
        n += AddAccessory(catalog, "boots_plate", "Plate Boots", EquipmentSlotType.Boots);
        n += AddAccessory(catalog, "gloves_leather", "Leather Gloves", EquipmentSlotType.Gloves);
        n += AddAccessory(catalog, "gloves_plate", "Plate Gauntlets", EquipmentSlotType.Gloves);
        return n;
    }

    private static int AddAccessory(EquipmentCatalog catalog, string id, string display, EquipmentSlotType slot)
    {
        var item = GetOrCreateItem(id, display, slot, EquipmentChestCategory.Accessory, null, null, false);
        catalog.items.Add(item);
        return 1;
    }

    private static EquipmentItemDefinition GetOrCreateItem(
        string id,
        string displayName,
        EquipmentSlotType slot,
        EquipmentChestCategory category,
        GameObject visualPrefab,
        string rigChildName,
        bool useRigToggle)
    {
        string assetPath = $"{ItemsFolder}/{id}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>(assetPath);
        if (existing != null)
        {
            existing.itemId = id;
            existing.displayName = displayName;
            existing.slot = slot;
            existing.chestCategory = category;
            existing.visualPrefab = visualPrefab;
            existing.rigChildName = rigChildName ?? string.Empty;
            existing.useRigChildToggle = useRigToggle;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var item = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
        item.itemId = id;
        item.displayName = displayName;
        item.slot = slot;
        item.chestCategory = category;
        item.visualPrefab = visualPrefab;
        item.rigChildName = rigChildName ?? string.Empty;
        item.useRigChildToggle = useRigToggle;
        AssetDatabase.CreateAsset(item, assetPath);
        return item;
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        path = path.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folder);
    }
}
#endif
