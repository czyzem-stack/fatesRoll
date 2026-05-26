#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EquipmentLootManagerSetup
{
    private const string CatalogPath = "Assets/Data/Equipment/EquipmentCatalog.asset";
    private const string CatalogResourcesPath = "Assets/Resources/Equipment/EquipmentCatalog.asset";

    [MenuItem("FatesRoll/Equipment/Fix Equipment Loot Manager In Scene")]
    public static void FixEquipmentLootManagerInScene()
    {
        EnsureCatalogResourcesCopy();

        var catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"Catalog missing at {CatalogPath}. Run Build Catalog From Heroes Pack first.");
            return;
        }

        var manager = Object.FindAnyObjectByType<EquipmentLootManager>();
        if (manager == null)
        {
            AddToScene();
            manager = Object.FindAnyObjectByType<EquipmentLootManager>();
        }

        if (manager == null)
            return;

        var so = new SerializedObject(manager);
        so.FindProperty("catalog").objectReferenceValue = catalog;
        so.ApplyModifiedPropertiesWithoutUndo();
        manager.EditorAssignDefaultButtonPrefabs();
        manager.EnsureReferences();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = manager.gameObject;
        Debug.Log("EquipmentLootManager: catalog and button prefabs assigned. Save scene (Ctrl+S).");
    }

    [MenuItem("FatesRoll/Equipment/Add Equipment Loot Manager To Scene")]
    public static void AddToScene()
    {
        var existing = Object.FindAnyObjectByType<EquipmentLootManager>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("EquipmentLootManager already in scene.");
            return;
        }

        var catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning("No catalog at " + CatalogPath + " — run FatesRoll → Equipment → Build Catalog From Heroes Pack first.");
        }

        var go = new GameObject("EquipmentLootManager");
        var manager = go.AddComponent<EquipmentLootManager>();
        manager.EditorAssignDefaultButtonPrefabs();

        var so = new SerializedObject(manager);
        so.FindProperty("catalog").objectReferenceValue = catalog;
        so.ApplyModifiedPropertiesWithoutUndo();

        Transform parent = GetManagersRoot(createIfMissing: true);
        if (parent != null)
            go.transform.SetParent(parent, false);

        Undo.RegisterCreatedObjectUndo(go, "Add EquipmentLootManager");
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("EquipmentLootManager added. Assign catalog if needed, then save scene.");
    }

    [MenuItem("FatesRoll/Equipment/Create Treasure Chest POI At Scene View Pivot")]
    public static void CreateTreasureChestPoi()
    {
        var poiGo = new GameObject("POI_TreasureChest");
        var node = poiGo.AddComponent<POINode>();
        node.type = POIType.TreasureChest;
        node.isTreasureChest = true;
        node.order = 0;

        poiGo.transform.position = GetGroundPositionForNewPoi();

        POINodeEditor.RefreshVisuals(node);

        Undo.RegisterCreatedObjectUndo(poiGo, "Create Treasure Chest POI");
        Selection.activeGameObject = poiGo;
        EditorSceneManager.MarkSceneDirty(poiGo.scene);
        Debug.Log("Treasure chest POI created. Set Visit Order and FTUE options, then save scene.");
    }

    [MenuItem("FatesRoll/Equipment/Fix Hero Animator Stance")]
    public static void FixHeroAnimatorStance()
    {
        var hero = Object.FindAnyObjectByType<HeroController>();
        if (hero == null)
        {
            Debug.LogError("No HeroController in scene.");
            return;
        }

        var stance = hero.GetComponent<HeroWeaponStance>();
        if (stance == null)
            stance = hero.gameObject.AddComponent<HeroWeaponStance>();
        stance.EditorLoadControllers();
        stance.Initialize();
        hero.ApplyVisualLocomotionAlignment();
        EditorUtility.SetDirty(hero.gameObject);
        EditorSceneManager.MarkSceneDirty(hero.gameObject.scene);
        Debug.Log("Hero animator: yaw fix + weapon stance controllers applied. Save scene.");
    }

    [MenuItem("FatesRoll/Equipment/Setup Steve (MC02 Base + Hero Equipment)")]
    public static void SetupSteveEquipment()
    {
        var hero = Object.FindAnyObjectByType<HeroController>();
        if (hero == null)
        {
            Debug.LogError("No HeroController in scene.");
            return;
        }

        if (hero.GetComponent<HeroEquipment>() == null)
            hero.gameObject.AddComponent<HeroEquipment>();

        var stance = hero.GetComponent<HeroWeaponStance>();
        if (stance == null)
            stance = hero.gameObject.AddComponent<HeroWeaponStance>();
        stance.EditorLoadControllers();
        stance.Initialize();

        StripMc02ToBase(hero);
        EditorUtility.SetDirty(hero.gameObject);
        EditorSceneManager.MarkSceneDirty(hero.gameObject.scene);
        Debug.Log("Steve: HeroEquipment added; MC02 stripped to Body01, cloaks/weapons hidden.");
    }

    private static void StripMc02ToBase(HeroController hero)
    {
        Transform rig = null;
        foreach (var anim in hero.GetComponentsInChildren<Animator>(true))
        {
            if (anim.gameObject.name.Contains("MC02") || anim.gameObject.name.Contains("Steve_Base"))
            {
                rig = anim.transform;
                break;
            }
        }

        if (rig == null)
        {
            Debug.LogWarning("Could not find MC02 rig under Steve — swap Steve visual to Assets/Heroes/Prefab/ModularCharacters/MC02.prefab first.");
            return;
        }

        var equip = hero.GetComponent<HeroEquipment>();
        equip.EditorSetRigRoot(rig);

        foreach (var t in rig.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Body") && t.name.Length <= 7)
                t.gameObject.SetActive(t.name == "Body01");
            else if (t.name.StartsWith("Cloak") && !t.name.Contains("Bone"))
                t.gameObject.SetActive(false);
        }

        var weaponR = FindBone(rig, "weapon_r");
        var weaponL = FindBone(rig, "weapon_l");
        ClearSocketChildren(weaponR);
        ClearSocketChildren(weaponL);

        var rigAnimator = rig.GetComponent<Animator>();
        if (rigAnimator != null)
        {
            rigAnimator.applyRootMotion = false;
            bool yawFixed = false;
            HeroLocomotionUtility.AlignVisualToAgentForward(hero.transform, rigAnimator, ref yawFixed);
        }
    }

    private static Transform FindBone(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }

        return null;
    }

    private static void ClearSocketChildren(Transform socket)
    {
        if (socket == null)
            return;
        for (int i = socket.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(socket.GetChild(i).gameObject);
    }

    private static Vector3 GetGroundPositionForNewPoi()
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null)
            return Vector3.zero;

        Camera cam = view.camera;
        if (cam != null)
        {
            Vector3 probe = cam.transform.position + cam.transform.forward * 8f;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 200f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                return hit.point;
        }

        Vector3 pivot = view.pivot;
        if (Physics.Raycast(pivot + Vector3.up * 40f, Vector3.down, out RaycastHit pivotHit, 120f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return pivotHit.point;

        return pivot;
    }

    private static void EnsureCatalogResourcesCopy()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Equipment"))
            AssetDatabase.CreateFolder("Assets/Resources", "Equipment");

        if (AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(CatalogResourcesPath) != null)
            return;

        if (AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(CatalogPath) == null)
            return;

        AssetDatabase.CopyAsset(CatalogPath, CatalogResourcesPath);
        AssetDatabase.SaveAssets();
    }

    private static Transform GetManagersRoot(bool createIfMissing)
    {
        string[] names = { "Managers", "GameManagers", "_Managers" };
        foreach (string name in names)
        {
            var found = GameObject.Find(name);
            if (found != null)
                return found.transform;
        }

        if (!createIfMissing)
            return null;

        return new GameObject("Managers").transform;
    }
}
#endif
