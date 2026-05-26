using UnityEngine;
using UnityEditor;

public static class POIEnemyMenu
{
    [MenuItem("FatesRoll/Enemies/Add POI Manager To Scene")]
    public static void AddPOIManager()
    {
        if (Object.FindAnyObjectByType<POIManager>() != null)
        {
            Debug.Log("POIManager already exists in the scene.");
            return;
        }

        var go = new GameObject("POIManager");
        Undo.RegisterCreatedObjectUndo(go, "Add POI Manager");
        go.AddComponent<POIManager>();
        Selection.activeGameObject = go;
    }

    [MenuItem("FatesRoll/Enemies/Add Spawn Node")]
    public static void AddSpawnNode()
    {
        Vector3 pos = SceneView.lastActiveSceneView != null
            ? SceneView.lastActiveSceneView.pivot
            : Vector3.zero;

        var go = new GameObject("SpawnNode");
        Undo.RegisterCreatedObjectUndo(go, "Add Spawn Node");
        go.transform.position = pos;
        PoiVisualPlacer.SnapToGround(go.transform);
        go.AddComponent<SpawnNode>();
        Selection.activeGameObject = go;
        Debug.Log("SpawnNode placed — used for random enemies after all visit-order POIs are cleared.");
    }

    [MenuItem("FatesRoll/Enemies/Add Treasure Chest Spawn Node")]
    public static void AddTreasureChestSpawnNode()
    {
        Vector3 pos = SceneView.lastActiveSceneView != null
            ? SceneView.lastActiveSceneView.pivot
            : Vector3.zero;

        var go = new GameObject("SpawnNode_TreasureChest");
        Undo.RegisterCreatedObjectUndo(go, "Add Treasure Chest Spawn Node");
        go.transform.position = pos;
        PoiVisualPlacer.SnapToGround(go.transform);
        var node = go.AddComponent<SpawnNode>();
        node.spawnKind = SpawnNodeKind.TreasureChest;
        Selection.activeGameObject = go;
        Debug.Log("Treasure chest SpawnNode — always spawns equipment chests (visit POIs or random pool).");
    }

    public static void CreateMonsterPOI(POIType type, int visitOrder = 0)
    {
        Vector3 pos = SceneView.lastActiveSceneView != null
            ? SceneView.lastActiveSceneView.pivot
            : Vector3.zero;

        var poiGo = new GameObject($"POI_{MonsterPOIDefinitions.GetDisplayName(type)}_{visitOrder}");
        Undo.RegisterCreatedObjectUndo(poiGo, "Create POI");

        poiGo.transform.position = pos;
        PoiVisualPlacer.SnapToGround(poiGo.transform);

        var node = poiGo.AddComponent<POINode>();
        node.type = type;
        node.order = visitOrder;

        POIVisualBuilder.BuildVisuals(node);
        EditorUtility.SetDirty(node);

        Selection.activeGameObject = poiGo;
        Debug.Log($"Visit POI {poiGo.name} (order {visitOrder}). Set visit order to any value — not tied to random spawns.");
    }

    [MenuItem("FatesRoll/Enemies/Add Orc POI")]
    public static void AddOrcPOI() => CreateMonsterPOI(POIType.Orc);

    [MenuItem("FatesRoll/Enemies/Add Skeleton POI")]
    public static void AddSkeletonPOI() => CreateMonsterPOI(POIType.Skeleton);

    [MenuItem("FatesRoll/Enemies/Add Slime POI")]
    public static void AddSlimePOI() => CreateMonsterPOI(POIType.Slime);

    [MenuItem("FatesRoll/Enemies/Add Bat POI")]
    public static void AddBatPOI() => CreateMonsterPOI(POIType.Bat);

    [MenuItem("FatesRoll/Enemies/Add Dragon POI")]
    public static void AddDragonPOI() => CreateMonsterPOI(POIType.Dragon);

    [MenuItem("FatesRoll/Enemies/Add Evil Mage POI")]
    public static void AddEvilMagePOI() => CreateMonsterPOI(POIType.EvilMage);

    [MenuItem("FatesRoll/Enemies/Add Golem POI")]
    public static void AddGolemPOI() => CreateMonsterPOI(POIType.Golem);

    [MenuItem("FatesRoll/Enemies/Add Monster Plant POI")]
    public static void AddMonsterPlantPOI() => CreateMonsterPOI(POIType.MonsterPlant);

    [MenuItem("FatesRoll/Enemies/Add Spider POI")]
    public static void AddSpiderPOI() => CreateMonsterPOI(POIType.Spider);

    [MenuItem("FatesRoll/Enemies/Add Turtle Shell POI")]
    public static void AddTurtleShellPOI() => CreateMonsterPOI(POIType.TurtleShell);
}
