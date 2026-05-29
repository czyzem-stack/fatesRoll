using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Chest loot offer card. At runtime only loot content is applied; layout/fonts/colors stay from the scene.
/// </summary>
[AddComponentMenu("FatesRoll/UI/Chest Loot Offer Card")]
public class ChestLootOfferCard : MonoBehaviour, IPointerClickHandler
{
    public const string PrefabAssetPath = "Assets/Prefabs/UI/ChestLootOfferCard.prefab";

    /// <summary>GUI Pro ItemFrame demo child — must stay off so loot uses only IconRoot/Icon.</summary>
    public const string FrameDemoIconObjectName = "ItemIcon";

    [Header("UI references (assign from scene hierarchy)")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TextMeshProUGUI statLine1;
    [SerializeField] private TextMeshProUGUI statLine2;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionLabel;

    [Header("Runtime binding (content only)")]
    [SerializeField] private bool bindIcon = true;
    [SerializeField] private bool bindSlotLabel = true;
    [SerializeField] private bool bindNameLabel = true;
    [SerializeField] private bool bindStatLines = true;

    bool layoutDefaultsCaptured;
    Color defaultIconColor = Color.white;
    bool defaultIconColorCaptured;
    bool pickEnabled;
    System.Action pendingPick;

    void Awake()
    {
        ResolveReferences();
        DisableFrameDemoIconLayer();
        CaptureLayoutDefaults();
        EnsureCardReceivesClicks();
    }

    /// <summary>Hides the packaged ItemFrame demo icon (red potion) so only loot IconRoot/Icon is visible.</summary>
    public static void DisableFrameDemoIconLayer(Transform cardRoot)
    {
        if (cardRoot == null)
            return;

        foreach (Transform child in cardRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == FrameDemoIconObjectName)
                child.gameObject.SetActive(false);
        }
    }

    void DisableFrameDemoIconLayer() => DisableFrameDemoIconLayer(transform);

    public void PrepareForBind()
    {
        gameObject.SetActive(true);
        pickEnabled = false;
        pendingPick = null;
        DisableFrameDemoIconLayer();
        CaptureLayoutDefaults();

        if (bindIcon && iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
            if (defaultIconColorCaptured)
                iconImage.color = defaultIconColor;
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.interactable = false;
        }
    }

    /// <summary>Binds loot content and wires pick. Returns false if no pick target was found.</summary>
    public bool Bind(EquipmentInstance item, System.Action onPick)
    {
        if (item?.definition == null)
        {
            Clear();
            return false;
        }

        ResolveReferences();
        DisableFrameDemoIconLayer();
        gameObject.SetActive(true);
        CaptureLayoutDefaults();
        ApplyLootContent(item);
        return WirePickAction(onPick);
    }

    bool WirePickAction(System.Action onPick)
    {
        pendingPick = onPick;
        pickEnabled = onPick != null;

        if (actionButton == null)
            actionButton = FindPickButton();

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(InvokePick);
            actionButton.interactable = pickEnabled;
            return true;
        }

        if (EnsureCardReceivesClicks())
            return pickEnabled;

        Debug.LogWarning($"ChestLootOfferCard: no Button on '{name}' — add Button_Action or a Graphic for card clicks.");
        return false;
    }

    void InvokePick()
    {
        if (!pickEnabled)
            return;

        pickEnabled = false;
        pendingPick?.Invoke();
        pendingPick = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (actionButton != null && actionButton.interactable)
            return;

        InvokePick();
    }

    void ApplyLootContent(EquipmentInstance item)
    {
        if (bindIcon && iconImage != null)
        {
            iconImage.sprite = item.runtimeIcon;
            iconImage.enabled = item.runtimeIcon != null;
            if (item.runtimeIcon != null)
            {
                Color c = defaultIconColorCaptured ? defaultIconColor : iconImage.color;
                c.a = 1f;
                iconImage.color = c;
            }
            else if (defaultIconColorCaptured)
                iconImage.color = defaultIconColor;
        }

        if (bindSlotLabel)
            SetDynamicText(slotLabel, item.SlotDisplayName);

        if (bindNameLabel)
            SetDynamicText(nameLabel, item.DisplayName);

        if (bindStatLines)
        {
            SetDynamicText(statLine1, item.GetStatLine(0));
            SetDynamicText(statLine2, item.GetStatLine(1));
        }
    }

    public void Clear()
    {
        if (!this)
            return;

        pickEnabled = false;
        pendingPick = null;

        if (bindIcon && iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
            if (defaultIconColorCaptured)
                iconImage.color = defaultIconColor;
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.interactable = false;
        }

        gameObject.SetActive(false);
    }

    public void ResolveReferences()
    {
        DisableFrameDemoIconLayer();

        Transform lootIcon = transform.Find("IconRoot/Icon");
        if (lootIcon != null)
            iconImage = lootIcon.GetComponent<Image>();
        else if (iconImage == null || iconImage.gameObject.name != "Icon" ||
                 iconImage.transform.parent == null ||
                 iconImage.transform.parent.name != "IconRoot")
            iconImage = null;

        slotLabel ??= FindTmpDeep("Text_Slot");
        nameLabel ??= FindTmpDeep("Text_Name");
        statLine1 ??= FindTmpDeep("Text_Stat1");
        statLine2 ??= FindTmpDeep("Text_Stat2");

        actionButton ??= FindPickButton();

        actionLabel ??= FindTmpDeep("Text_Action");
        actionLabel ??= actionButton != null
            ? actionButton.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
    }

    Button FindPickButton()
    {
        Transform named = transform.Find("Button_Action");
        if (named != null && named.TryGetComponent(out Button namedButton))
            return namedButton;

        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button != null && button.gameObject.name.Contains("Button_Action", System.StringComparison.OrdinalIgnoreCase))
                return button;
        }

        return null;
    }

    bool EnsureCardReceivesClicks()
    {
        Graphic graphic = GetComponent<Graphic>();
        if (graphic == null)
            graphic = GetComponentInChildren<Graphic>(true);

        if (graphic != null)
            graphic.raycastTarget = true;

        return graphic != null;
    }

    void CaptureLayoutDefaults()
    {
        if (layoutDefaultsCaptured)
            return;

        if (iconImage != null)
        {
            defaultIconColor = iconImage.color;
            defaultIconColorCaptured = true;
        }

        layoutDefaultsCaptured = true;
    }

    static void SetDynamicText(TextMeshProUGUI label, string value)
    {
        if (label == null)
            return;

        label.text = value ?? string.Empty;
    }

    TextMeshProUGUI FindTmpDeep(string childName)
    {
        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject.name == childName)
                return tmp;
        }

        return null;
    }
}
