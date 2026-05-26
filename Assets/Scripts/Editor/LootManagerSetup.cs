#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LootManagerSetup
{
    const string CoinPrefabPath = "Assets/Coins/Prefabs/Coins/coin_04.prefab";
    const string LootManagerPrefabPath = "Assets/Prefabs/LootManager.prefab";

    [MenuItem("FatesRoll/Loot/Add LootManager To Scene", false, 0)]
    public static void AddLootManagerToScene()
    {
        var existing = Object.FindAnyObjectByType<LootManager>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.Log("LootManager already in this scene — selected it in the Hierarchy.");
            return;
        }

        GameObject go = LoadOrCreateLootManagerObject();
        Transform parent = GetManagersRoot(createIfMissing: true);
        if (parent != null)
            go.transform.SetParent(parent, false);

        Undo.RegisterCreatedObjectUndo(go, "Add LootManager");
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        Debug.Log("LootManager added to the scene. Save the scene (Ctrl+S) so it stays in the Hierarchy.");
    }

    [MenuItem("FatesRoll/Loot/Create LootManager Prefab", false, 1)]
    public static void CreateLootManagerPrefab()
    {
        Directory.CreateDirectory("Assets/Prefabs");

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LootManagerPrefabPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log($"Prefab already exists at {LootManagerPrefabPath}");
            return;
        }

        GameObject temp = BuildLootManagerObject();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, LootManagerPrefabPath);
        Object.DestroyImmediate(temp);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"Created {LootManagerPrefabPath}. Drag it into your scene (e.g. under Managers).");
    }

    [InitializeOnLoad]
    static class SceneOpenHook
    {
        static SceneOpenHook()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (Application.isPlaying || !scene.IsValid() || !scene.isLoaded)
                return;

            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying)
                    return;
                if (Object.FindAnyObjectByType<LootManager>() != null)
                    return;

                Debug.LogWarning(
                    "[FatesRoll] No LootManager in this scene. Use menu: FatesRoll → Loot → Add LootManager To Scene");
            };
        }
    }

    static GameObject LoadOrCreateLootManagerObject()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LootManagerPrefabPath);
        if (prefab != null)
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        return BuildLootManagerObject();
    }

    static GameObject BuildLootManagerObject()
    {
        var go = new GameObject("LootManager");
        var loot = go.AddComponent<LootManager>();
        loot.coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefabPath);
        loot.AutoAssignUI();
        return go;
    }

    static Transform GetManagersRoot(bool createIfMissing)
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
