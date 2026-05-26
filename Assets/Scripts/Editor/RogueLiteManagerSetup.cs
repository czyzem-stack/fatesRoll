#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RogueLiteManagerSetup
{
    [MenuItem("FatesRoll/Roguelite/Add RogueLiteManager To Scene")]
    public static void AddToScene()
    {
        var existing = Object.FindAnyObjectByType<RogueLiteManager>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("RogueLiteManager already in scene.");
            return;
        }

        var go = new GameObject("RogueLiteManager");
        var manager = go.AddComponent<RogueLiteManager>();
        manager.EditorAssignDefaultButtonPrefabs();

        Transform parent = GetManagersRoot(createIfMissing: true);
        if (parent != null)
            go.transform.SetParent(parent, false);

        Undo.RegisterCreatedObjectUndo(go, "Add RogueLiteManager");
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("RogueLiteManager added. Save the scene (Ctrl+S).");
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
