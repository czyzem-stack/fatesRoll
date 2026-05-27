#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Restores Steve into main.unity from the last committed scene (or builds from MC02).</summary>
public static class MainSceneSteveRestore
{
    public const string MainScenePath = MainSceneBootstrapCleanup.MainScenePath;
    public const string StevePrefabPath = "Assets/Heroes/Prefab/Steve.prefab";
    public const string Mc02PrefabPath = "Assets/Heroes/Prefab/ModularCharacters/MC02.prefab";
    public const string HeroAnimatorPath = "Assets/Heroes/Animator/SwordAndShieldStance.controller";

    const string TempGitScenePath = MainSceneGitRestoreUtility.TempGitScenePath;

    [MenuItem("FatesRoll/Setup/Restore Steve In Main Scene")]
    public static void RestoreSteveMenu()
    {
        RestoreSteve(saveScene: true);
    }

    /// <summary>Batchmode: Unity -executeMethod MainSceneSteveRestore.RestoreSteveBatch</summary>
    public static void RestoreSteveBatch()
    {
        RestoreSteve(saveScene: true);
    }

    public static bool RestoreSteve(bool saveScene = true)
    {
        if (!File.Exists(MainScenePath))
        {
            UnityEngine.Debug.LogError($"MainSceneSteveRestore: missing {MainScenePath}.");
            return false;
        }

        Scene main = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        if (FindSteveInScene(main) != null)
        {
            UnityEngine.Debug.Log("MainSceneSteveRestore: Steve already present in main.unity.");
            WireSpawnManagerHeroPrefab();
            MainSceneDiceUiSetup.RewireMainSceneDiceButtons();
            MainSceneGameplayRestore.RestoreGameplayMarkers(saveScene: saveScene);
            return true;
        }

        bool restored = TryCopySteveFromGitHead(main);
        if (!restored)
        {
            UnityEngine.Debug.LogWarning("MainSceneSteveRestore: git copy failed — creating Steve from MC02 prefab.");
            CreateSteveFromMc02(main);
        }

        RemoveDuplicateDiceSpawnerFromSteve();
        EnsureHeroSpawnPoint(main);
        SaveStevePrefabAsset();
        WireSpawnManagerHeroPrefab();
        MainSceneDiceUiSetup.RewireMainSceneDiceButtons();
        MainSceneGameplayRestore.RestoreGameplayMarkers(saveScene: false);

        if (saveScene)
            EditorSceneManager.SaveScene(main);

        UnityEngine.Debug.Log("MainSceneSteveRestore: Steve restored in main.unity.");
        return true;
    }

    static bool TryCopySteveFromGitHead(Scene main)
    {
        if (!MainSceneGitRestoreUtility.ExportGitMainScene(TempGitScenePath))
            return false;

        AssetDatabase.ImportAsset(TempGitScenePath);

        Scene source = EditorSceneManager.OpenScene(TempGitScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject steve = FindSteveInScene(source);
            if (steve == null)
            {
                UnityEngine.Debug.LogError("MainSceneSteveRestore: Steve not found in git HEAD main.unity.");
                return false;
            }

            GameObject copy = UnityEngine.Object.Instantiate(steve);
            copy.name = "Steve";
            SceneManager.MoveGameObjectToScene(copy, main);
            copy.transform.SetPositionAndRotation(steve.transform.position, steve.transform.rotation);
            Undo.RegisterCreatedObjectUndo(copy, "Restore Steve");

            CopyHeroSpawnPointFromSource(source, main);
            return true;
        }
        finally
        {
            if (source.IsValid())
                EditorSceneManager.CloseScene(source, true);
            AssetDatabase.DeleteAsset(TempGitScenePath);
        }
    }

    static void CreateSteveFromMc02(Scene main)
    {
        GameObject mc02Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Mc02PrefabPath);
        if (mc02Prefab == null)
        {
            UnityEngine.Debug.LogError($"MainSceneSteveRestore: missing {Mc02PrefabPath}.");
            return;
        }

        HeroSpawnPoint spawn = EnsureHeroSpawnPoint(main);
        Vector3 position = spawn != null ? spawn.transform.position : Vector3.zero;
        Quaternion rotation = spawn != null ? spawn.transform.rotation : Quaternion.identity;

        var steve = new GameObject("Steve");
        SceneManager.MoveGameObjectToScene(steve, main);
        steve.transform.SetPositionAndRotation(position, rotation);

        var visual = PrefabUtility.InstantiatePrefab(mc02Prefab, steve.transform) as GameObject;
        if (visual != null)
            visual.name = "MC02";

        if (steve.GetComponent<NavMeshAgent>() == null)
            steve.AddComponent<NavMeshAgent>();
        if (steve.GetComponent<HeroController>() == null)
            steve.AddComponent<HeroController>();
        if (steve.GetComponent<PlayerStats>() == null)
            steve.AddComponent<PlayerStats>();
        if (steve.GetComponent<HeroEquipment>() == null)
            steve.AddComponent<HeroEquipment>();
        if (steve.GetComponent<SteveMovement>() == null)
            steve.AddComponent<SteveMovement>();
        if (steve.GetComponent<SteveAnimator>() == null)
            steve.AddComponent<SteveAnimator>();

        var animator = steve.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HeroAnimatorPath);
            if (controller != null)
                animator.runtimeAnimatorController = controller;
        }

        Undo.RegisterCreatedObjectUndo(steve, "Create Steve");
    }

    static HeroSpawnPoint EnsureHeroSpawnPoint(Scene main)
    {
        foreach (GameObject root in main.GetRootGameObjects())
        {
            var existing = root.GetComponentInChildren<HeroSpawnPoint>(true);
            if (existing != null)
                return existing;
        }

        var markerGo = new GameObject("HeroSpawnPoint");
        SceneManager.MoveGameObjectToScene(markerGo, main);
        markerGo.transform.position = Vector3.zero;
        var marker = markerGo.AddComponent<HeroSpawnPoint>();
        Undo.RegisterCreatedObjectUndo(markerGo, "Add HeroSpawnPoint");
        return marker;
    }

    static void CopyHeroSpawnPointFromSource(Scene source, Scene main)
    {
        HeroSpawnPoint existing = null;
        foreach (GameObject root in main.GetRootGameObjects())
        {
            existing = root.GetComponentInChildren<HeroSpawnPoint>(true);
            if (existing != null)
                break;
        }

        if (existing != null)
            return;

        foreach (GameObject root in source.GetRootGameObjects())
        {
            var marker = root.GetComponentInChildren<HeroSpawnPoint>(true);
            if (marker == null)
                continue;

            GameObject copy = UnityEngine.Object.Instantiate(marker.gameObject);
            copy.name = marker.gameObject.name;
            SceneManager.MoveGameObjectToScene(copy, main);
            copy.transform.SetPositionAndRotation(marker.transform.position, marker.transform.rotation);
            Undo.RegisterCreatedObjectUndo(copy, "Restore HeroSpawnPoint");
            return;
        }

        EnsureHeroSpawnPoint(main);
    }

    static void RemoveDuplicateDiceSpawnerFromSteve()
    {
        GameObject steve = GameObject.Find("Steve");
        if (steve == null)
            return;

        var dice = steve.GetComponent<DiceSpawner>();
        if (dice == null)
            return;

        Undo.DestroyObjectImmediate(dice);
        UnityEngine.Debug.Log(
            "MainSceneSteveRestore: removed DiceSpawner from Steve (Bootstrap scene owns DiceSpawner).");
    }

    static void SaveStevePrefabAsset()
    {
        GameObject steve = GameObject.Find("Steve");
        if (steve == null)
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Heroes"))
            AssetDatabase.CreateFolder("Assets", "Heroes");
        if (!AssetDatabase.IsValidFolder("Assets/Heroes/Prefab"))
            AssetDatabase.CreateFolder("Assets/Heroes", "Prefab");

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(StevePrefabPath);
        if (existing != null)
            PrefabUtility.SaveAsPrefabAssetAndConnect(steve, StevePrefabPath, InteractionMode.AutomatedAction);
        else
            PrefabUtility.SaveAsPrefabAsset(steve, StevePrefabPath);

        AssetDatabase.SaveAssets();
    }

    public static void WireSpawnManagerHeroPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StevePrefabPath);
        if (prefab == null)
            return;

        foreach (SpawnManager spawn in UnityEngine.Object.FindObjectsByType<SpawnManager>(FindObjectsInactive.Include))
        {
            SerializedObject so = new SerializedObject(spawn);
            SerializedProperty prop = so.FindProperty("heroPrefab");
            if (prop == null)
                continue;
            if (prop.objectReferenceValue == prefab)
                continue;
            prop.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawn);
        }
    }

    public static GameObject FindSteveInScene(Scene scene)
    {
        if (!scene.IsValid())
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null)
                continue;

            if (root.name.Equals("Steve", StringComparison.OrdinalIgnoreCase) &&
                root.GetComponentInChildren<HeroController>(true) != null)
                return root;

            HeroController onRoot = root.GetComponent<HeroController>();
            if (onRoot != null && root.name.Equals("Steve", StringComparison.OrdinalIgnoreCase))
                return root.gameObject;

            HeroController inChildren = root.GetComponentInChildren<HeroController>(true);
            if (inChildren != null && inChildren.gameObject.name.Equals("Steve", StringComparison.OrdinalIgnoreCase))
                return inChildren.gameObject;
        }

        return null;
    }
}
#endif
