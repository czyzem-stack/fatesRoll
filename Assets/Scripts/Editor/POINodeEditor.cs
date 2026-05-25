using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(POINode))]
public class POINodeEditor : Editor
{
    private static readonly string OrcPrefabPath = "Assets/Monsters/CommonStuffs/Prefab/Wave01/CharacterPBR/OrcPBRDefault.prefab";
    private static readonly string SkeletonPrefabPath = "Assets/Monsters/CommonStuffs/Prefab/Wave01/CharacterPBR/SkeletonPBRDefault.prefab";
    private static readonly string SlimePrefabPath = "Assets/Monsters/CommonStuffs/Prefab/Wave01/CharacterPBR/SlimePBRDefault.prefab";
    private static readonly string HealthBarPrefabPath = "Assets/Prefabs/UI/HealthBar.prefab";

    public override void OnInspectorGUI()
    {
        POINode node = (POINode)target;

        EditorGUI.BeginChangeCheck();
        
        node.type = (POIType)EditorGUILayout.EnumPopup("Enemy Type", node.type);
        
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(node);
            UpdateVisuals(node);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Visuals are now generated as children to support animations. Configure enemy stats in the 'Enemy' component below.", MessageType.Info);
        
        if (GUILayout.Button("Force Refresh Visuals"))
        {
            UpdateVisuals(node);
        }
    }

    private void OnEnable()
    {
        POINode node = (POINode)target;
        // Optional: UpdateVisuals(node) here might be too aggressive if user is just clicking around
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
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"POI: Could not find prefab at {path}");
            return;
        }

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, node.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        node.currentVisual = visual;

        // 3.5 Add HealthBar
        GameObject hbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthBarPrefabPath);
        if (hbPrefab != null)
        {
            GameObject hb = (GameObject)PrefabUtility.InstantiatePrefab(hbPrefab, node.transform);
            hb.transform.localPosition = Vector3.up * 2.8f; 
            
            RectTransform hbRT = hb.GetComponent<RectTransform>();
            hbRT.sizeDelta = new Vector2(1.8f, 0.36f);
            hbRT.pivot = new Vector2(0.5f, 0.5f);
            hbRT.anchorMin = new Vector2(0.5f, 0.5f);
            hbRT.anchorMax = new Vector2(0.5f, 0.5f);

            Canvas hbCanvas = hb.GetComponent<Canvas>();
            if (hbCanvas != null)
            {
                hbCanvas.renderMode = RenderMode.WorldSpace;
                hbCanvas.worldCamera = Camera.main;
            }

            SetupCenteredStretching(hb.transform);
            
            hb.transform.localRotation = Quaternion.identity;
            hb.transform.localScale = Vector3.one; 
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