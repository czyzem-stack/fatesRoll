#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Restores POINode / SpawnNode markers into main.unity from git HEAD (lost when manager roots were deleted).</summary>
public static class MainSceneGameplayRestore
{
    public const string MainScenePath = MainSceneBootstrapCleanup.MainScenePath;

    [MenuItem("FatesRoll/Setup/Restore POIs And Spawn Nodes In Main Scene")]
    public static void RestoreGameplayMarkersMenu()
    {
        RestoreGameplayMarkers(saveScene: true);
    }

    /// <summary>Batchmode: Unity -executeMethod MainSceneGameplayRestore.RestoreGameplayMarkersBatch</summary>
    public static void RestoreGameplayMarkersBatch()
    {
        RestoreGameplayMarkers(saveScene: true);
    }

    public static bool RestoreGameplayMarkers(bool saveScene = true)
    {
        if (!File.Exists(MainScenePath))
        {
            Debug.LogError($"MainSceneGameplayRestore: missing {MainScenePath}.");
            return false;
        }

        Scene main = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        int poiBefore = CountComponents<POINode>(main);
        int spawnBefore = CountComponents<SpawnNode>(main);

        if (!MainSceneGitRestoreUtility.ExportGitMainScene())
        {
            Debug.LogError("MainSceneGameplayRestore: could not export main.unity from git HEAD.");
            return false;
        }

        AssetDatabase.ImportAsset(MainSceneGitRestoreUtility.TempGitScenePath);
        Scene source = EditorSceneManager.OpenScene(MainSceneGitRestoreUtility.TempGitScenePath, OpenSceneMode.Additive);

        try
        {
            int poiCopied = CopyMissingMarkers<POINode>(source, main);
            int spawnCopied = CopyMissingMarkers<SpawnNode>(source, main);

            // Close temp scene before saving main — avoids cross-scene refs while source is loaded.
            if (source.IsValid())
            {
                EditorSceneManager.CloseScene(source, true);
                source = default;
            }

            if (saveScene && (poiCopied > 0 || spawnCopied > 0))
                EditorSceneManager.SaveScene(main);

            Debug.Log(
                $"MainSceneGameplayRestore: POIs {poiBefore}→{CountComponents<POINode>(main)} " +
                $"(+{poiCopied}), SpawnNodes {spawnBefore}→{CountComponents<SpawnNode>(main)} (+{spawnCopied}).");

            return poiCopied > 0 || spawnCopied > 0 || poiBefore > 0 || spawnBefore > 0;
        }
        finally
        {
            if (source.IsValid())
                EditorSceneManager.CloseScene(source, true);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainSceneGitRestoreUtility.TempGitScenePath) != null)
                AssetDatabase.DeleteAsset(MainSceneGitRestoreUtility.TempGitScenePath);
        }
    }

    static int CopyMissingMarkers<T>(Scene source, Scene destination) where T : Component
    {
        var existingNames = new HashSet<string>();
        foreach (GameObject root in destination.GetRootGameObjects())
        {
            foreach (T marker in root.GetComponentsInChildren<T>(true))
            {
                if (marker != null)
                    existingNames.Add(marker.gameObject.name);
            }
        }

        int copied = 0;
        foreach (GameObject root in source.GetRootGameObjects())
        {
            foreach (T marker in root.GetComponentsInChildren<T>(true))
            {
                if (marker == null)
                    continue;

                GameObject sourceGo = marker.gameObject;
                if (existingNames.Contains(sourceGo.name))
                {
                    if (!RemoveStaleHierarchyCopy<T>(destination, sourceGo.name))
                        continue;
                    existingNames.Remove(sourceGo.name);
                }

                // Marker only — visuals / HealthBar are rebuilt at runtime by POIVisualBuilder.
                GameObject copy = new GameObject(sourceGo.name);
                copy.transform.SetPositionAndRotation(sourceGo.transform.position, sourceGo.transform.rotation);
                SceneManager.MoveGameObjectToScene(copy, destination);

                T destMarker = copy.AddComponent<T>();
                EditorUtility.CopySerialized(marker, destMarker);
                SanitizeCopiedMarker(destMarker);

                Undo.RegisterCreatedObjectUndo(copy, $"Restore {typeof(T).Name}");
                existingNames.Add(copy.name);
                copied++;
            }
        }

        return copied;
    }

    /// <summary>Earlier restores copied full hierarchies (HealthBar → temp-scene camera). Drop and re-copy.</summary>
    static bool RemoveStaleHierarchyCopy<T>(Scene destination, string markerName) where T : Component
    {
        foreach (GameObject root in destination.GetRootGameObjects())
        {
            foreach (T marker in root.GetComponentsInChildren<T>(true))
            {
                if (marker == null || marker.gameObject.name != markerName)
                    continue;

                if (marker.transform.childCount == 0)
                    return false;

                Undo.DestroyObjectImmediate(marker.gameObject);
                return true;
            }
        }

        return false;
    }

    static void SanitizeCopiedMarker(Component marker)
    {
        switch (marker)
        {
            case POINode poi:
                poi.currentVisual = null;
                poi.gameObject.tag = "POI";
                break;
            case SpawnNode node:
                node.activeEncounter = null;
                node.hasSpawnedType = false;
                break;
        }
    }

    static int CountComponents<T>(Scene scene) where T : Component
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            count += root.GetComponentsInChildren<T>(true).Length;
        return count;
    }
}
#endif
