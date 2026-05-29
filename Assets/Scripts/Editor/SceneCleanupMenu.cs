#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Removes broken script references (e.g. deleted HeroWeaponStance) from open scenes and prefabs.</summary>
public static class SceneCleanupMenu
{
    const string MainScenePath = MainSceneBootstrapCleanup.MainScenePath;
    const string BootstrapScenePath = BootstrapSceneSetup.BootstrapScenePath;

    [MenuItem("FatesRoll/Cleanup/Remove Missing Scripts In Open Scene")]
    public static void RemoveMissingScriptsInOpenScene()
    {
        int removed = RemoveMissingScriptsRecursive(
            EditorSceneManager.GetActiveScene().GetRootGameObjects());

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"FatesRoll: removed {removed} missing script component(s) from the open scene.");
    }

    [MenuItem("FatesRoll/Cleanup/Remove Missing Scripts In Main Scene")]
    public static void RemoveMissingScriptsInMainScene()
    {
        RemoveMissingScriptsInSceneAtPath(MainScenePath, save: true);
    }

    [MenuItem("FatesRoll/Cleanup/Remove Missing Scripts In Bootstrap Scene")]
    public static void RemoveMissingScriptsInBootstrapScene()
    {
        RemoveMissingScriptsInSceneAtPath(BootstrapScenePath, save: true);
    }

    [MenuItem("FatesRoll/Cleanup/Remove Obsolete Hero Components In Open Scene")]
    public static void RemoveObsoleteHeroComponentsInOpenScene()
    {
        int removed = 0;
#pragma warning disable CS0618 // Intentionally targets obsolete placeholder so it can be stripped from scenes.
        foreach (var stance in Object.FindObjectsByType<HeroWeaponStance>(FindObjectsInactive.Include))
        {
            if (stance == null)
                continue;
            Object.DestroyImmediate(stance);
            removed++;
        }
#pragma warning restore CS0618

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"FatesRoll: removed {removed} obsolete HeroWeaponStance component(s). Save the scene.");
    }

    [MenuItem("FatesRoll/Cleanup/Remove Obsolete Camera Follow In Open Scene")]
    public static void RemoveObsoleteCameraFollowInOpenScene()
    {
        int removed = RemoveObsoleteCameraFollowRecursive(
            EditorSceneManager.GetActiveScene().GetRootGameObjects());

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"FatesRoll: removed {removed} obsolete CameraFollow component(s). Save the scene.");
    }

    [MenuItem("FatesRoll/Cleanup/Remove Obsolete Camera Follow In Main Scene")]
    public static void RemoveObsoleteCameraFollowInMainScene()
    {
        RemoveObsoleteCameraFollowInSceneAtPath(MainScenePath, save: true);
    }

    [MenuItem("FatesRoll/Cleanup/Remove Missing Scripts On Selected")]
    public static void RemoveMissingScriptsOnSelection()
    {
        int removed = 0;
        foreach (var obj in Selection.gameObjects)
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);

        Debug.Log($"FatesRoll: removed {removed} missing script component(s) from selection.");
    }

    static void RemoveMissingScriptsInSceneAtPath(string path, bool save)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"FatesRoll: missing scene at {path}.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        int removed = RemoveMissingScriptsRecursive(scene.GetRootGameObjects());
        if (removed > 0)
        {
            if (save)
                EditorSceneManager.SaveScene(scene);
            Debug.Log($"FatesRoll: removed {removed} missing script component(s) from {path}.");
        }
        else
            Debug.Log($"FatesRoll: no missing scripts in {path}.");
    }

    static void RemoveObsoleteCameraFollowInSceneAtPath(string path, bool save)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"FatesRoll: missing scene at {path}.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        int removed = RemoveObsoleteCameraFollowRecursive(scene.GetRootGameObjects());
        if (removed > 0)
        {
            if (save)
                EditorSceneManager.SaveScene(scene);
            Debug.Log($"FatesRoll: removed {removed} obsolete CameraFollow component(s) from {path}.");
        }
        else
            Debug.Log($"FatesRoll: no CameraFollow components in {path}.");
    }

    static int RemoveMissingScriptsRecursive(GameObject[] roots)
    {
        int count = 0;
        foreach (var root in roots)
        {
            if (root == null)
                continue;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
        }

        return count;
    }

    static int RemoveObsoleteCameraFollowRecursive(GameObject[] roots)
    {
        int count = 0;
#pragma warning disable CS0618
        foreach (var root in roots)
        {
            if (root == null)
                continue;

            foreach (var follow in root.GetComponentsInChildren<CameraFollow>(true))
            {
                if (follow == null)
                    continue;
                Object.DestroyImmediate(follow);
                count++;
            }
        }
#pragma warning restore CS0618
        return count;
    }
}
#endif
