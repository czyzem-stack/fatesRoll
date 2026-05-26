using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level-up roguelite rewards: after Steve's celebration, shows a popup with two random upgrade choices.
/// </summary>
[AddComponentMenu("FatesRoll/Rogue Lite Manager")]
public class RogueLiteManager : MonoBehaviour
{
    private const string PopupPrefabPath =
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_Component_Popups/Popup_01_Basic_Demo.prefab";
    private const string ButtonPrefabFolder =
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_Component_Buttons/Button_Rectangle_01_Convex_";

    private static RogueLiteManager _instance;
    public static RogueLiteManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = Object.FindAnyObjectByType<RogueLiteManager>();
            return _instance;
        }
    }

    [Header("Popup copy")]
    [SerializeField] private string titleFormat = "Congratulations, you have reached Level {0}";
    [SerializeField] private string bodyText = "Fate has given you a powerful gift.";

    [Header("Popup layout")]
    [Tooltip("Prefab reference size (Popup_01_Basic_Demo). Panel is centered and scaled down.")]
    [SerializeField] private Vector2 popupReferenceSize = new Vector2(838f, 524f);
    [Tooltip("0.5 = half width/height (~¼ screen area). Tune in Inspector.")]
    [SerializeField] [Range(0.25f, 1f)] private float popupScale = 0.58f;
    [SerializeField] private Color dimOverlayColor = new Color(0f, 0f, 0f, 0.65f);

    [Header("Popup typography")]
    [SerializeField] private float titleFontSize = 48f;
    [SerializeField] private float bodyFontSize = 36f;
    [SerializeField] private float buttonFontSize = 34f;
    [SerializeField] [Range(0.5f, 1.25f)] private float choiceButtonScale = 0.92f;

    [Header("Button prefabs (auto-filled in Editor)")]
    [SerializeField] private RogueLiteButtonPrefabBinding[] buttonPrefabs = new RogueLiteButtonPrefabBinding[0];

    [Header("Strength")]
    [SerializeField] private RogueLiteStatConfig strength = new RogueLiteStatConfig
    {
        upgradeAmount = 1f,
        offerChancePercent = 20f,
        upgradeScaler = 1f,
        buttonColor = RogueLiteButtonColor.Red
    };

    [Header("Agility")]
    [SerializeField] private RogueLiteStatConfig agility = new RogueLiteStatConfig
    {
        upgradeAmount = 1f,
        offerChancePercent = 20f,
        upgradeScaler = 1f,
        buttonColor = RogueLiteButtonColor.Green
    };

    [Header("Vitality")]
    [SerializeField] private RogueLiteStatConfig vitality = new RogueLiteStatConfig
    {
        upgradeAmount = 1f,
        offerChancePercent = 20f,
        upgradeScaler = 1f,
        buttonColor = RogueLiteButtonColor.Mint
    };

    [Header("Luck")]
    [SerializeField] private RogueLiteStatConfig luck = new RogueLiteStatConfig
    {
        upgradeAmount = 1f,
        offerChancePercent = 20f,
        upgradeScaler = 1f,
        buttonColor = RogueLiteButtonColor.Blue
    };

    [Header("Energy regen (seconds off interval)")]
    [SerializeField] private RogueLiteStatConfig energyRegen = new RogueLiteStatConfig
    {
        upgradeAmount = 1f,
        offerChancePercent = 20f,
        upgradeScaler = 1f,
        buttonColor = RogueLiteButtonColor.Purple
    };

    [Header("Coin bonus (+coins per enemy kill)")]
    [SerializeField] private RogueLiteStatConfig coinBonus = new RogueLiteStatConfig
    {
        upgradeAmount = 1f,
        offerChancePercent = 20f,
        upgradeScaler = 1f,
        buttonColor = RogueLiteButtonColor.Yellow
    };

    [Header("Runtime totals (read-only)")]
    [SerializeField] private float energyRegenTimeReduction;
    [SerializeField] private int bonusCoinsPerEnemyKill;

    private readonly Queue<int> pendingLevels = new Queue<int>();
    private readonly Dictionary<RogueLiteUpgradeType, int> pickCounts = new Dictionary<RogueLiteUpgradeType, int>();

    private GameObject overlayRoot;
    private GameObject popupInstance;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI bodyLabel;
    private Transform choiceButtonRoot;
    private bool rewardFlowActive;
    private bool waitingForChoice;

    [System.Serializable]
    public struct RogueLiteButtonPrefabBinding
    {
        public RogueLiteButtonColor color;
        public GameObject prefab;
    }

    public bool IsRewardFlowActive => rewardFlowActive;
    public float EnergyRegenTimeReduction => energyRegenTimeReduction;
    public int BonusCoinsPerEnemyKill => bonusCoinsPerEnemyKill;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    public void EnqueueLevelUp(int newLevel)
    {
        pendingLevels.Enqueue(newLevel);
        GlobalSettings.LogGameplay($"RogueLiteManager: queued level-up reward for level {newLevel}.");
    }

    public IEnumerator RunPostCelebrationRewards()
    {
        if (pendingLevels.Count == 0)
            yield break;

        rewardFlowActive = true;
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        try
        {
            EnsurePopup();
            while (pendingLevels.Count > 0)
            {
                int level = pendingLevels.Dequeue();
                yield return ShowRewardPopupAndWait(level);
            }
        }
        finally
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            rewardFlowActive = false;
            HidePopup();
        }
    }

    public float GetEffectiveEnergyRegenInterval()
    {
        var settings = GlobalSettings.Instance;
        float baseInterval = settings != null ? settings.energyRegenInterval : 15f;
        return Mathf.Max(1f, baseInterval - energyRegenTimeReduction);
    }

    public float GetNextBonusAmount(RogueLiteUpgradeType type)
    {
        if (!TryGetStatConfig(type, out RogueLiteStatConfig config))
            return 0f;

        int priorPicks = pickCounts.TryGetValue(type, out int count) ? count : 0;
        return config.GetScaledAmount(priorPicks);
    }

    private void ApplyUpgrade(RogueLiteOffer offer)
    {
        if (offer.config == null)
            return;

        float amount = offer.GetNextBonusAmount(
            pickCounts.TryGetValue(offer.upgradeType, out int count) ? count : 0);

        if (!pickCounts.ContainsKey(offer.upgradeType))
            pickCounts[offer.upgradeType] = 0;
        pickCounts[offer.upgradeType]++;

        switch (offer.upgradeType)
        {
            case RogueLiteUpgradeType.Strength:
                ApplyStrength(amount);
                break;
            case RogueLiteUpgradeType.Agility:
                ApplyAgility(amount);
                break;
            case RogueLiteUpgradeType.Vitality:
                ApplyVitality(amount);
                break;
            case RogueLiteUpgradeType.Luck:
                ApplyLuck(amount);
                break;
            case RogueLiteUpgradeType.EnergyRegen:
                energyRegenTimeReduction += amount;
                GlobalSettings.LogGameplay($"RogueLite: energy regen interval −{amount:0.##}s (total −{energyRegenTimeReduction:0.##}s).");
                break;
            case RogueLiteUpgradeType.CoinBonus:
                bonusCoinsPerEnemyKill += Mathf.RoundToInt(amount);
                GlobalSettings.LogGameplay($"RogueLite: +{Mathf.RoundToInt(amount)} coin(s) per enemy (total +{bonusCoinsPerEnemyKill}).");
                break;
        }
    }

    private static void ApplyStrength(float amount)
    {
        var hero = Object.FindAnyObjectByType<HeroController>();
        if (hero == null) return;
        var stats = hero.GetComponent<PlayerStats>();
        if (stats == null) return;

        stats.strength += amount;
        stats.CalculateAllDerivedStats();
        GlobalSettings.LogGameplay($"RogueLite: Strength +{amount:0.##} (now {stats.strength:0.##}).");
    }

    private static void ApplyAgility(float amount)
    {
        var hero = Object.FindAnyObjectByType<HeroController>();
        if (hero == null) return;
        var stats = hero.GetComponent<PlayerStats>();
        if (stats == null) return;

        stats.agility += amount;
        stats.CalculateAllDerivedStats();
        GlobalSettings.LogGameplay($"RogueLite: Agility +{amount:0.##} (now {stats.agility:0.##}).");
    }

    private static void ApplyVitality(float amount)
    {
        var hero = Object.FindAnyObjectByType<HeroController>();
        if (hero == null) return;
        var stats = hero.GetComponent<PlayerStats>();
        if (stats == null) return;

        stats.CalculateAllDerivedStats();
        float oldMaxHp = stats.MaxHP;

        stats.vitality += amount;
        stats.CalculateAllDerivedStats();
        stats.currentHP = Mathf.Min(stats.currentHP + (stats.MaxHP - oldMaxHp), stats.MaxHP);

        hero.UpdateHealthUI();
        GlobalSettings.LogGameplay($"RogueLite: Vitality +{amount:0.##} (now {stats.vitality:0.##}, max HP {stats.MaxHP:0.##}).");
    }

    private static void ApplyLuck(float amount)
    {
        var hero = Object.FindAnyObjectByType<HeroController>();
        if (hero == null) return;
        var stats = hero.GetComponent<PlayerStats>();
        if (stats == null) return;

        stats.luck += amount;
        stats.CalculateAllDerivedStats();
        GlobalSettings.LogGameplay($"RogueLite: Luck +{amount:0.##} (now {stats.luck:0.##}).");
    }

    private IEnumerator ShowRewardPopupAndWait(int level)
    {
        if (popupInstance == null)
            yield break;

        if (!TryRollOfferPair(out RogueLiteOffer optionA, out RogueLiteOffer optionB))
        {
            Debug.LogWarning("RogueLiteManager: no valid upgrade pool — skipping popup.");
            yield break;
        }

        if (titleLabel != null)
            titleLabel.text = string.Format(titleFormat, level);
        if (bodyLabel != null)
            bodyLabel.text = bodyText;

        ClearChoiceButtons();
        SpawnChoiceButton(optionA, () => CompleteChoice());
        SpawnChoiceButton(optionB, () => CompleteChoice());

        waitingForChoice = true;
        if (overlayRoot != null)
            overlayRoot.SetActive(true);
        popupInstance.SetActive(true);

        while (waitingForChoice)
            yield return null;
    }

    private void CompleteChoice()
    {
        waitingForChoice = false;
    }

    private void SpawnChoiceButton(RogueLiteOffer offer, System.Action onPicked)
    {
        if (offer.config == null)
            return;

        GameObject prefab = LoadButtonPrefab(offer.config.buttonColor);
        if (prefab == null)
        {
            Debug.LogError($"RogueLiteManager: missing button prefab for {offer.config.buttonColor}");
            return;
        }

        GameObject buttonGo = Instantiate(prefab, choiceButtonRoot);
        buttonGo.name = $"Choice_{offer.upgradeType}";

        var buttonRt = buttonGo.GetComponent<RectTransform>();
        if (buttonRt != null)
            buttonRt.localScale = new Vector3(choiceButtonScale, choiceButtonScale, 1f);

        var label = buttonGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = BuildChoiceLabel(offer);
            ApplyButtonLabelStyle(label);
        }

        var button = buttonGo.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            ApplyUpgrade(offer);
            onPicked?.Invoke();
        });
    }

    private string BuildChoiceLabel(RogueLiteOffer offer)
    {
        int prior = pickCounts.TryGetValue(offer.upgradeType, out int count) ? count : 0;
        float next = offer.GetNextBonusAmount(prior);
        return offer.upgradeType switch
        {
            RogueLiteUpgradeType.Strength => $"Strength +{next:0.##}",
            RogueLiteUpgradeType.Agility => $"Agility +{next:0.##}",
            RogueLiteUpgradeType.Vitality => $"Vitality +{next:0.##}",
            RogueLiteUpgradeType.Luck => $"Luck +{next:0.##}",
            RogueLiteUpgradeType.EnergyRegen => $"Energy Regen −{next:0.##}s",
            RogueLiteUpgradeType.CoinBonus => $"+{Mathf.RoundToInt(next)} Coin / Enemy",
            _ => offer.upgradeType.ToString()
        };
    }

    private bool TryGetStatConfig(RogueLiteUpgradeType type, out RogueLiteStatConfig config)
    {
        config = type switch
        {
            RogueLiteUpgradeType.Strength => strength,
            RogueLiteUpgradeType.Agility => agility,
            RogueLiteUpgradeType.Vitality => vitality,
            RogueLiteUpgradeType.Luck => luck,
            RogueLiteUpgradeType.EnergyRegen => energyRegen,
            RogueLiteUpgradeType.CoinBonus => coinBonus,
            _ => null
        };
        return config != null;
    }

    private bool TryRollOfferPair(out RogueLiteOffer optionA, out RogueLiteOffer optionB)
    {
        optionA = default;
        optionB = default;

        var pool = BuildWeightedPool();
        if (pool.Count == 0)
            return false;

        optionA = PickWeighted(pool);
        if (optionA.config == null)
            return false;

        RogueLiteUpgradeType typeA = optionA.upgradeType;
        pool.RemoveAll(o => o.upgradeType == typeA);

        if (pool.Count == 0)
            pool = BuildWeightedPool();

        optionB = PickWeighted(pool);
        if (optionB.config != null && optionA.config != null && optionB.upgradeType == optionA.upgradeType && pool.Count > 1)
        {
            RogueLiteUpgradeType typeB = optionB.upgradeType;
            pool.RemoveAll(o => o.upgradeType == typeB);
            optionB = PickWeighted(pool);
        }

        return optionA.config != null && optionB.config != null;
    }

    private List<RogueLiteOffer> BuildWeightedPool()
    {
        var list = new List<RogueLiteOffer>();
        TryAddOffer(list, RogueLiteUpgradeType.Strength, strength);
        TryAddOffer(list, RogueLiteUpgradeType.Agility, agility);
        TryAddOffer(list, RogueLiteUpgradeType.Vitality, vitality);
        TryAddOffer(list, RogueLiteUpgradeType.Luck, luck);
        TryAddOffer(list, RogueLiteUpgradeType.EnergyRegen, energyRegen);
        TryAddOffer(list, RogueLiteUpgradeType.CoinBonus, coinBonus);
        return list;
    }

    private static void TryAddOffer(List<RogueLiteOffer> list, RogueLiteUpgradeType type, RogueLiteStatConfig config)
    {
        if (config != null && config.IsOffered)
            list.Add(new RogueLiteOffer(type, config));
    }

    private static RogueLiteOffer PickWeighted(List<RogueLiteOffer> pool)
    {
        float total = 0f;
        foreach (var offer in pool)
            total += offer.config.offerChancePercent;

        if (total <= 0f)
            return pool.Count > 0 ? pool[0] : default;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        foreach (var offer in pool)
        {
            cumulative += offer.config.offerChancePercent;
            if (roll <= cumulative)
                return offer;
        }

        return pool[pool.Count - 1];
    }

    private void EnsurePopup()
    {
        if (popupInstance != null && overlayRoot != null)
            return;

        if (popupInstance != null)
            Destroy(popupInstance);
        if (overlayRoot != null)
            Destroy(overlayRoot);
        popupInstance = null;
        overlayRoot = null;

#if UNITY_EDITOR
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PopupPrefabPath);
#else
        GameObject prefab = null;
#endif
        if (prefab == null)
        {
            Debug.LogError($"RogueLiteManager: could not load popup prefab at {PopupPrefabPath}");
            return;
        }

        Canvas canvas = FindMainUiCanvas();
        if (canvas == null)
        {
            Debug.LogError("RogueLiteManager: no Canvas in scene for level-up popup.");
            return;
        }

        overlayRoot = new GameObject("LevelUpOverlay", typeof(RectTransform));
        overlayRoot.transform.SetParent(canvas.transform, false);
        overlayRoot.transform.SetAsLastSibling();
        StretchFullscreen(overlayRoot.GetComponent<RectTransform>());

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = dimOverlayColor;
        dim.raycastTarget = true;

        popupInstance = Instantiate(prefab, overlayRoot.transform, false);
        popupInstance.name = "LevelUpRewardPopup";

        var popupRt = popupInstance.GetComponent<RectTransform>();
        if (popupRt != null)
            ApplyCompactPopupLayout(popupRt);

        titleLabel = FindTmp(popupInstance.transform, "Text_Title");
        bodyLabel = FindTmp(popupInstance.transform, "Text_Info");
        ApplyPopupTextLayout(titleLabel, bodyLabel);

        Transform contentDemo = popupInstance.transform.Find("Content_Demo");
        if (contentDemo != null)
            contentDemo.gameObject.SetActive(false);

        Transform buttonOk = FindDeepChild(popupInstance.transform, "Button_OK");
        if (buttonOk != null)
            buttonOk.gameObject.SetActive(false);

        var host = new GameObject("ChoiceButtons", typeof(RectTransform));
        choiceButtonRoot = host.transform;
        choiceButtonRoot.SetParent(popupInstance.transform, false);
        var hostRt = (RectTransform)choiceButtonRoot;
        hostRt.anchorMin = new Vector2(0.5f, 0f);
        hostRt.anchorMax = new Vector2(0.5f, 0f);
        hostRt.pivot = new Vector2(0.5f, 0f);
        hostRt.anchoredPosition = new Vector2(0f, 36f);
        hostRt.sizeDelta = new Vector2(popupReferenceSize.x - 48f, 118f);

        var layout = host.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        overlayRoot.SetActive(false);
    }

    private void ApplyCompactPopupLayout(RectTransform popupRt)
    {
        popupRt.anchorMin = new Vector2(0.5f, 0.5f);
        popupRt.anchorMax = new Vector2(0.5f, 0.5f);
        popupRt.pivot = new Vector2(0.5f, 0.5f);
        popupRt.anchoredPosition = Vector2.zero;
        popupRt.sizeDelta = popupReferenceSize;
        popupRt.localScale = new Vector3(popupScale, popupScale, 1f);
    }

    private void ApplyPopupTextLayout(TextMeshProUGUI title, TextMeshProUGUI body)
    {
        if (title != null)
        {
            StretchTopBand(title.rectTransform, topInset: 20f, height: 88f);
            title.enableAutoSizing = true;
            title.fontSizeMax = titleFontSize;
            title.fontSizeMin = titleFontSize * 0.72f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.margin = new Vector4(24f, 4f, 24f, 4f);
            title.lineSpacing = -4f;
        }

        if (body != null)
        {
            StretchTopBand(body.rectTransform, topInset: 108f, height: 96f);
            body.enableAutoSizing = true;
            body.fontSizeMax = bodyFontSize;
            body.fontSizeMin = bodyFontSize * 0.72f;
            body.alignment = TextAlignmentOptions.Center;
            body.margin = new Vector4(32f, 0f, 32f, 0f);
            body.lineSpacing = 0f;
        }
    }

    private void ApplyButtonLabelStyle(TextMeshProUGUI label)
    {
        label.enableAutoSizing = true;
        label.fontSizeMax = buttonFontSize;
        label.fontSizeMin = buttonFontSize * 0.65f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.margin = new Vector4(8f, 4f, 8f, 4f);

        var labelRt = label.rectTransform;
        if (labelRt != null)
        {
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(12f, 6f);
            labelRt.offsetMax = new Vector2(-12f, -6f);
        }
    }

    private static void StretchTopBand(RectTransform rt, float topInset, float height)
    {
        if (rt == null)
            return;

        rt.anchorMin = new Vector2(0.04f, 1f);
        rt.anchorMax = new Vector2(0.96f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -topInset);
        rt.sizeDelta = new Vector2(0f, height);
    }

    private void HidePopup()
    {
        if (overlayRoot != null)
            overlayRoot.SetActive(false);
        if (popupInstance != null)
            popupInstance.SetActive(false);
        ClearChoiceButtons();
    }

    private void ClearChoiceButtons()
    {
        if (choiceButtonRoot == null)
            return;

        for (int i = choiceButtonRoot.childCount - 1; i >= 0; i--)
            Destroy(choiceButtonRoot.GetChild(i).gameObject);
    }

    private GameObject LoadButtonPrefab(RogueLiteButtonColor color)
    {
        if (buttonPrefabs != null)
        {
            foreach (var entry in buttonPrefabs)
            {
                if (entry.color == color && entry.prefab != null)
                    return entry.prefab;
            }
        }

#if UNITY_EDITOR
        string path = ButtonPrefabFolder + color + ".prefab";
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }

    private static Canvas FindMainUiCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>();
        foreach (var canvas in canvases)
        {
            if (canvas != null && canvas.gameObject.name.Contains("MainUI", System.StringComparison.OrdinalIgnoreCase))
                return canvas;
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        EditorAssignDefaultButtonPrefabs();
    }

    public void EditorAssignDefaultButtonPrefabs()
    {
        var colors = (RogueLiteButtonColor[])System.Enum.GetValues(typeof(RogueLiteButtonColor));
        buttonPrefabs = new RogueLiteButtonPrefabBinding[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            string path = ButtonPrefabFolder + colors[i] + ".prefab";
            buttonPrefabs[i] = new RogueLiteButtonPrefabBinding
            {
                color = colors[i],
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path)
            };
        }
    }
#endif

    private static TextMeshProUGUI FindTmp(Transform root, string objectName)
    {
        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject.name == objectName)
                return tmp;
        }

        return null;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
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

    private static void StretchFullscreen(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
