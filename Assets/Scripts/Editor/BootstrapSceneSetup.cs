#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates Bootstrap.unity with [Bootstrap] → GameServices and copies service managers from main (main unchanged).</summary>
public static class BootstrapSceneSetup
{
    public const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
    public const string MainScenePath = "Assets/Scenes/main.unity";

    static readonly Type[] ServiceComponentTypes =
    {
        typeof(GlobalSettings), typeof(DiceSpawner), typeof(POIManager), typeof(SpawnManager),
        typeof(EnergyManager), typeof(LevelManager), typeof(LootManager), typeof(EquipmentLootManager),
        typeof(RogueLiteManager), typeof(RunDeathController), typeof(EnemyStatManager)
    };

    [MenuItem("FatesRoll/Setup/Create Bootstrap Scene From Main")]
    public static void CreateBootstrapSceneMenu()
    {
        CreateBootstrapScene();
    }

    /// <summary>Batchmode: Unity -executeMethod BootstrapSceneSetup.CreateBootstrapScene</summary>
    public static void CreateBootstrapScene()
    {
        if (!File.Exists(MainScenePath))
        {
            Debug.LogError($"BootstrapSceneSetup: missing {MainScenePath}.");
            return;
        }

        Scene mainScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        GameObject mainServicesRoot = GameObject.Find("GameServices");
        if (mainServicesRoot == null)
        {
            Debug.LogError("BootstrapSceneSetup: no 'GameServices' in main.unity.");
            return;
        }

        Scene bootstrapScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(bootstrapScene);

        GameObject bootstrapRoot = new GameObject("[Bootstrap]");
        bootstrapRoot.AddComponent<BootstrapFlow>();

        GameObject servicesGo = new GameObject("GameServices");
        servicesGo.transform.SetParent(bootstrapRoot.transform, false);

        GameServices services = servicesGo.AddComponent<GameServices>();
        SerializedObject servicesSo = new SerializedObject(services);
        SerializedProperty persist = servicesSo.FindProperty("persistAcrossScenes");
        if (persist != null)
            persist.boolValue = true;
        servicesSo.ApplyModifiedPropertiesWithoutUndo();

        int copied = 0;
        foreach (Type serviceType in ServiceComponentTypes)
        {
            if (TryCopyService(serviceType, mainServicesRoot, mainScene, servicesGo))
                copied++;
        }

        ClearCrossSceneReferences(bootstrapRoot, bootstrapScene);

        EditorSceneManager.CloseScene(mainScene, removeScene: true);

        if (!EditorSceneManager.SaveScene(bootstrapScene, BootstrapScenePath))
        {
            Debug.LogError($"BootstrapSceneSetup: could not save {BootstrapScenePath}.");
            return;
        }

        EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

        TitleSceneSetup.SetBuildScenes();
        TitleSceneSetup.ApplyPlayModeStartBootstrap(silent: true);
        MainSceneBootstrapCleanup.RemoveFromMainScene(saveScene: true);
        MainSceneSteveRestore.RestoreSteve(saveScene: true);
        MainSceneGameplayRestore.RestoreGameplayMarkers(saveScene: true);
        AssetDatabase.SaveAssets();

        var bootstrapRootInScene = GameObject.Find("[Bootstrap]");
        if (bootstrapRootInScene != null)
            Selection.activeGameObject = bootstrapRootInScene;

        Debug.Log(
            $"BootstrapSceneSetup: saved {BootstrapScenePath} with [Bootstrap] → GameServices and {copied} service object(s). " +
            "main.unity unchanged. Build order: Bootstrap → title → main.");
    }

    static bool TryCopyService(Type serviceType, GameObject servicesRoot, Scene mainScene, GameObject servicesGo)
    {
        Component source = FindServiceSource(serviceType, servicesRoot, mainScene);
        if (source != null)
        {
            string objectName = serviceType == typeof(DiceSpawner) ? "DiceSpawner" : source.gameObject.name;
            CopyServiceComponent(source, servicesGo, objectName);
            return true;
        }

        if (serviceType != typeof(RunDeathController))
        {
            Debug.LogWarning($"BootstrapSceneSetup: no {serviceType.Name} in main.unity — skipped.");
            return false;
        }

        var runDeathGo = new GameObject("RunDeathController");
        runDeathGo.transform.SetParent(servicesGo.transform, false);
        runDeathGo.AddComponent<RunDeathController>();
        Debug.Log("BootstrapSceneSetup: RunDeathController not in main.unity — added under GameServices.");
        return true;
    }

    /// <summary>Prefer GameServices subtree; DiceSpawner may live on Steve; others can be scene-wide.</summary>
    static Component FindServiceSource(Type serviceType, GameObject servicesRoot, Scene mainScene)
    {
        foreach (var component in servicesRoot.GetComponentsInChildren(serviceType, true))
        {
            if (component is not Component behaviour)
                continue;

            if (serviceType == typeof(DiceSpawner))
                return behaviour;

            if (!IsExcludedFromBootstrap(behaviour.gameObject))
                return behaviour;
        }

        foreach (var obj in UnityEngine.Object.FindObjectsByType(serviceType, FindObjectsInactive.Include))
        {
            if (obj is not Component component)
                continue;

            if (component.gameObject.scene != mainScene)
                continue;

            if (serviceType == typeof(DiceSpawner))
                return component;

            if (!IsExcludedFromBootstrap(component.gameObject))
                return component;
        }

        return null;
    }

    /// <summary>Copy component only — no children (avoids HealthBar / UI from main).</summary>
    static void CopyServiceComponent(Component source, GameObject parent, string objectName)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent.transform, false);
        var dest = go.AddComponent(source.GetType());
        EditorUtility.CopySerialized(source, dest);
    }

    static bool IsExcludedFromBootstrap(GameObject go)
    {
        if (go.GetComponent<HeroController>() != null)
            return true;
        if (go.name.Equals("Steve", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    static void ClearCrossSceneReferences(GameObject root, Scene targetScene)
    {
        foreach (var mono in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mono == null)
                continue;

            var so = new SerializedObject(mono);
            var prop = so.GetIterator();
            bool changed = false;

            while (prop.NextVisible(true))
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                var obj = prop.objectReferenceValue;
                if (obj == null)
                    continue;

                if (obj is not (GameObject or Component))
                    continue;

                GameObject referencedGo = obj is GameObject g ? g : ((Component)obj).gameObject;
                if (referencedGo.scene.IsValid() && referencedGo.scene != targetScene)
                {
                    prop.objectReferenceValue = null;
                    changed = true;
                }
            }

            if (changed)
                so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

#endif
