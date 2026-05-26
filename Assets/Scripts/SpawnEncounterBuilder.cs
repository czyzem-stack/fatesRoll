using UnityEngine;
using UnityEngine.AI;

/// <summary>Spawns a combat encounter under a SpawnNode (separate from visit-order POIs).</summary>
public static class SpawnEncounterBuilder
{
    public static GameObject Build(SpawnNode node, POIType type, MonsterPrefabCatalog catalog)
    {
        if (node == null || catalog == null) return null;

        node.ClearEncounter();

        GameObject prefab = catalog.GetPrefab(type);
        if (prefab == null)
        {
            Debug.LogError($"SpawnEncounterBuilder: no prefab for {type}.");
            return null;
        }

        var root = new GameObject($"Encounter_{type}");
        root.transform.SetParent(node.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

        if (root.GetComponent<Enemy>() == null)
            root.AddComponent<Enemy>();
        if (root.GetComponent<NavMeshAgent>() == null)
            root.AddComponent<NavMeshAgent>();

        GameObject visual = Object.Instantiate(prefab, root.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        POIVisualBuilder.ApplyMonsterAnimator(visual, type, catalog);
        GameObject hbCache = null;
        POIVisualBuilder.AddHealthBarTo(root.transform, null, ref hbCache);

        node.activeEncounter = root;
        node.RecordSpawnedType(type);
        return root;
    }

    public static GameObject BuildChest(SpawnNode node)
    {
        if (node == null) return null;

        node.ClearEncounter();

        var root = new GameObject("Encounter_TreasureChest");
        root.transform.SetParent(node.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

        var poi = root.AddComponent<POINode>();
        poi.type = POIType.TreasureChest;
        poi.isTreasureChest = true;

        POIVisualBuilder.BuildVisuals(poi);

        node.activeEncounter = root;
        return root;
    }
}
