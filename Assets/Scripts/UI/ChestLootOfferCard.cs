using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chest loot offer card. At runtime only loot <b>content</b> is applied (icon sprite, label strings).
/// Font size, colors, alignment, RectTransforms, and button label text come from this scene GameObject.
/// </summary>
[AddComponentMenu("FatesRoll/UI/Chest Loot Offer Card")]
public class ChestLootOfferCard : MonoBehaviour
{
    public const string PrefabAssetPath = "Assets/Prefabs/UI/ChestLootOfferCard.prefab";

    [Header("UI references (assign from scene hierarchy)")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TextMeshProUGUI statLine1;
    [SerializeField] private TextMeshProUGUI statLine2;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionLabel;

    [Header("Runtime binding (content only)")]
    [Tooltip("When enabled, only the icon sprite is swapped; color/size/aspect come from the scene.")]
    [SerializeField] private bool bindIcon = true;
    [SerializeField] private bool bindSlotLabel = true;
    [SerializeField] private bool bindNameLabel = true;
    [SerializeField] private bool bindStatLines = true;

    bool layoutDefaultsCaptured;
    Color defaultIconColor = Color.white;
    bool defaultIconColorCaptured;

    void Awake()
    {
        ResolveReferences();
        CaptureLayoutDefaults();
    }

    /// <summary>Activates the card and wires the button; does not change text styling or layout.</summary>
    public void PrepareForBind()
    {
        gameObject.SetActive(true);
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

    /// <summary>Binds loot content and wires pick. Returns false if no clickable button was found.</summary>
    public bool Bind(EquipmentInstance item, System.Action onPick)
    {
        if (item?.definition == null)
        {
            Clear();
            return false;
        }

        ResolveReferences();
        gameObject.SetActive(true);
        CaptureLayoutDefaults();
        ApplyLootContent(item);
        return WirePickAction(onPick);
    }

    bool WirePickAction(System.Action onPick)
    {
        if (actionButton == null)
        {
            Debug.LogWarning($"ChestLootOfferCard: no Button on '{name}' — assign Button_Action in the inspector.");
            return false;
        }

        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(() => onPick?.Invoke());
        actionButton.interactable = true;
        return true;
    }

    void ApplyLootContent(EquipmentInstance item)
    {
        if (bindIcon && iconImage != null)
        {
            iconImage.sprite = item.runtimeIcon;
            iconImage.enabled = item.runtimeIcon != null;
            if (defaultIconColorCaptured)
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
        Transform iconRoot = transform.Find("IconRoot/Icon")
                               ?? transform.Find("ItemIcon/Icon")
                               ?? transform.Find("Icon");
        if (iconRoot != null)
            iconImage = iconRoot.GetComponent<Image>();

        slotLabel ??= FindTmpDeep("Text_Slot");
        nameLabel ??= FindTmpDeep("Text_Name");
        statLine1 ??= FindTmpDeep("Text_Stat1");
        statLine2 ??= FindTmpDeep("Text_Stat2");

        if (actionButton == null)
            actionButton = transform.Find("Button_Action")?.GetComponent<Button>();

        if (actionButton == null)
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name.Contains("Button", System.StringComparison.OrdinalIgnoreCase))
                {
                    actionButton = button;
                    break;
                }
            }
        }

        actionLabel ??= FindTmpDeep("Text_Action");
        actionLabel ??= actionButton != null
            ? actionButton.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
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

    /// <summary>Sets string content only — font, size, color, and alignment stay as authored in the scene.</summary>
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
