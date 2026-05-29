#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Adds a GameServices bootstrap to the open scene (reparents existing managers under it when possible).</summary>
public static class GameServicesSetup
{
    [MenuItem("FatesRoll/Setup/Add Game Services Bootstrap")]
    public static void AddGameServicesBootstrap()
    {
        if (Object.FindAnyObjectByType<GameServices>() != null)
        {
            Debug.Log("GameServices already exists in the scene.");
            return;
        }

        var root = new GameObject("GameServices");
        root.AddComponent<GameServices>();

        System.Type[] serviceTypes =
        {
            typeof(GlobalSettings), typeof(DiceSpawner), typeof(POIManager), typeof(SpawnManager),
            typeof(EnergyManager), typeof(LevelManager), typeof(LootManager), typeof(EquipmentManager),
            typeof(EquipmentLootManager),
            typeof(RogueLiteManager), typeof(RunDeathController), typeof(EnemyStatManager)
        };

        foreach (var type in serviceTypes)
        {
            var behaviour = Object.FindAnyObjectByType(type) as Component;
            if (behaviour == null || behaviour.transform.parent == root.transform)
                continue;
            behaviour.transform.SetParent(root.transform, true);
        }

        var services = root.GetComponent<GameServices>();
        if (services != null)
        {
            var hero = Object.FindAnyObjectByType<HeroController>();
            var spawn = Object.FindAnyObjectByType<HeroSpawnPoint>();
            // Hero / spawn stay in the scene hierarchy; wire references only via discovery on play.
            if (hero == null)
                Debug.LogWarning("GameServices: no HeroController in scene — assign on GameServices after play mode test.");
        }

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log("GameServices bootstrap added. Steve and spawn markers are left in place; managers are grouped under GameServices.");
    }
}
#endif
