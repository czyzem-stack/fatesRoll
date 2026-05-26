using UnityEditor;
using UnityEngine;

public static class SpawnManagerSetup
{
    private const string CatalogPath = "Assets/Data/Monsters/MonsterPrefabCatalog.asset";

    [MenuItem("FatesRoll/Enemies/Add Spawn + Stat Managers To Scene")]
    public static void AddManagers()
    {
        if (Object.FindAnyObjectByType<POIManager>() == null)
            POIEnemyMenu.AddPOIManager();

        if (Object.FindAnyObjectByType<EnemyStatManager>() == null)
        {
            var statGo = new GameObject("EnemyStatManager");
            Undo.RegisterCreatedObjectUndo(statGo, "Add EnemyStatManager");
            statGo.AddComponent<EnemyStatManager>();
        }

        SpawnManager spawn = Object.FindAnyObjectByType<SpawnManager>();
        if (spawn == null)
        {
            var spawnGo = new GameObject("SpawnManager");
            Undo.RegisterCreatedObjectUndo(spawnGo, "Add SpawnManager");
            spawn = spawnGo.AddComponent<SpawnManager>();
        }

        var catalog = AssetDatabase.LoadAssetAtPath<MonsterPrefabCatalog>(CatalogPath);
        if (catalog == null)
        {
            MonsterPrefabCatalogBuilder.BuildCatalog();
            catalog = AssetDatabase.LoadAssetAtPath<MonsterPrefabCatalog>(CatalogPath);
        }

        var so = new SerializedObject(spawn);
        so.FindProperty("poiManager").objectReferenceValue = Object.FindAnyObjectByType<POIManager>();
        so.FindProperty("statManager").objectReferenceValue = Object.FindAnyObjectByType<EnemyStatManager>();
        so.FindProperty("monsterCatalog").objectReferenceValue = catalog;
        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = spawn.gameObject;
        Debug.Log("SpawnManager + EnemyStatManager ready. Assign catalog if missing; build catalog via FatesRoll → Enemies → Build Monster Prefab Catalog.");
    }
}
