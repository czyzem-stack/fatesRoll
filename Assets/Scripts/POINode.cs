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
        
        // 1. Clear all existing content, but KEEP the UI children
        var children = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in transform) 
        {
            if (child.name == "HealthBar_Canvas") continue;
            // Legacy cleanup
            if (child.name == "Background" || child.name == "Slider") continue;
            children.Add(child.gameObject);
        }

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
            nodeAnim.applyRootMotion = false;
            nodeAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            nodeAnim.updateMode = prefabAnim.updateMode;
        }

        // Move children to root
        var prefabChildren = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in tempInstance.transform) prefabChildren.Add(t);

        foreach (Transform t in prefabChildren)
        {
            t.SetParent(transform);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            // Removed HideFlags to ensure compatibility with all systems and avoid flickering
            t.gameObject.hideFlags = HideFlags.None;
        }

        if (Application.isPlaying) Destroy(tempInstance);
        else DestroyImmediate(tempInstance);

        // 4. Handle Enemy Components and Health Bar
        if (IsEnemy(type))
        {
            if (gameObject.GetComponent<EnemyCombatant>() == null)
            {
                gameObject.AddComponent<EnemyCombatant>();
            }

            if (gameObject.GetComponent<PlayerStats>() == null)
            {
                var stats = gameObject.AddComponent<PlayerStats>();
                stats.strength = 8;
                stats.agility = 8;
                stats.vitality = 8;
                stats.luck = 5;
            }

            // Setup separate HealthBar Canvas
            EnsureHealthBarUI();
        }
        else
        {
            RemoveUI();
        }

        SetLayerRecursive(gameObject, 8);
        
        // Final cleanup of legacy components on root
        var rootCanvas = GetComponent<Canvas>();
        if (rootCanvas != null)
        {
            if (Application.isPlaying) Destroy(rootCanvas);
            else DestroyImmediate(rootCanvas);
            
            var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null) { if (Application.isPlaying) Destroy(scaler); else DestroyImmediate(scaler); }
            var raycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null) { if (Application.isPlaying) Destroy(raycaster); else DestroyImmediate(raycaster); }
        }
    }

    private bool IsEnemy(POIType type)
    {
        return type == POIType.Orc || 
               type == POIType.Skeleton || 
               type == POIType.Slime || 
               type == POIType.Cyclops || 
               type == POIType.Beholder ||
               type == POIType.BishopKnight;
    }

    private void EnsureHealthBarUI()
    {
        Transform canvasTransform = transform.Find("HealthBar_Canvas");
        if (canvasTransform == null)
        {
            GameObject go = new GameObject("HealthBar_Canvas");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.up * 2.5f;
            canvasTransform = go.transform;
        }

        var canvas = canvasTransform.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = canvasTransform.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasTransform.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasTransform.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            var rt = canvasTransform.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1, 0.2f);
            rt.localScale = Vector3.one;
        }

        // Move existing UI elements into the canvas if they are still on root
        var bg = transform.Find("Background");
        if (bg != null) bg.SetParent(canvasTransform);
        var slider = transform.Find("Slider");
        if (slider != null) slider.SetParent(canvasTransform);

        if (canvasTransform.Find("Slider") == null)
        {
#if UNITY_EDITOR
            string prefabPath = "Assets/Prefabs/UI/HealthBar.prefab";
            GameObject hbPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (hbPrefab != null)
            {
                GameObject tempHB = Instantiate(hbPrefab);
                Material overlayMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UIOverlay.mat");

                var children = new System.Collections.Generic.List<Transform>();
                foreach (Transform child in tempHB.transform) children.Add(child);
                
                foreach (var child in children)
                {
                    child.SetParent(canvasTransform);
                    child.localPosition = Vector3.zero;
                    child.localRotation = Quaternion.identity;

                    var img = child.GetComponent<UnityEngine.UI.Image>();
                    if (img != null && overlayMat != null) img.material = overlayMat;

                    foreach (Transform grandChild in child.GetComponentsInChildren<Transform>(true))
                    {
                        var gcImg = grandChild.GetComponent<UnityEngine.UI.Image>();
                        if (gcImg != null && overlayMat != null) gcImg.material = overlayMat;
                    }
                }
                
                DestroyImmediate(tempHB);
            }
#endif
        }
    }

    private void RemoveUI()
    {
        var ec = gameObject.GetComponent<EnemyCombatant>();
        if (ec != null) 
        {
            if (Application.isPlaying) Destroy(ec);
            else DestroyImmediate(ec);
        }

        var canvasTransform = transform.Find("HealthBar_Canvas");
        if (canvasTransform != null)
        {
            if (Application.isPlaying) Destroy(canvasTransform.gameObject);
            else DestroyImmediate(canvasTransform.gameObject);
        }

        // Clean up legacy UI children
        var bg = transform.Find("Background");
        if (bg != null) { if (Application.isPlaying) Destroy(bg.gameObject); else DestroyImmediate(bg.gameObject); }
        var slider = transform.Find("Slider");
        if (slider != null) { if (Application.isPlaying) Destroy(slider.gameObject); else DestroyImmediate(slider.gameObject); }
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        // UI layer logic - simplified as they are now in a child Canvas
        bool isUIElement = obj.name == "HealthBar_Canvas" || 
                          obj.GetComponent<UnityEngine.UI.Image>() != null || 
                          obj.GetComponent<UnityEngine.UI.Slider>() != null;

        if (isUIElement)
        {
            obj.layer = 5; // UI
        }
        else if (obj.GetComponent<Renderer>() != null)
        {
            obj.layer = layer; // Highlight/Character
        }
        else
        {
            obj.layer = 0; // Default
        }

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    }
