#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[InitializeOnLoad]
public static class TitleSceneSetup
{
    const string TitleScenePath = "Assets/Scenes/title.unity";
    const string MainScenePath = "Assets/Scenes/main.unity";
    const string PlayFromActiveScenePref = "FatesRoll_PlayFromActiveScene";

    static TitleSceneSetup()
    {
        if (EditorPrefs.GetBool(PlayFromActiveScenePref, false))
            return;
        if (!File.Exists(TitleScenePath))
            return;

        ApplyPlayModeStartScene(silent: true);
    }
    const string TitlePrefabPath =
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_DemoScene_Panels/Title.prefab";
    const string TitleStartPrefabPath =
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_DemoScene_Panels/TitleStart.prefab";

    [MenuItem("FatesRoll/Scenes/Setup Title Loading Scene")]
    public static void SetupTitleLoadingScene()
    {
        var titlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TitlePrefabPath);
        var titleStartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TitleStartPrefabPath);
        if (titlePrefab == null || titleStartPrefab == null)
        {
            Debug.LogError("Could not find Title.prefab or TitleStart.prefab under GUI Pro-FantasyRPG.");
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EnsureEventSystem();
        EnsureTitleCamera();

        var canvasGo = new GameObject("TitleCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var canvasRt = canvasGo.GetComponent<RectTransform>();
        canvasRt.localScale = Vector3.one;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var flowGo = new GameObject("TitleFlow");
        var flow = flowGo.AddComponent<TitleFlowController>();

        var loading = PrefabUtility.InstantiatePrefab(titlePrefab, canvasGo.transform) as GameObject;
        var tap = PrefabUtility.InstantiatePrefab(titleStartPrefab, canvasGo.transform) as GameObject;
        loading.name = "Title_Loading";
        tap.name = "TitleStart_Tap";

        StretchFullscreen(loading.GetComponent<RectTransform>());
        StretchFullscreen(tap.GetComponent<RectTransform>());

        GameObject branding = ConsolidateTitleHierarchy(canvasGo.transform, loading, tap);

        var so = new SerializedObject(flow);
        so.FindProperty("loadingPanel").objectReferenceValue = loading;
        so.FindProperty("tapToStartPanel").objectReferenceValue = tap;
        so.FindProperty("sharedBranding").objectReferenceValue = branding;
        so.FindProperty("mainSceneName").stringValue = "main";
        so.FindProperty("mainSceneBuildIndex").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, TitleScenePath);
        SetBuildScenes();
        EditorPrefs.SetBool(PlayFromActiveScenePref, false);
        ApplyPlayModeStartScene(silent: true);
        Selection.activeGameObject = flowGo;

        Debug.Log(
            "Title scene saved. Build order: title (0) → main (1). Press Play from any scene to run the title/loading flow first.");
    }

    [MenuItem("FatesRoll/Scenes/Set Build Order (Title → Main)")]
    public static void SetBuildScenesMenu()
    {
        SetBuildScenes();
        Debug.Log("Build Settings: title first, then main.");
    }

    static void SetBuildScenes()
    {
        var scenes = new List<EditorBuildSettingsScene>();

        if (File.Exists(TitleScenePath))
            scenes.Add(new EditorBuildSettingsScene(TitleScenePath, true));

        if (File.Exists(MainScenePath))
            scenes.Add(new EditorBuildSettingsScene(MainScenePath, true));

        if (scenes.Count == 0)
        {
            Debug.LogWarning("No title or main scene found to add to Build Settings.");
            return;
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        ApplyPlayModeStartScene(silent: true);
    }

    [MenuItem("FatesRoll/Scenes/Always Start Play From Title Scene")]
    public static void EnablePlayModeStartSceneMenu()
    {
        EditorPrefs.SetBool(PlayFromActiveScenePref, false);
        ApplyPlayModeStartScene();
    }

    [MenuItem("FatesRoll/Scenes/Play From Active Scene (Skip Title On Play)")]
    public static void DisablePlayModeStartSceneMenu()
    {
        EditorPrefs.SetBool(PlayFromActiveScenePref, true);
        EditorSceneManager.playModeStartScene = null;
        Debug.Log("Play Mode will use whichever scene is open (no title redirect).");
    }

    static void ApplyPlayModeStartScene(bool silent = false)
    {
        var title = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath);
        if (title == null)
        {
            if (!silent)
                Debug.LogWarning($"Could not set Play Mode start scene — missing {TitleScenePath}.");
            return;
        }

        EditorSceneManager.playModeStartScene = title;
        if (!silent)
            Debug.Log("Play Mode will start from title.unity (loading screen) even when main.unity is open.");
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    static void EnsureTitleCamera()
    {
        if (Camera.main != null)
            return;

        var camGo = new GameObject("TitleCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.1f, 1f);
        cam.orthographic = true;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;
        cam.depth = -10;
        cam.cullingMask = 0;
    }

    [MenuItem("FatesRoll/Scenes/Consolidate Title UI (one logo set)")]
    public static void ConsolidateTitleSceneMenu()
    {
        if (!File.Exists(TitleScenePath))
        {
            Debug.LogWarning("title.unity not found.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No Canvas in title scene.");
            return;
        }

        var loading = GameObject.Find("Title_Loading");
        var tap = GameObject.Find("TitleStart_Tap");
        if (loading == null || tap == null)
        {
            Debug.LogError("Expected Title_Loading and TitleStart_Tap under the canvas.");
            return;
        }

        GameObject branding = ConsolidateTitleHierarchy(canvas.transform, loading, tap);
        var flow = Object.FindAnyObjectByType<TitleFlowController>();
        if (flow != null)
        {
            var so = new SerializedObject(flow);
            so.FindProperty("sharedBranding").objectReferenceValue = branding;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Title UI consolidated: one TitleBranding, one ribbon text, loading = slider only, tap = button + TAP TO START.");
    }

    /// <summary>
    /// One shared logo/ribbon; loading panel keeps only the bar; tap panel keeps only tap UI.
    /// </summary>
    static GameObject ConsolidateTitleHierarchy(Transform canvas, GameObject loadingRoot, GameObject tapRoot)
    {
        UnpackPrefabInstanceRoot(loadingRoot);
        UnpackPrefabInstanceRoot(tapRoot);

        Transform branding = canvas.Find("TitleBranding");
        if (branding == null)
        {
            var brandingGo = new GameObject("TitleBranding", typeof(RectTransform));
            brandingGo.layer = 5;
            branding = brandingGo.transform;
            branding.SetParent(canvas, false);
            StretchFullscreen(branding as RectTransform);
        }
        else
        {
            branding = EnsureBrandingRectTransform(branding).transform;
        }

        // Keep branding from the loading panel; remove duplicates from the tap panel.
        MoveChildToBranding(loadingRoot.transform, branding, "Background");
        MoveChildToBranding(loadingRoot.transform, branding, "Title");
        RemoveDuplicateChild(tapRoot.transform, "Background");
        RemoveDuplicateChild(tapRoot.transform, "Title");
        RemoveDuplicateChild(FindDeepChild(tapRoot.transform, "TapToStart"), "Title");

        branding.SetAsFirstSibling();
        loadingRoot.transform.SetSiblingIndex(1);
        tapRoot.transform.SetSiblingIndex(2);

        StretchFullscreen(branding as RectTransform);
        StretchBrandingBackground(branding);

        return branding.gameObject;
    }

    /// <summary>UI under Canvas must use RectTransform; a plain Transform breaks fullscreen Background layout.</summary>
    static RectTransform EnsureBrandingRectTransform(Transform branding)
    {
        if (branding == null)
            return null;

        if (branding is RectTransform existing)
        {
            branding.gameObject.layer = 5;
            return existing;
        }

        var parent = branding.parent;
        var siblingIndex = branding.GetSiblingIndex();
        var childNames = new List<string>();
        for (int i = 0; i < branding.childCount; i++)
            childNames.Add(branding.GetChild(i).name);

        var brandingGo = new GameObject("TitleBranding", typeof(RectTransform));
        brandingGo.layer = 5;
        var newRt = brandingGo.GetComponent<RectTransform>();
        newRt.SetParent(parent, false);
        newRt.SetSiblingIndex(siblingIndex);
        StretchFullscreen(newRt);

        foreach (var name in childNames)
        {
            var child = branding.Find(name);
            if (child != null)
                child.SetParent(newRt, false);
        }

        Object.DestroyImmediate(branding.gameObject);
        return newRt;
    }

    static void StretchBrandingBackground(Transform branding)
    {
        if (branding == null) return;

        var bg = branding.Find("Background");
        if (bg == null)
            bg = FindDeepChild(branding, "Background");
        if (bg is RectTransform bgRt)
            StretchFullscreen(bgRt);
    }

    static void UnpackPrefabInstanceRoot(GameObject root)
    {
        if (root == null) return;
        if (!PrefabUtility.IsPartOfPrefabInstance(root))
            return;

        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    static void MoveChildToBranding(Transform panelRoot, Transform branding, string childName)
    {
        if (panelRoot == null || branding == null) return;

        Transform child = panelRoot.Find(childName);
        if (child == null)
            child = FindDeepChild(panelRoot, childName);
        if (child == null || child.parent == branding)
            return;

        var canvasRoot = branding.GetComponentInParent<Canvas>()?.gameObject ?? branding.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(canvasRoot, "Consolidate title branding");
        child.SetParent(branding, false);
        StretchFullscreen(child as RectTransform);
    }

    static void RemoveDuplicateChild(Transform searchRoot, string childName)
    {
        if (searchRoot == null) return;

        Transform child = searchRoot.Find(childName);
        if (child == null)
            child = FindDeepChild(searchRoot, childName);
        if (child == null || child.parent.name == "TitleBranding")
            return;

        Undo.DestroyObjectImmediate(child.gameObject);
    }

    static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    [MenuItem("FatesRoll/Scenes/Fix Title Scene Camera")]
    public static void FixTitleSceneCamera()
    {
        if (!File.Exists(TitleScenePath))
        {
            Debug.LogWarning("title.unity not found. Run Setup Title Loading Scene first.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        EnsureTitleCamera();

        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            var rt = canvas.GetComponent<RectTransform>();
            if (rt != null)
                rt.localScale = Vector3.one;
            EditorUtility.SetDirty(canvas);

            var branding = canvas.transform.Find("TitleBranding");
            if (branding != null)
            {
                branding = EnsureBrandingRectTransform(branding).transform;
                StretchFullscreen(branding as RectTransform);
                StretchBrandingBackground(branding);
                branding.SetAsFirstSibling();
            }
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Title scene: Overlay canvas, TitleBranding RectTransform, background stretch, camera.");
    }

    static void StretchFullscreen(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
#endif
