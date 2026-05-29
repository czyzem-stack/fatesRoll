#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Removes duplicate bootstrap / manager objects from main.unity (Bootstrap scene keeps DDOL copy).</summary>
public static class MainSceneBootstrapCleanup
{
    public const string MainScenePath = "Assets/Scenes/main.unity";

    static readonly string[] RootNamesToRemove = { "GameServices", "Managers" };

    static readonly Type[] ServiceComponentTypes =
    {
        typeof(GlobalSettings), typeof(DiceSpawner), typeof(POIManager), typeof(SpawnManager),
        typeof(EnergyManager), typeof(LevelManager), typeof(LootManager), typeof(EquipmentLootManager),
        typeof(RogueLiteManager), typeof(RunDeathController), typeof(EnemyStatManager),
        typeof(TalentManager), typeof(QuestManager)
    };

    [MenuItem("FatesRoll/Setup/Remove Duplicate Bootstrap From Main Scene")]
    public static void RemoveFromMainSceneMenu()
    {
        RemoveFromMainScene(saveScene: true);
    }

    [MenuItem("FatesRoll/Setup/Strip Bootstrap Service Components From Main Scene")]
    public static void StripServiceComponentsFromMainSceneMenu()
    {
        StripServiceComponentsFromMainScene(saveScene: true);
    }

    /// <summary>Batchmode: Unity -executeMethod MainSceneBootstrapCleanup.RemoveFromMainSceneBatch</summary>
    public static void RemoveFromMainSceneBatch()
    {
        RemoveFromMainScene(saveScene: true);
    }

    public static int RemoveFromMainScene(bool saveScene = true)
    {
        if (!System.IO.File.Exists(MainScenePath))
        {
            Debug.LogError($"MainSceneBootstrapCleanup: missing {MainScenePath}.");
            return 0;
        }

        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        int removed = RemoveBootstrapObjectsFromScene(scene);
        int stripped = StripBootstrapServiceComponents(scene);

        if (saveScene && (removed > 0 || stripped > 0))
            EditorSceneManager.SaveScene(scene);

        Debug.Log(
            (removed > 0 || stripped > 0)
                ? $"MainSceneBootstrapCleanup: removed {removed} object(s), stripped {stripped} duplicate service component(s) from main.unity (Hero, UI, geometry, POIs kept)."
                : "MainSceneBootstrapCleanup: no duplicate GameServices / manager roots found in main.");

        return removed + stripped;
    }

    public static int StripServiceComponentsFromMainScene(bool saveScene = true)
    {
        if (!System.IO.File.Exists(MainScenePath))
        {
            Debug.LogError($"MainSceneBootstrapCleanup: missing {MainScenePath}.");
            return 0;
        }

        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        int stripped = StripBootstrapServiceComponents(scene);

        if (saveScene && stripped > 0)
            EditorSceneManager.SaveScene(scene);

        Debug.Log(
            stripped > 0
                ? $"MainSceneBootstrapCleanup: stripped {stripped} bootstrap service component(s) from main.unity."
                : "MainSceneBootstrapCleanup: no bootstrap service components needed stripping in main.");

        return stripped;
    }

    public static int RemoveBootstrapObjectsFromScene(Scene scene)
    {
        var destroyed = new System.Collections.Generic.HashSet<GameObject>();
        int count = 0;

        foreach (string rootName in RootNamesToRemove)
        {
            GameObject root = GameObject.Find(rootName);
            if (root == null || root.scene != scene)
                continue;

            RescueProtectedDescendants(root);
            RescueGameplayMarkersFrom(root);
            Undo.DestroyObjectImmediate(root);
            destroyed.Add(root);
            count++;
        }

        foreach (Type serviceType in ServiceComponentTypes)
        {
            foreach (var obj in UnityEngine.Object.FindObjectsByType(serviceType, FindObjectsInactive.Include))
            {
                if (obj is not Component behaviour)
                    continue;

                GameObject go = behaviour.gameObject;
                if (go == null || go.scene != scene || destroyed.Contains(go))
                    continue;

                if (IsProtectedMainObject(go))
                    continue;

                if (IsManagerObjectWithGameplayChildren(serviceType))
                    RescueGameplayMarkersFrom(go);

                Undo.DestroyObjectImmediate(go);
                destroyed.Add(go);
                count++;
            }
        }

        if (count > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        return count;
    }

    static bool IsManagerObjectWithGameplayChildren(Type serviceType) =>
        serviceType == typeof(POIManager) || serviceType == typeof(SpawnManager);

    static int StripBootstrapServiceComponents(Scene scene)
    {
        int stripped = 0;
        foreach (Type serviceType in ServiceComponentTypes)
        {
            foreach (var obj in UnityEngine.Object.FindObjectsByType(serviceType, FindObjectsInactive.Include))
            {
                if (obj is not Component component)
                    continue;

                GameObject go = component.gameObject;
                if (go == null || go.scene != scene)
                    continue;

                if (component is POIManager || component is SpawnManager)
                    RescueGameplayMarkersFrom(go);

                Undo.DestroyObjectImmediate(component);
                stripped++;
            }
        }

        if (stripped > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        return stripped;
    }

    /// <summary>POI / spawn markers must survive when their manager root is removed.</summary>
    static void RescueGameplayMarkersFrom(GameObject root)
    {
        if (root == null)
            return;

        var rescued = new System.Collections.Generic.HashSet<Transform>();

        foreach (POINode poi in root.GetComponentsInChildren<POINode>(true))
        {
            if (poi != null)
                rescued.Add(poi.transform);
        }

        foreach (SpawnNode node in root.GetComponentsInChildren<SpawnNode>(true))
        {
            if (node != null)
                rescued.Add(node.transform);
        }

        foreach (Transform t in rescued)
        {
            if (t == null)
                continue;
            Undo.SetTransformParent(t, null, "Rescue POI/Spawn from manager");
        }
    }

    /// <summary>Move Steve / spawn marker out of GameServices before the root is destroyed.</summary>
    static void RescueProtectedDescendants(GameObject root)
    {
        if (root == null)
            return;

        var rescued = new System.Collections.Generic.HashSet<Transform>();

        foreach (HeroController hero in root.GetComponentsInChildren<HeroController>(true))
        {
            if (hero != null)
                rescued.Add(hero.transform);
        }

        foreach (HeroSpawnPoint spawn in root.GetComponentsInChildren<HeroSpawnPoint>(true))
        {
            if (spawn != null)
                rescued.Add(spawn.transform);
        }

        foreach (Transform t in rescued)
        {
            if (t == null)
                continue;
            Undo.SetTransformParent(t, null, "Rescue Steve from GameServices");
        }
    }

    /// <summary>Steve stays (DiceSpawner on Steve is not a duplicate manager object).</summary>
    static bool IsProtectedMainObject(GameObject go)
    {
        if (go.GetComponent<HeroController>() != null)
            return true;
        if (go.name.Equals("Steve", StringComparison.OrdinalIgnoreCase))
            return true;
        if (go.GetComponent<HeroSpawnPoint>() != null)
            return true;
        return false;
    }
}
#endif
