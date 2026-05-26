#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DiceSpawnerSetup
{
    private const string D6PrefabPath = "Assets/Dice/Prefabs/Dice_d6.prefab";
    private const string ResourcesCopyPath = "Assets/Resources/Dice/Dice_d6.prefab";

    [MenuItem("FatesRoll/Dice/Fix Dice Spawner In Scene")]
    public static void FixDiceSpawnerInScene()
    {
        EnsureResourcesCopy();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(D6PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Dice prefab not found at {D6PrefabPath}");
            return;
        }

        var spawner = Object.FindAnyObjectByType<DiceSpawner>();
        if (spawner == null)
        {
            var go = new GameObject("DiceSpawner");
            spawner = go.AddComponent<DiceSpawner>();

            Transform parent = FindManagersRoot();
            if (parent != null)
                go.transform.SetParent(parent, false);

            Undo.RegisterCreatedObjectUndo(go, "Add DiceSpawner");
            Debug.Log("Created DiceSpawner in scene.");
        }

        spawner.d6Prefab = prefab;
        if (spawner.spawnPoint == null)
            spawner.spawnPoint = spawner.transform;

        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        Selection.activeGameObject = spawner.gameObject;
        Debug.Log("DiceSpawner: assigned Dice_d6 prefab and spawn point. Save the scene (Ctrl+S).");
    }

    [MenuItem("FatesRoll/Dice/Ensure D6 In Resources (for builds)")]
    public static void EnsureResourcesCopy()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Dice"))
            AssetDatabase.CreateFolder("Assets/Resources", "Dice");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesCopyPath) != null)
            return;

        if (!AssetDatabase.CopyAsset(D6PrefabPath, ResourcesCopyPath))
        {
            Debug.LogWarning("Could not copy dice prefab to Resources — assign manually for standalone builds.");
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Copied dice prefab to {ResourcesCopyPath} for Resources.Load at runtime.");
    }

    private static Transform FindManagersRoot()
    {
        foreach (string name in new[] { "Managers", "GameManagers", "_Managers" })
        {
            var found = GameObject.Find(name);
            if (found != null)
                return found.transform;
        }

        return null;
    }
}
#endif
