#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renames ScrollRect child "Veiwport" → "Viewport" (GUI Pro typo). Use the menu — do not text-edit binary scenes.
/// </summary>
public static class FixScrollViewportTypo
{
    const string TypoName = "Veiwport";
    const string FixedName = "Viewport";

    static readonly string[] DefaultPrefabPaths =
    {
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_DemoScene_Panels/Mission.prefab",
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_DemoScene_Panels/Equipment.prefab",
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_DemoScene_Panels/Shop_Chest.prefab",
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_DemoScene_Panels/Guild.prefab",
    };

    [MenuItem("FatesRoll/Setup/Fix Scroll Viewport Typo In Open Scene")]
    public static void FixOpenSceneMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("FixScrollViewportTypo: no active loaded scene.");
            return;
        }

        int fixedCount = FixInScene(scene);
        if (fixedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"FixScrollViewportTypo: renamed {fixedCount} '{TypoName}' → '{FixedName}' in {scene.path}. Save the scene (Ctrl+S).");
        }
        else
            Debug.Log($"FixScrollViewportTypo: no '{TypoName}' under ScrollRect in {scene.name}.");
    }

    [MenuItem("FatesRoll/Setup/Fix Scroll Viewport Typo In Main Scene")]
    public static void FixMainSceneMenu()
    {
        string path = MainSceneBootstrapCleanup.MainScenePath;
        if (!File.Exists(path))
        {
            Debug.LogError($"FixScrollViewportTypo: missing {path}.");
            return;
        }

        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        int fixedCount = FixInScene(scene);
        if (fixedCount > 0)
        {
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"FixScrollViewportTypo: fixed {fixedCount} in {path} and saved.");
        }
        else
            Debug.Log($"FixScrollViewportTypo: no '{TypoName}' under ScrollRect in main — nothing to save.");

        if (previous.IsValid() && previous.isLoaded && previous.path != path)
            EditorSceneManager.OpenScene(previous.path, OpenSceneMode.Single);
    }

    [MenuItem("FatesRoll/Setup/Fix Scroll Viewport Typo In GUI Pro Prefabs")]
    public static void FixGuiProPrefabsMenu()
    {
        int total = 0;
        foreach (string path in DefaultPrefabPaths)
        {
            if (!File.Exists(path))
                continue;

            total += FixInPrefabAsset(path);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"FixScrollViewportTypo: renamed {total} '{TypoName}' → '{FixedName}' across GUI Pro panel prefabs.");
    }

    static int FixInScene(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!IsScrollRectViewportTypo(t))
                    continue;

                Undo.RecordObject(t.gameObject, "Fix Veiwport typo");
                t.gameObject.name = FixedName;
                count++;
            }
        }

        return count;
    }

    static int FixInPrefabAsset(string assetPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
        if (root == null)
            return 0;

        try
        {
            int count = FixInHierarchy(root.transform);
            if (count > 0)
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            return count;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static int FixInHierarchy(Transform root)
    {
        int count = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!IsScrollRectViewportTypo(t))
                continue;

            t.gameObject.name = FixedName;
            count++;
        }

        return count;
    }

    static bool IsScrollRectViewportTypo(Transform t)
    {
        if (t == null || t.name != TypoName)
            return false;

        Transform parent = t.parent;
        if (parent == null)
            return false;

        return parent.GetComponent<ScrollRect>() != null ||
               parent.name == "ScrollRect";
    }
}
#endif
