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
        
        // 1. Clear all existing content, but KEEP the UI and Hidden components
        var children = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in transform) 
        {
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

        // Move children and HIDE them
        var prefabChildren = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in tempInstance.transform) prefabChildren.Add(t);

        foreach (Transform t in prefabChildren)
        {
            t.SetParent(transform);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.gameObject.hideFlags = HideFlags.HideInHierarchy;
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
                // Basic enemy stats
                stats.strength = 8;
                stats.agility = 8;
                stats.vitality = 8;
                stats.luck = 5;
            }

            // Setup root as Canvas and integrate UI
            EnsureRootCanvas();
        }
else
        {
            RemoveUI();
        }

        SetLayerRecursive(gameObject, 8);
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

    private void EnsureRootCanvas()
    {
        // 1. Ensure root has Canvas components
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            var rt = GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1, 0.2f);
            rt.localScale = Vector3.one;
        }

        // 2. Ensure UI children exist and are hidden
        if (transform.Find("Slider") == null)
        {
#if UNITY_EDITOR
            string prefabPath = "Assets/Prefabs/UI/HealthBar.prefab";
            GameObject hbPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (hbPrefab != null)
            {
                GameObject tempHB = Instantiate(hbPrefab);
                
                // Load overlay material
                Material overlayMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UIOverlay.mat");

                // Move children to root
                var children = new System.Collections.Generic.List<Transform>();
                foreach (Transform child in tempHB.transform) children.Add(child);
                
                foreach (var child in children)
                {
                    child.SetParent(transform);
                    child.localPosition = Vector3.up * 2.5f;
                    child.localRotation = Quaternion.identity;
                    child.gameObject.hideFlags = HideFlags.HideInHierarchy;

                    // Apply overlay material to images
                    var img = child.GetComponent<UnityEngine.UI.Image>();
                    if (img != null && overlayMat != null) img.material = overlayMat;

                    // Check children (like Slider Fill)
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
        else
        {
            // Enforce hiding and material if they exist
            var bg = transform.Find("Background");
            var slider = transform.Find("Slider");
            
            if (bg != null) 
            {
                bg.gameObject.hideFlags = HideFlags.HideInHierarchy;
                bg.localPosition = Vector3.up * 2.5f;
            }
            if (slider != null) 
            {
                slider.gameObject.hideFlags = HideFlags.HideInHierarchy;
                slider.localPosition = Vector3.up * 2.5f;
            }
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

        // Clean up UI components
        var raycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (raycaster != null) DestroyImmediate(raycaster);
        var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
        if (scaler != null) DestroyImmediate(scaler);
        var canvas = GetComponent<Canvas>();
        if (canvas != null) DestroyImmediate(canvas);

        // Clean up UI children
        var bg = transform.Find("Background");
        if (bg != null) DestroyImmediate(bg.gameObject);
        var slider = transform.Find("Slider");
        if (slider != null) DestroyImmediate(slider.gameObject);
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        // UI layer logic
        bool isUIElement = obj.GetComponent<Canvas>() != null || 
                          obj.GetComponent<UnityEngine.UI.Image>() != null || 
                          obj.GetComponent<UnityEngine.UI.Slider>() != null ||
                          obj.name == "Background" || obj.name == "Slider";

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
