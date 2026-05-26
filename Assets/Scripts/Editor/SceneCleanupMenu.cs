#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Removes broken script references (e.g. deleted HeroWeaponStance) from open scenes and prefabs.</summary>
public static class SceneCleanupMenu
{
    [MenuItem("FatesRoll/Cleanup/Remove Missing Scripts In Open Scene")]
    public static void RemoveMissingScriptsInOpenScene()
    {
        int removed = RemoveMissingScriptsRecursive(
            EditorSceneManager.GetActiveScene().GetRootGameObjects());

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"FatesRoll: removed {removed} missing script component(s) from the open scene.");
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

    [MenuItem("FatesRoll/Cleanup/Remove Missing Scripts On Selected")]
    public static void RemoveMissingScriptsOnSelection()
    {
        int removed = 0;
        foreach (var obj in Selection.gameObjects)
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);

        Debug.Log($"FatesRoll: removed {removed} missing script component(s) from selection.");
    }

    private static int RemoveMissingScriptsRecursive(GameObject[] roots)
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
}
#endif
