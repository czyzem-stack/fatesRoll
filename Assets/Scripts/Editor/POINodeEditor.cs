using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(POINode))]
public class POINodeEditor : Editor
{
    private static readonly string OrcPrefabPath = "Assets/Monsters/CommonStuffs/Prefab/Wave01/CharacterPBR/OrcPBRDefault.prefab";
    private static readonly string SkeletonPrefabPath = "Assets/Monsters/CommonStuffs/Prefab/Wave01/CharacterPBR/SkeletonPBRDefault.prefab";
    private static readonly string SlimePrefabPath = "Assets/Monsters/CommonStuffs/Prefab/Wave01/CharacterPBR/SlimePBRDefault.prefab";
    private static readonly string ChestPrefabPath = "Assets/Synty/PolygonNature/Prefabs/Props/SM_Prop_Chest_Wood_01.prefab";
    private static readonly string HealthBarPrefabPath = "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_Component_Slider/Slider_Border_Tapered_02_Green.prefab";

    public override void OnInspectorGUI()
{
        POINode node = (POINode)target;

        EditorGUI.BeginChangeCheck();
        
        node.type = (POIType)EditorGUILayout.EnumPopup("Enemy Type", node.type);
        node.order = EditorGUILayout.IntField("Visit Order", node.order);
        
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(node);
            if (node.type != POIType.TreasureChest)
                node.isTreasureChest = false;
            else
                node.isTreasureChest = true;
            UpdateVisuals(node);
        }

        if (node.IsTreasureChest)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Treasure chest loot", EditorStyles.boldLabel);
            node.ftueLootIndex = EditorGUILayout.IntField("FTUE Loot Index (-1 = random)", node.ftueLootIndex);
            node.ftueForcedOptionA = (EquipmentItemDefinition)EditorGUILayout.ObjectField(
                "FTUE Forced Option A", node.ftueForcedOptionA, typeof(EquipmentItemDefinition), false);
            node.ftueForcedOptionB = (EquipmentItemDefinition)EditorGUILayout.ObjectField(
                "FTUE Forced Option B", node.ftueForcedOptionB, typeof(EquipmentItemDefinition), false);
            EditorGUILayout.HelpBox(
                "Defeating this POI opens an equipment popup (like level-up). Coins are not dropped. " +
                "Set forced options for tutorial chests; leave empty for random weapon vs armor.",
                MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Visuals are now generated as children to support animations. Configure enemy stats in the 'Enemy' component below.", MessageType.Info);
        
        if (GUILayout.Button("Force Refresh Visuals"))
        {
            UpdateVisuals(node);
        }

        if (node.IsTreasureChest && GUILayout.Button("Snap Chest To Ground"))
        {
            PoiVisualPlacer.SnapToGround(node.transform);
            if (node.currentVisual != null)
                PoiVisualPlacer.PlaceTreasureChestVisual(node.transform, node.currentVisual);
            EditorUtility.SetDirty(node);
        }
    }

    private void OnEnable()
    {
        POINode node = (POINode)target;
        // Optional: UpdateVisuals(node) here might be too aggressive if user is just clicking around
    }

    public static void RefreshVisuals(POINode node)
    {
        var editor = CreateEditor(node) as POINodeEditor;
        if (editor != null)
        {
            editor.UpdateVisuals(node);
            DestroyImmediate(editor);
        }
    }

    private void UpdateVisuals(POINode node)
    {
        // 1. Clean up existing visuals (children)
        // We look for any child that isn't the POI itself
        for (int i = node.transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(node.transform.GetChild(i).gameObject);
        }

        // 2. Clean up components on root that were used for single-object mode
        MeshFilter mf = node.GetComponent<MeshFilter>();
        if (mf != null) DestroyImmediate(mf);
        
        MeshRenderer mr = node.GetComponent<MeshRenderer>();
        if (mr != null) DestroyImmediate(mr);

        // 3. Instantiate the correct prefab
        string path = "";
        switch (node.type)
        {
            case POIType.Orc: path = OrcPrefabPath; break;
            case POIType.Skeleton: path = SkeletonPrefabPath; break;
            case POIType.Slime: path = SlimePrefabPath; break;
            case POIType.TreasureChest: path = ChestPrefabPath; break;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"POI: Could not find prefab at {path}");
            return;
        }

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, node.transform);
        visual.transform.localRotation = Quaternion.identity;
        node.currentVisual = visual;

        if (node.type == POIType.TreasureChest)
        {
            node.isTreasureChest = true;
            PoiVisualPlacer.SnapToGround(node.transform);
            PoiVisualPlacer.PlaceTreasureChestVisual(node.transform, visual);
            ConfigureChestEnemy(node);
        }
        else
        {
            visual.transform.localPosition = Vector3.zero;
        }

        // 3.5 Add HealthBar
        GameObject hbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthBarPrefabPath);
        if (hbPrefab != null)
        {
            GameObject hb = (GameObject)PrefabUtility.InstantiatePrefab(hbPrefab, node.transform);
            hb.transform.localPosition = Vector3.up * 3.0f; 
            
            RectTransform hbRT = hb.GetComponent<RectTransform>();
            // Use pixel-scale dimensions to avoid offsetting children into negative space
            hbRT.sizeDelta = new Vector2(240, 40); 
            hbRT.pivot = new Vector2(0.5f, 0.5f);
            hbRT.anchorMin = new Vector2(0.5f, 0.5f);
            hbRT.anchorMax = new Vector2(0.5f, 0.5f);

            Canvas hbCanvas = hb.GetComponent<Canvas>();
            if (hbCanvas == null) hbCanvas = hb.AddComponent<Canvas>();
            
            if (hbCanvas != null)
            {
                hbCanvas.renderMode = RenderMode.WorldSpace;
                hbCanvas.worldCamera = Camera.main;
            }

            // Standard World Space UI scale
            hb.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            hb.transform.localRotation = Quaternion.identity;
        }

        // 4. Ensure root has Enemy and NavMeshAgent
        if (node.GetComponent<Enemy>() == null) node.gameObject.AddComponent<Enemy>();
        if (node.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
        {
            node.gameObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
        }

        // 5. Initialize the enemy to find the new animator and set proper agent settings
        if (Application.isPlaying)
        {
            node.InitializeEnemy();
        }

        Debug.Log($"POI visual updated to {node.type} with child prefab for animations.");
    }

    private static void ConfigureChestEnemy(POINode node)
    {
        var enemy = node.GetComponent<Enemy>();
        if (enemy == null)
            enemy = node.gameObject.AddComponent<Enemy>();

        enemy.strength = 1f;
        enemy.agility = 1f;
        enemy.vitality = 5f;
        enemy.luck = 1f;
        enemy.patrolRadius = 0f;
        enemy.patrolSpeed = 0f;

        var agent = node.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = 0f;
            agent.angularSpeed = 0f;
        }

        EditorUtility.SetDirty(enemy);
    }

    private void SetupCenteredStretching(Transform parent, bool applyMargin = false)
    {
        foreach (Transform child in parent)
        {
            RectTransform childRT = child.GetComponent<RectTransform>();
            if (childRT != null)
            {
                if (child.name != "Fill") 
                {
                    childRT.anchorMin = Vector2.zero;
                    childRT.anchorMax = Vector2.one;
                    
                    if (applyMargin)
                    {
                        // Apply a small 0.02m margin for the border effect
                        childRT.offsetMin = new Vector2(0.04f, 0.04f);
                        childRT.offsetMax = new Vector2(-0.04f, -0.04f);
                    }
                    else
                    {
                        childRT.sizeDelta = Vector2.zero;
                        childRT.anchoredPosition = Vector2.zero;
                    }
                }
                childRT.pivot = new Vector2(0.5f, 0.5f);
                
                // Recursively check deeper (but only apply margin to top-level children of the root)
                SetupCenteredStretching(child, false);
            }
        }
    }
}