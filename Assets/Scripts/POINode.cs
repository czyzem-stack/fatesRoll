using UnityEngine;

[ExecuteAlways]
public class POINode : MonoBehaviour
{
    [Header("POI Selection")]
    public POIType type = POIType.Orc;

    [SerializeField, HideInInspector] 
    private POIType currentType = (POIType)(-1);

    private void Awake()
    {
        gameObject.tag = "POI";
    }

    private void Update()
    {
        if (type != currentType)
        {
            RefreshVisuals();
        }
    }

    public void RefreshVisuals()
    {
        currentType = type;
        
        // 1. Clear all existing content
        var children = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in transform) children.Add(child.gameObject);
        foreach (var child in children)
        {
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        // 2. Get prefab from registry
        var manager = POIManager.Instance;
        if (manager == null) return;

        GameObject prefab = manager.GetPrefabForType(type);
        if (prefab == null) return;

        // 3. Instantiate and Consolidate
        GameObject tempInstance;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            tempInstance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
            UnityEditor.PrefabUtility.UnpackPrefabInstance(tempInstance, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
        }
        else
        {
            tempInstance = Instantiate(prefab);
        }
#else
        tempInstance = Instantiate(prefab);
#endif

        // Copy Animator
        var prefabAnim = tempInstance.GetComponent<Animator>();
        if (prefabAnim != null)
        {
            var nodeAnim = gameObject.GetComponent<Animator>();
            if (nodeAnim == null) nodeAnim = gameObject.AddComponent<Animator>();
            
            nodeAnim.runtimeAnimatorController = prefabAnim.runtimeAnimatorController;
            nodeAnim.avatar = prefabAnim.avatar;
            nodeAnim.applyRootMotion = false; // Disable root motion
            nodeAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            nodeAnim.updateMode = prefabAnim.updateMode;

            // nodeAnim.hideFlags = HideFlags.HideInInspector; // Removed
        }

        // Move children and HIDE them from the hierarchy slop
        var prefabChildren = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in tempInstance.transform) prefabChildren.Add(t);

        foreach (Transform t in prefabChildren)
        {
            t.SetParent(transform);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            
            // Hide everything inside the node from the hierarchy
            t.gameObject.hideFlags = HideFlags.HideInHierarchy;
        }

        if (Application.isPlaying) Destroy(tempInstance);
        else DestroyImmediate(tempInstance);

        if (type == POIType.Orc)
        {
            if (gameObject.GetComponent<EnemyCombatant>() == null)
            {
                gameObject.AddComponent<EnemyCombatant>();
            }
        }

        SetLayerRecursive(gameObject, 8);
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        // Skip UI elements and LineRenderers
        if (obj.GetComponent<Canvas>() != null || 
            obj.GetComponent<RectTransform>() != null || 
            obj.GetComponent<LineRenderer>() != null)
        {
            int target = (obj.GetComponent<LineRenderer>() != null) ? 0 : 5;
            SetLayerRecursiveInternal(obj, target);
            return;
        }

        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            obj.layer = layer;
        }
        else
        {
            obj.layer = 0;
        }

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    private void SetLayerRecursiveInternal(GameObject obj, int layer)
{
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursiveInternal(child.gameObject, layer);
        }
    }
}
