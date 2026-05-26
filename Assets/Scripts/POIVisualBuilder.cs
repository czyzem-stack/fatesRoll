using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Builds POI child visuals and gameplay components (editor + runtime respawn).</summary>
public static class POIVisualBuilder
{
    public const string ChestPrefabPath =
        "Assets/Synty/PolygonNature/Prefabs/Props/SM_Prop_Chest_Wood_01.prefab";
    public const string HealthBarPrefabPath =
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_Component_Slider/Slider_Border_Tapered_02_Green.prefab";
    public const string GameplayAnimatorFallbackPath = "Assets/OrcAnimator.controller";

    public static void BuildVisuals(POINode node)
    {
        if (node == null) return;

        ClearVisualChildren(node, useDestroy: Application.isPlaying);

        string path = node.IsTreasureChest
            ? ChestPrefabPath
            : MonsterPOIDefinitions.GetPrefabAssetPath(node.type);

        GameObject prefab = LoadPrefab(path);
        if (prefab == null)
        {
            Debug.LogError($"POI: Could not load prefab at {path}");
            return;
        }

        node.monsterVisualPrefab = prefab;

        GameObject visual = InstantiateVisual(prefab, node.transform);
        visual.transform.localRotation = Quaternion.identity;
        node.currentVisual = visual;

        if (node.IsTreasureChest)
        {
            node.isTreasureChest = true;
            PoiVisualPlacer.SnapToGround(node.transform);
            PoiVisualPlacer.PlaceTreasureChestVisual(node.transform, visual);
            ConfigureChestEnemy(node);
        }
        else
        {
            visual.transform.localPosition = Vector3.zero;
            ApplyMonsterAnimator(visual, node.type, MonsterAnimatorUtility.ResolveCatalog());
        }

        if (!node.IsTreasureChest)
            AddHealthBar(node);
        EnsureEnemyComponents(node);

        if (Application.isPlaying)
            node.InitializeEnemy();
    }

    public static void ClearVisualChildren(POINode node, bool useDestroy = false)
    {
        for (int i = node.transform.childCount - 1; i >= 0; i--)
        {
            var child = node.transform.GetChild(i).gameObject;
            if (useDestroy)
                Object.Destroy(child);
            else
                Object.DestroyImmediate(child);
        }

        var mf = node.GetComponent<MeshFilter>();
        if (mf != null) Object.DestroyImmediate(mf);
        var mr = node.GetComponent<MeshRenderer>();
        if (mr != null) Object.DestroyImmediate(mr);
    }

    private static GameObject LoadPrefab(string path)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }

    private static GameObject InstantiateVisual(GameObject prefab, Transform parent)
    {
#if UNITY_EDITOR
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#else
        return Object.Instantiate(prefab, parent);
#endif
    }

    public static void ApplyMonsterAnimator(
        GameObject visualRoot,
        POIType type,
        MonsterPrefabCatalog catalog)
    {
        MonsterAnimatorUtility.ApplyToVisual(visualRoot, type, catalog);
    }

    public static void AddHealthBar(POINode node) =>
        AddHealthBarTo(node.transform, node.healthBarPrefab, ref node.healthBarPrefab);

    public static void AddHealthBarTo(Transform parent, GameObject hbPrefab, ref GameObject cachedPrefab)
    {
#if UNITY_EDITOR
        if (hbPrefab == null)
            hbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthBarPrefabPath);
        if (hbPrefab != null && cachedPrefab == null)
            cachedPrefab = hbPrefab;
#endif
        if (hbPrefab == null || parent == null) return;

        var canvasRoot = new GameObject("HealthBar");
        canvasRoot.transform.SetParent(parent, false);
        canvasRoot.transform.localPosition = Vector3.up * 3.0f;
        canvasRoot.transform.localRotation = Quaternion.identity;
        canvasRoot.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        var canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null)
            canvas.worldCamera = Camera.main;

#if UNITY_EDITOR
        var hb = (GameObject)PrefabUtility.InstantiatePrefab(hbPrefab, canvasRoot.transform);
#else
        var hb = Object.Instantiate(hbPrefab, canvasRoot.transform);
#endif
        if (hb == null) return;

        hb.transform.localPosition = Vector3.zero;
        hb.transform.localRotation = Quaternion.identity;
        hb.transform.localScale = Vector3.one;

        var hbRT = hb.GetComponent<RectTransform>();
        if (hbRT != null)
        {
            hbRT.sizeDelta = new Vector2(240, 40);
            hbRT.pivot = new Vector2(0.5f, 0.5f);
            hbRT.anchorMin = new Vector2(0.5f, 0.5f);
            hbRT.anchorMax = new Vector2(0.5f, 0.5f);
            hbRT.anchoredPosition = Vector2.zero;
        }
    }

    private static void EnsureEnemyComponents(POINode node)
    {
        if (node.GetComponent<Enemy>() == null)
            node.gameObject.AddComponent<Enemy>();

        if (node.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
            node.gameObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
    }

    private static void ConfigureChestEnemy(POINode node)
    {
        var enemy = node.GetComponent<Enemy>() ?? node.gameObject.AddComponent<Enemy>();
        enemy.ConfigureAsTreasureChest();
    }
}
