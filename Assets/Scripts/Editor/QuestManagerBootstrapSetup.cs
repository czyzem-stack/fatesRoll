#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One QuestManager under GameServices on Bootstrap — remove duplicates from main.</summary>
public static class QuestManagerBootstrapSetup
{
    const string BootstrapPath = BootstrapSceneSetup.BootstrapScenePath;
    const string MainPath = MainSceneBootstrapCleanup.MainScenePath;

    [MenuItem("FatesRoll/Setup/Move Quest Manager To Bootstrap (remove from main)")]
    public static void MoveQuestManagerToBootstrap()
    {
        if (!System.IO.File.Exists(BootstrapPath))
        {
            Debug.LogError($"Missing {BootstrapPath}.");
            return;
        }

        Scene bootstrap = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
        GameServices services = Object.FindAnyObjectByType<GameServices>();
        if (services == null)
        {
            Debug.LogError("Bootstrap: no GameServices object.");
            return;
        }

        QuestManager bootstrapQm = services.GetComponentInChildren<QuestManager>(true);
        if (bootstrapQm == null)
        {
            var go = new GameObject("QuestManager");
            go.transform.SetParent(services.transform, false);
            bootstrapQm = go.AddComponent<QuestManager>();
            Debug.Log("Bootstrap: added QuestManager under GameServices.");
        }

        SerializedObject servicesSo = new SerializedObject(services);
        SerializedProperty questProp = servicesSo.FindProperty("questManager");
        if (questProp != null)
        {
            questProp.objectReferenceValue = bootstrapQm;
            servicesSo.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(bootstrap);
        EditorSceneManager.SaveScene(bootstrap);

        int removedFromMain = RemoveQuestManagersFromMain();
        Selection.activeGameObject = bootstrapQm.gameObject;
        EditorGUIUtility.PingObject(bootstrapQm.gameObject);

        Debug.Log(
            $"QuestManager lives on Bootstrap under GameServices. Removed {removedFromMain} duplicate(s) from main. " +
            "Play from Bootstrap — do not keep a second QuestManager in main.");
    }

    static int RemoveQuestManagersFromMain()
    {
        if (!System.IO.File.Exists(MainPath))
            return 0;

        Scene main = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Additive);
        int removed = 0;

        foreach (QuestManager qm in Object.FindObjectsByType<QuestManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (qm.gameObject.scene != main)
                continue;

            Object.DestroyImmediate(qm);
            removed++;
        }

        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.SaveScene(main);
        }

        EditorSceneManager.CloseScene(main, removeScene: true);
        EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
        return removed;
    }
}
#endif
