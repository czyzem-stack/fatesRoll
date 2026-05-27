using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Title screen flow: loading bar (Title prefab) → tap to start (TitleStart) → main scene.
/// Preloads main with <see cref="AsyncOperation.allowSceneActivation"/> false to reduce Play hiccup.
/// </summary>
public class TitleFlowController : MonoBehaviour
{
    private const string DefaultMainScene = "main";

    [Header("Panels")]
    [Tooltip("Loading phase root (slider only after scene consolidate). Hidden when tap appears.")]
    [SerializeField] private GameObject loadingPanel;
    [Tooltip("Tap-to-start phase root. Shown after loading finishes.")]
    [SerializeField] private GameObject tapToStartPanel;
    [Tooltip("Shared logo/ribbon/background — stays visible in both phases (optional).")]
    [SerializeField] private GameObject sharedBranding;

    [Header("Loading UI (auto-found if empty)")]
    [SerializeField] private Slider loadingSlider;
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("Scene")]
    [SerializeField] private string mainSceneName = DefaultMainScene;
    [SerializeField] private int mainSceneBuildIndex = 1;

    [Header("Timing")]
    [SerializeField] private float minLoadingSeconds = 1.25f;
    [SerializeField] private float sliderLerpSpeed = 2.5f;

    [Header("Title branding")]
    [Tooltip("Optional: set ribbon text from code. Leave empty to use whatever you typed on the TMP in the scene/prefab.")]
    [SerializeField] private string ribbonLabelOverride = "";
    [Tooltip("Hide baked Sample_TitleTextt sprite logos (Image_Title_Fantasy / Image_Tile_RPG).")]
    [SerializeField] private bool hideDemoTitleSprites = false;

    private AsyncOperation _mainLoadOp;
    private Button _startButton;
    private bool _readyToStart;
    private bool _startedMain;
    private float _displayProgress;

    private void Awake()
    {
        EnsureTitleCamera();

        if (sharedBranding == null)
            sharedBranding = GameObject.Find("TitleBranding");

        if (sharedBranding != null)
        {
            sharedBranding.SetActive(true);
            EnsureSharedBrandingLayout();
        }

        if (tapToStartPanel != null)
            tapToStartPanel.SetActive(false);
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        CacheUiReferences();
        ApplyTitleBranding();
        WireStartButton();
        PlayTitleAmbienceParticles();
    }

    private void ApplyTitleBranding()
    {
        if (!string.IsNullOrWhiteSpace(ribbonLabelOverride))
        {
            foreach (var tmp in FindAllRibbonLabelTexts())
                tmp.text = ribbonLabelOverride;
        }

        if (!hideDemoTitleSprites)
            return;

        SetActiveByName(sharedBranding, "Image_Title_Fantasy", false);
        SetActiveByName(sharedBranding, "Image_Tile_RPG", false);
        SetActiveByName(loadingPanel, "Image_Title_Fantasy", false);
        SetActiveByName(loadingPanel, "Image_Tile_RPG", false);
        SetActiveByName(tapToStartPanel, "Image_Title_Fantasy", false);
        SetActiveByName(tapToStartPanel, "Image_Tile_RPG", false);
    }

    private IEnumerable<TextMeshProUGUI> FindAllRibbonLabelTexts()
    {
        var found = new List<TextMeshProUGUI>();
        foreach (var root in new[] { sharedBranding, loadingPanel, tapToStartPanel })
        {
            if (root == null) continue;
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (!IsRibbonLabelText(tmp))
                    continue;
                if (!found.Contains(tmp))
                    found.Add(tmp);
            }
        }
        return found;
    }

    private static bool IsRibbonLabelText(TextMeshProUGUI tmp)
    {
        if (tmp == null) return false;
        if (tmp.gameObject.name.Contains("MOBILE", System.StringComparison.OrdinalIgnoreCase))
            return true;
        return tmp.transform.parent != null &&
               tmp.transform.parent.name.Contains("TitleRibbon", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Fullscreen scenic art must live under a RectTransform on the canvas (not a plain Transform).</summary>
    private void EnsureSharedBrandingLayout()
    {
        if (sharedBranding == null) return;

        var canvas = sharedBranding.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var canvasRt = canvas.GetComponent<RectTransform>();
        if (canvasRt == null) return;

        Transform bg = sharedBranding.transform.Find("Background");
        if (bg == null) return;

        if (sharedBranding.GetComponent<RectTransform>() == null)
        {
            bg.SetParent(canvasRt, false);
            bg.SetAsFirstSibling();
        }

        if (bg is RectTransform bgRt)
            StretchFullscreen(bgRt);

        var brandingRt = sharedBranding.GetComponent<RectTransform>();
        if (brandingRt != null)
            StretchFullscreen(brandingRt);
    }

    private static void StretchFullscreen(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static void SetActiveByName(GameObject root, string objectName, bool active)
    {
        if (root == null) return;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == objectName)
                t.gameObject.SetActive(active);
        }
    }

    private void Start()
    {
        StartCoroutine(TitleFlowRoutine());
    }

    private void CacheUiReferences()
    {
        if (loadingSlider == null && loadingPanel != null)
            loadingSlider = loadingPanel.GetComponentInChildren<Slider>(true);

        if (loadingText == null && loadingPanel != null)
        {
            foreach (var tmp in loadingPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.gameObject.name.Contains("Loading", System.StringComparison.OrdinalIgnoreCase))
                {
                    loadingText = tmp;
                    break;
                }
            }
        }

        if (loadingSlider != null)
            loadingSlider.value = 0f;
    }

    private void WireStartButton()
    {
        if (tapToStartPanel == null) return;

        _startButton = tapToStartPanel.GetComponentInChildren<Button>(true);
        if (_startButton != null)
            _startButton.onClick.AddListener(OnTapToStart);
    }

    private IEnumerator TitleFlowRoutine()
    {
        float flowStart = Time.unscaledTime;
        _displayProgress = 0f;

        _mainLoadOp = LoadMainAsync();
        if (_mainLoadOp == null)
        {
            Debug.LogError($"TitleFlowController: could not start loading scene '{mainSceneName}'. Add it to Build Settings.");
            yield break;
        }

        while (true)
        {
            float target = Mathf.Clamp01(_mainLoadOp.progress / 0.9f);
            _displayProgress = Mathf.MoveTowards(_displayProgress, target, Time.unscaledDeltaTime * sliderLerpSpeed);

            if (loadingSlider != null)
                loadingSlider.value = _displayProgress;

            if (loadingText != null)
                loadingText.text = $"Loading... {Mathf.RoundToInt(_displayProgress * 100f)}%";

            bool loadReady = _mainLoadOp.progress >= 0.9f;
            bool minTimeDone = Time.unscaledTime - flowStart >= minLoadingSeconds;

            if (loadReady && minTimeDone)
                break;

            yield return null;
        }

        if (loadingSlider != null)
            loadingSlider.value = 1f;
        if (loadingText != null)
            loadingText.text = "Loading... 100%";

        yield return null;

        if (loadingPanel != null)
            loadingPanel.SetActive(false);
        if (tapToStartPanel != null)
            tapToStartPanel.SetActive(true);

        _readyToStart = true;
    }

    private AsyncOperation LoadMainAsync()
    {
        if (!string.IsNullOrEmpty(mainSceneName))
        {
            if (Application.CanStreamedLevelBeLoaded(mainSceneName))
            {
                var op = SceneManager.LoadSceneAsync(mainSceneName, LoadSceneMode.Single);
                if (op != null)
                {
                    op.allowSceneActivation = false;
                    return op;
                }
            }
        }

        if (mainSceneBuildIndex >= 0 && mainSceneBuildIndex < SceneManager.sceneCountInBuildSettings)
        {
            var op = SceneManager.LoadSceneAsync(mainSceneBuildIndex, LoadSceneMode.Single);
            if (op != null)
            {
                op.allowSceneActivation = false;
                return op;
            }
        }

        return null;
    }

    public void OnTapToStart()
    {
        if (!_readyToStart || _startedMain || _mainLoadOp == null)
            return;

        _startedMain = true;
        _mainLoadOp.allowSceneActivation = true;
        StartCoroutine(WaitForMainSceneReadyAfterActivation());
    }

    private IEnumerator WaitForMainSceneReadyAfterActivation()
    {
        while (!_mainLoadOp.isDone)
            yield return null;

        yield return MainSceneGameplayGate.WaitUntilReady(mainSceneName);
    }

    private void Update()
    {
        if (!_readyToStart || _startedMain)
            return;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null &&
            UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            OnTapToStart();
            return;
        }
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame ||
             UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame))
        {
            OnTapToStart();
        }
#else
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            OnTapToStart();
#endif
    }

    /// <summary>Overlay canvas for UI + a simple clear camera (avoids black screen / "no cameras" warning).</summary>
    private void EnsureTitleCamera()
    {
        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            canvas = Object.FindAnyObjectByType<Canvas>();

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;

            var canvasRt = canvas.GetComponent<RectTransform>();
            if (canvasRt != null && canvasRt.localScale.sqrMagnitude < 0.001f)
                canvasRt.localScale = Vector3.one;
        }

        if (Camera.main != null)
            return;

        var camGo = new GameObject("TitleCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.1f, 1f);
        cam.cullingMask = 0;
        cam.depth = -10;
        cam.orthographic = true;
    }

    private void PlayTitleAmbienceParticles()
    {
        if (sharedBranding == null) return;

        foreach (var ps in sharedBranding.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!ps.gameObject.activeInHierarchy)
                continue;

            if (!ps.isPlaying)
                ps.Play(true);
        }
    }
}
