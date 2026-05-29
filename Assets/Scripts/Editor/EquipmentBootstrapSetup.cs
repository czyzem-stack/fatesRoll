#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EquipmentBootstrapSetup
{
    const string BootstrapPath = BootstrapSceneSetup.BootstrapScenePath;

    [MenuItem("FatesRoll/Equipment/Copy Chest Loot UI Prefabs To Resources")]
    public static void CopyChestLootUiPrefabsToResources()
    {
        Directory.CreateDirectory("Assets/Resources/UI");

        CopyPrefab(
            "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_Component_Popups/Popup_01_Basic_Demo.prefab",
            "Assets/Resources/UI/ChestLootPopup.prefab");
        CopyPrefab(
            "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_Component_Frames/ItemFrame_03_Green.prefab",
            "Assets/Resources/UI/ItemFrame_03_Green.prefab");
        CopyPrefab(
            "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_Component_Buttons/Button_Rectangle_01_Convex_Green.prefab",
            "Assets/Resources/UI/Button_Rectangle_01_Convex_Green.prefab");

        AssetDatabase.SaveAssets();
        Debug.Log("EquipmentBootstrapSetup: chest loot UI prefabs copied to Assets/Resources/UI/.");
    }

    static void CopyPrefab(string sourcePath, string destPath)
    {
        if (!File.Exists(sourcePath))
        {
            Debug.LogWarning($"CopyChestLootUiPrefabs: missing {sourcePath}");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) == null)
            AssetDatabase.CopyAsset(sourcePath, destPath);
        else
            File.Copy(sourcePath, destPath, true);
    }

    [MenuItem("FatesRoll/Equipment/Validate Eight Slot Catalog")]
    public static void ValidateEightSlotCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>("Assets/Data/Equipment/EquipmentCatalog.asset");
        if (catalog == null)
        {
            Debug.LogError("ValidateEightSlotCatalog: missing EquipmentCatalog.asset");
            return;
        }

        int missing = 0;
        foreach (var slot in EquipmentSlots.ChestLootSlots)
        {
            int count = catalog.GetBySlot(slot).Count;
            if (count == 0)
            {
                Debug.LogWarning($"Catalog: no items for {EquipmentSlots.GetDisplayName(slot)} ({slot}).");
                missing++;
            }
            else
            {
                Debug.Log($"Catalog: {EquipmentSlots.GetDisplayName(slot)} — {count} item(s).");
            }
        }

        if (missing == 0)
            Debug.Log("ValidateEightSlotCatalog: all eight player slots have catalog entries.");
    }

    [MenuItem("FatesRoll/Equipment/Add EquipmentManager To Bootstrap")]
    public static void AddEquipmentManagerToBootstrap()
    {
        if (!System.IO.File.Exists(BootstrapPath))
        {
            Debug.LogError($"EquipmentBootstrapSetup: missing {BootstrapPath}.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
        Transform services = FindGameServicesRoot();
        if (services == null)
        {
            Debug.LogError("EquipmentBootstrapSetup: no GameServices root in Bootstrap.unity.");
            return;
        }

        EquipmentManager manager = services.GetComponentInChildren<EquipmentManager>(true);
        if (manager == null)
        {
            var go = new GameObject("EquipmentManager");
            go.transform.SetParent(services, false);
            manager = go.AddComponent<EquipmentManager>();
        }

        manager.EnsureReferences();
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = manager.gameObject;
        Debug.Log("EquipmentBootstrapSetup: EquipmentManager added/verified on bootstrap.");
    }

    static Transform FindGameServicesRoot()
    {
        var services = GameObject.Find("GameServices");
        if (services != null)
            return services.transform;

        foreach (var gs in Object.FindObjectsByType<GameServices>(FindObjectsInactive.Include))
        {
            if (gs != null)
                return gs.transform;
        }

        return null;
    }
}
#endif
