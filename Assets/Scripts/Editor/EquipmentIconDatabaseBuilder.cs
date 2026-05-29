#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EquipmentIconDatabaseBuilder
{
    const string DatabasePath = "Assets/Data/Equipment/EquipmentIconDatabase.asset";
    const string ResourcesDatabasePath = "Assets/Resources/Equipment/EquipmentIconDatabase.asset";
    const string IconsRoot = "Assets/5000FantasyIcons";

    [MenuItem("FatesRoll/Equipment/Build Icon Database From 5000FantasyIcons")]
    public static void BuildIconDatabase()
    {
        Directory.CreateDirectory("Assets/Data/Equipment");
        Directory.CreateDirectory("Assets/Resources/Equipment");

        var database = AssetDatabase.LoadAssetAtPath<EquipmentIconDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<EquipmentIconDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        database.SetIconsForSlot(EquipmentSlotType.MainHand, LoadSpritesFromFolder($"{IconsRoot}/WeaponIcons/WeaponIconsVol2", "Sword", "Axe", "Staff", "Wand", "Spear", "Bow", "Hammer", "Dagger"));
        database.SetIconsForSlot(EquipmentSlotType.BodyArmor, LoadSpritesFromFolder($"{IconsRoot}/ArmorIcons/ArmorSet_Icons/Cloth", "Chest", "Body"));
        database.SetIconsForSlot(EquipmentSlotType.HeadHelmet, LoadSpritesFromFolder($"{IconsRoot}/ArmorIcons/ArmorSet_Icons/HatSpecial", "Hat", "Helm", "Head"));
        database.SetIconsForSlot(EquipmentSlotType.Cape, LoadSpritesFromFolder($"{IconsRoot}/ArmorIcons/ArmorSet_Icons/Cloak", "cloak", "Cloak"));
        database.SetIconsForSlot(EquipmentSlotType.Ring, LoadSpritesFromFolder($"{IconsRoot}/ArmorIcons/RingAndNeck_Icons", "Ring"));
        database.SetIconsForSlot(EquipmentSlotType.Necklace, LoadSpritesFromFolder($"{IconsRoot}/ArmorIcons/RingAndNeck_Icons", "Neck", "Amulet"));
        database.SetIconsForSlot(EquipmentSlotType.Boots, LoadSpritesFromFolder($"{IconsRoot}/ArmorIcons/ArmorSet_Icons/Cloth", "Boots", "boots"));
        database.SetIconsForSlot(EquipmentSlotType.Gloves, LoadSpritesFromFolder($"{IconsRoot}/ArmorIcons/ArmorSet_Icons/Cloth", "gloves", "Gloves"));

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        var resourcesCopy = AssetDatabase.LoadAssetAtPath<EquipmentIconDatabase>(ResourcesDatabasePath);
        if (resourcesCopy == null)
        {
            AssetDatabase.CopyAsset(DatabasePath, ResourcesDatabasePath);
        }
        else
        {
            resourcesCopy.iconsBySlot = database.iconsBySlot;
            EditorUtility.SetDirty(resourcesCopy);
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"EquipmentIconDatabaseBuilder: saved {DatabasePath} and synced Resources copy.");
    }

    static Sprite[] LoadSpritesFromFolder(string folderPath, params string[] nameContainsFilters)
    {
        var sprites = new List<Sprite>();
        if (!Directory.Exists(folderPath))
            return sprites.ToArray();

        foreach (string file in Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories))
        {
            string assetPath = file.Replace('\\', '/');
            if (assetPath.StartsWith(Application.dataPath))
                assetPath = "Assets" + assetPath.Substring(Application.dataPath.Length);

            if (nameContainsFilters != null && nameContainsFilters.Length > 0)
            {
                bool match = false;
                foreach (string filter in nameContainsFilters)
                {
                    if (string.IsNullOrEmpty(filter) ||
                        assetPath.Contains(filter, System.StringComparison.OrdinalIgnoreCase))
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                    continue;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                sprites.Add(sprite);
        }

        return sprites.ToArray();
    }
}
#endif
