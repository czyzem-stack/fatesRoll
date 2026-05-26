using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Treasure chest rewards: rolled equipment with two-of-four stat bonuses and A/B popup picks.
/// </summary>
/// <remarks>Inherits <see cref="GameServiceBehaviour{T}"/> — auto-registers in Awake via <see cref="GameServices"/>.</remarks>
[AddComponentMenu("FatesRoll/Equipment Loot Manager")]
public class EquipmentLootManager : GameServiceBehaviour<EquipmentLootManager>
{
    private const string PopupPrefabPath =
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_Component_Popups/Popup_01_Basic_Demo.prefab";
    private const string ButtonPrefabFolder =
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_Component_Buttons/Button_Rectangle_01_Convex_";
    private const string DefaultCatalogPath = "Assets/Data/Equipment/EquipmentCatalog.asset";
    private const string CatalogResourcesPath = "Equipment/EquipmentCatalog";


    [Header("Catalog")]
    [SerializeField] private EquipmentCatalog catalog;

    [Header("Stat rolling")]
    [Tooltip("Base bonus per rolled stat on the first chest.")]
    [SerializeField] private float startingStatBonus = 1f;
    [Tooltip("Each chest multiplies bonus by this factor (chest 2 = starting × scalar^1, etc.).")]
    [SerializeField] private float chestPowerScalar = 1.2f;

    [Header("Popup copy")]
    [SerializeField] private string titleFormat = "Treasure Chest Opened!";
    [SerializeField] private string bodyText = "Fate offers you two relics. Choose one.";

    [Header("Popup layout")]
    [SerializeField] private Vector2 popupReferenceSize = new Vector2(838f, 524f);
    [SerializeField] [Range(0.25f, 1f)] private float popupScale = 0.58f;
    [SerializeField] private Color dimOverlayColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private float titleFontSize = 48f;
    [SerializeField] private float bodyFontSize = 36f;
    [SerializeField] private float buttonFontSize = 34f;
    [SerializeField] [Range(0.5f, 1.25f)] private float choiceButtonScale = 0.92f;

    [Header("Button prefabs")]
    [SerializeField] private RogueLiteManager.RogueLiteButtonPrefabBinding[] buttonPrefabs =
        new RogueLiteManager.RogueLiteButtonPrefabBinding[0];

    [SerializeField] private RogueLiteButtonColor weaponButtonColor = RogueLiteButtonColor.Red;
    [SerializeField] private RogueLiteButtonColor armorButtonColor = RogueLiteButtonColor.Blue;

    [Header("Runtime")]
    [SerializeField] private int chestsOpenedCount;

    private readonly Queue<ChestRewardRequest> pendingChests = new Queue<ChestRewardRequest>();

    private GameObject overlayRoot;
    private GameObject popupInstance;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI bodyLabel;
    private Transform choiceButtonRoot;
    private bool rewardFlowActive;
    private bool waitingForChoice;
    private EquipmentInstance pendingOptionA;
    private EquipmentInstance pendingOptionB;

    public bool IsRewardFlowActive => rewardFlowActive;
    public bool HasPendingChestRewards => pendingChests.Count > 0;
    public EquipmentCatalog Catalog => catalog;
    public int ChestsOpenedCount => chestsOpenedCount;

    private struct ChestRewardRequest
    {
        public POINode poi;
        public int ftueIndex;
        public EquipmentItemDefinition forcedA;
        public EquipmentItemDefinition forcedB;
    }

    protected override void Awake()
    {
        base.Awake();
        EnsureReferences();
    }

    /// <summary>Loads the equipment catalog when the Inspector reference is missing.</summary>
    public void EnsureReferences()
    {
        if (catalog != null)
            return;

#if UNITY_EDITOR
        catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(DefaultCatalogPath);
#endif
        if (catalog == null)
            catalog = Resources.Load<EquipmentCatalog>(CatalogResourcesPath);

        if (catalog == null)
            GlobalSettings.LogGameplayWarning(
                "EquipmentLootManager: no catalog assigned. Assign EquipmentCatalog on EquipmentLootManager in the scene.");
    }

    public void EnqueueChestReward(POINode poi)
    {
        if (poi == null)
            return;

        pendingChests.Enqueue(new ChestRewardRequest
        {
            poi = poi,
            ftueIndex = poi.ftueLootIndex,
            forcedA = poi.ftueForcedOptionA,
            forcedB = poi.ftueForcedOptionB
        });

        GlobalSettings.LogGameplay($"EquipmentLootManager: queued chest reward (FTUE index {poi.ftueLootIndex}).");
    }

    public IEnumerator RunChestRewards()
    {
        if (pendingChests.Count == 0)
            yield break;

        rewardFlowActive = true;
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        try
        {
            EnsurePopup();
            while (pendingChests.Count > 0)
            {
                var request = pendingChests.Dequeue();
                yield return ShowChestPopupAndWait(request);
                chestsOpenedCount++;
            }
        }
        finally
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            rewardFlowActive = false;
            HidePopup();
        }
    }

    public float GetCurrentStatBonusPerRoll()
    {
        return startingStatBonus * Mathf.Pow(chestPowerScalar, chestsOpenedCount);
    }

    private IEnumerator ShowChestPopupAndWait(ChestRewardRequest request)
    {
        if (popupInstance == null)
            yield break;

        if (!TryBuildOfferPair(request, out EquipmentInstance optionA, out EquipmentInstance optionB))
        {
            Debug.LogWarning("EquipmentLootManager: could not build chest offers — skipping.");
            yield break;
        }

        pendingOptionA = optionA;
        pendingOptionB = optionB;

        if (titleLabel != null)
            titleLabel.text = titleFormat;
        if (bodyLabel != null)
            bodyLabel.text = bodyText;

        ClearChoiceButtons();
        SpawnChoiceButton(optionA, weaponButtonColor, () => CompleteChoice(optionA));
        SpawnChoiceButton(optionB, armorButtonColor, () => CompleteChoice(optionB));

        waitingForChoice = true;
        overlayRoot?.SetActive(true);
        popupInstance.SetActive(true);

        while (waitingForChoice)
            yield return null;
    }

    private void CompleteChoice(EquipmentInstance chosen)
    {
        var heroEquip = FindHeroEquipment();
        if (heroEquip != null && chosen != null)
            heroEquip.Equip(chosen);

        waitingForChoice = false;
    }

    private static HeroEquipment FindHeroEquipment()
    {
        var hero = GameServices.Hero;
        if (hero == null)
            return null;
        var equip = hero.GetComponent<HeroEquipment>();
        if (equip == null)
            equip = hero.gameObject.AddComponent<HeroEquipment>();
        return equip;
    }

    private bool TryBuildOfferPair(ChestRewardRequest request, out EquipmentInstance optionA, out EquipmentInstance optionB)
    {
        optionA = null;
        optionB = null;
        float bonus = GetCurrentStatBonusPerRoll();
        int tier = chestsOpenedCount;
        var heroEquip = FindHeroEquipment();

        if (request.forcedA != null)
            optionA = CreateInstance(request.forcedA, bonus, tier, heroEquip?.GetReferenceForItemDefinition(request.forcedA));
        if (request.forcedB != null)
            optionB = CreateInstance(request.forcedB, bonus, tier, heroEquip?.GetReferenceForItemDefinition(request.forcedB));

        if (optionA != null && optionB != null)
            return true;

        EnsureReferences();
        if (catalog == null)
        {
            Debug.LogError(
                $"EquipmentLootManager: no Equipment Catalog. Assign {DefaultCatalogPath} or run " +
                "Assign EquipmentCatalog on EquipmentLootManager in the scene.");
            return false;
        }

        var weapons = catalog.GetByCategory(EquipmentChestCategory.Weapon);
        var armors = catalog.GetByCategory(EquipmentChestCategory.Armor);

        var accessories = catalog.GetByCategory(EquipmentChestCategory.Accessory);

        if (optionA == null)
            optionA = RollRandomFromPool(weapons.Count > 0 ? weapons : accessories, bonus, tier);
        if (optionB == null)
        {
            List<EquipmentItemDefinition> poolB = armors.Count > 0 ? armors : accessories;
            if (armors.Count > 0 && accessories.Count > 0 && Random.value < 0.3f)
                poolB = accessories;
            optionB = RollRandomFromPool(poolB, bonus, tier);
        }

        if (optionA == null || optionB == null)
            return false;

        if (optionA.definition == optionB.definition)
        {
            var pool = armors.Count > 0 ? armors : weapons;
            var alt = RollRandomFromPool(pool, bonus, tier, optionB.definition);
            if (alt != null)
                optionB = alt;
        }

        return optionA != null && optionB != null;
    }

    private static EquipmentInstance RollRandomFromPool(
        List<EquipmentItemDefinition> pool,
        float bonus,
        int tier,
        EquipmentItemDefinition exclude = null,
        EquipmentInstance upgradeFrom = null)
    {
        if (pool == null || pool.Count == 0)
            return null;

        var candidates = new List<EquipmentItemDefinition>();
        foreach (var item in pool)
        {
            if (item != null && item != exclude)
                candidates.Add(item);
        }

        if (candidates.Count == 0)
            return null;

        var pick = candidates[Random.Range(0, candidates.Count)];
        EquipmentInstance reference = upgradeFrom ?? FindHeroEquipment()?.GetReferenceForItemDefinition(pick);
        return CreateInstance(pick, bonus, tier, reference);
    }

    private static EquipmentInstance CreateInstance(
        EquipmentItemDefinition def,
        float bonus,
        int tier,
        EquipmentInstance upgradeFrom = null)
    {
        var rolled = upgradeFrom != null
            ? EquipmentStatRoller.RollTwoOfFourUpgradeFrom(upgradeFrom, bonus)
            : EquipmentStatRoller.RollTwoOfFour(bonus);
        return new EquipmentInstance(def, rolled, tier);
    }

    private void SpawnChoiceButton(EquipmentInstance offer, RogueLiteButtonColor color, System.Action onPicked)
    {
        if (offer?.definition == null)
            return;

        GameObject prefab = LoadButtonPrefab(color);
        if (prefab == null)
        {
            Debug.LogError($"EquipmentLootManager: missing button prefab for {color}");
            return;
        }

        GameObject buttonGo = Instantiate(prefab, choiceButtonRoot);
        buttonGo.name = $"Choice_{offer.definition.itemId}";

        var buttonRt = buttonGo.GetComponent<RectTransform>();
        if (buttonRt != null)
            buttonRt.localScale = new Vector3(choiceButtonScale, choiceButtonScale, 1f);

        var label = buttonGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = offer.BuildChoiceLabel();
            ApplyButtonLabelStyle(label);
        }

        var button = buttonGo.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onPicked?.Invoke());
    }

    private void EnsurePopup()
    {
        if (popupInstance != null && overlayRoot != null)
            return;

        if (popupInstance != null)
            Destroy(popupInstance);
        if (overlayRoot != null)
            Destroy(overlayRoot);

#if UNITY_EDITOR
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PopupPrefabPath);
#else
        GameObject prefab = null;
#endif
        if (prefab == null)
        {
            Debug.LogError($"EquipmentLootManager: popup prefab missing at {PopupPrefabPath}");
            return;
        }

        Canvas canvas = FindMainUiCanvas();
        if (canvas == null)
        {
            Debug.LogError("EquipmentLootManager: no Canvas for equipment popup.");
            return;
        }

        overlayRoot = new GameObject("EquipmentLootOverlay", typeof(RectTransform));
        overlayRoot.transform.SetParent(canvas.transform, false);
        overlayRoot.transform.SetAsLastSibling();
        StretchFullscreen(overlayRoot.GetComponent<RectTransform>());

        var dim = overlayRoot.AddComponent<Image>();
        dim.color = dimOverlayColor;
        dim.raycastTarget = true;

        popupInstance = Instantiate(prefab, overlayRoot.transform, false);
        popupInstance.name = "EquipmentRewardPopup";

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
            StretchTopBand(title.rectTransform, 20f, 88f);
            title.enableAutoSizing = true;
            title.fontSizeMax = titleFontSize;
            title.fontSizeMin = titleFontSize * 0.72f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
        }

        if (body != null)
        {
            StretchTopBand(body.rectTransform, 108f, 96f);
            body.enableAutoSizing = true;
            body.fontSizeMax = bodyFontSize;
            body.fontSizeMin = bodyFontSize * 0.72f;
            body.alignment = TextAlignmentOptions.Center;
        }
    }

    private void ApplyButtonLabelStyle(TextMeshProUGUI label)
    {
        label.enableAutoSizing = true;
        label.fontSizeMax = buttonFontSize;
        label.fontSizeMin = buttonFontSize * 0.65f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
    }

    private static void StretchTopBand(RectTransform rt, float topInset, float height)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.04f, 1f);
        rt.anchorMax = new Vector2(0.96f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -topInset);
        rt.sizeDelta = new Vector2(0f, height);
    }

    private void HidePopup()
    {
        overlayRoot?.SetActive(false);
        popupInstance?.SetActive(false);
        ClearChoiceButtons();
    }

    private void ClearChoiceButtons()
    {
        if (choiceButtonRoot == null) return;
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
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabFolder + color + ".prefab");
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
    public void EditorAssignDefaultButtonPrefabs()
    {
        var colors = (RogueLiteButtonColor[])System.Enum.GetValues(typeof(RogueLiteButtonColor));
        buttonPrefabs = new RogueLiteManager.RogueLiteButtonPrefabBinding[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            string path = ButtonPrefabFolder + colors[i] + ".prefab";
            buttonPrefabs[i] = new RogueLiteManager.RogueLiteButtonPrefabBinding
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
    }
}
