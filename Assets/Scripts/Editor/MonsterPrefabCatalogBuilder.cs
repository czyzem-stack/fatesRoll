using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MonsterPrefabCatalogBuilder
{
    private const string CatalogPath = "Assets/Data/Monsters/MonsterPrefabCatalog.asset";

    [MenuItem("FatesRoll/Enemies/Build Monster Prefab Catalog")]
    public static void BuildCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MonsterPrefabCatalog>(CatalogPath);
        if (catalog == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Data/Monsters");
            catalog = ScriptableObject.CreateInstance<MonsterPrefabCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.gameplayFallback = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            POIVisualBuilder.GameplayAnimatorFallbackPath);

        var list = new List<MonsterPrefabCatalog.Entry>();
        foreach (var type in MonsterPOIDefinitions.CombatTypes)
        {
            string path = MonsterPOIDefinitions.GetPrefabAssetPath(type);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"MonsterPrefabCatalog: missing prefab at {path}");
                continue;
            }

            list.Add(new MonsterPrefabCatalog.Entry
            {
                type = type,
                prefab = prefab,
                gameplayAnimator = PickGameplayAnimator(type, catalog.gameplayFallback)
            });
        }

        catalog.entries = list.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"MonsterPrefabCatalog updated at {CatalogPath} ({list.Count} monsters).");

        var spawn = Object.FindAnyObjectByType<SpawnManager>();
        if (spawn != null)
        {
            var so = new SerializedObject(spawn);
            so.FindProperty("monsterCatalog").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawn);
        }
    }

    /// <summary>
    /// Orc uses OrcAnimator (Speed/Attack). Skeleton/Slime use modified pack controllers.
    /// All other types keep their native pack controller for state-based driving.
    /// </summary>
    private static RuntimeAnimatorController PickGameplayAnimator(
        POIType type,
        RuntimeAnimatorController orcGameplay)
    {
        string packPath = MonsterPOIDefinitions.GetAnimatorAssetPath(type);
        var pack = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(packPath);

        if (type == POIType.Orc)
            return orcGameplay != null ? orcGameplay : pack;

        if (pack != null && ControllerHasGameplayParams(pack))
            return pack;

        return pack;
    }

    private static bool ControllerHasGameplayParams(RuntimeAnimatorController controller)
    {
        if (controller is not AnimatorController ac)
            return false;

        bool hasSpeed = false;
        bool hasAttack = false;
        foreach (var p in ac.parameters)
        {
            if (p.name == HeroAnimatorParams.Speed) hasSpeed = true;
            if (p.name == HeroAnimatorParams.Attack) hasAttack = true;
        }

        return hasSpeed && hasAttack;
    }
}
