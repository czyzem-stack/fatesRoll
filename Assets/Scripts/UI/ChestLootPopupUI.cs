using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Chest loot popup under MainUI_Canvas. Hierarchy is scene GameObjects (Offer_A / Offer_B); runtime only binds loot data.
/// </summary>
[AddComponentMenu("FatesRoll/UI/Chest Loot Popup")]
public class ChestLootPopupUI : MonoBehaviour
{
    public const string DefaultScenePath = "MainUI_Canvas/ChestLootOverlay";
    public const string PrefabAssetPath = "Assets/Prefabs/UI/ChestLootPopupOverlay.prefab";
    public const string ResourcesPrefabPath = "UI/ChestLootPopupOverlay";
    public const string OfferSlotAName = "Offer_A";
    public const string OfferSlotBName = "Offer_B";

    [Header("Roots")]
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI bodyLabel;
    [SerializeField] private RectTransform offerRoot;

    [Header("Offer slots (scene GameObjects under OfferCards)")]
    [SerializeField] private ChestLootOfferCard offerSlotA;
    [SerializeField] private ChestLootOfferCard offerSlotB;

    public bool IsReady =>
        overlayRoot != null &&
        offerSlotA != null &&
        offerSlotB != null;

    void Awake()
    {
        ResolveReferences();
        Hide();
    }

    public static ChestLootPopupUI FindInScene()
    {
        var found = FindAnyObjectByType<ChestLootPopupUI>(FindObjectsInactive.Include);
        if (found != null)
            return found;

        var byName = GameObject.Find("ChestLootOverlay");
        return byName != null ? byName.GetComponent<ChestLootPopupUI>() : null;
    }

    /// <summary>Finds the chest overlay GameObject already placed in the loaded scene.</summary>
    public static ChestLootPopupUI EnsureInScene()
    {
        ChestLootPopupUI found = FindInScene();
        if (found != null)
            return found;

        Debug.LogError(
            "ChestLootPopupUI: no ChestLootOverlay in scene. Run FatesRoll → Equipment → Create Chest Loot Popup In Main Scene.");
        return null;
    }

    public void ResolveReferences()
    {
        overlayRoot ??= transform as RectTransform;

        if (popupPanel == null)
        {
            Transform popup = transform.Find("ChestLootPopup");
            popupPanel = popup != null ? popup as RectTransform : null;
        }

        if (titleLabel == null && popupPanel != null)
            titleLabel = FindNamedTmp(popupPanel, "Text_Title");

        if (bodyLabel == null && popupPanel != null)
            bodyLabel = FindNamedTmp(popupPanel, "Text_Info");

        if (offerRoot == null && popupPanel != null)
        {
            Transform cards = popupPanel.Find("OfferCards");
            offerRoot = cards != null ? cards as RectTransform : null;
        }

        if (offerRoot != null)
        {
            if (offerSlotA == null)
                offerSlotA = FindOfferSlot(OfferSlotAName);
            if (offerSlotB == null)
                offerSlotB = FindOfferSlot(OfferSlotBName);
        }

        if (offerSlotA != null)
            offerSlotA.ResolveReferences();

        if (offerSlotB != null)
            offerSlotB.ResolveReferences();

        if (popupPanel != null)
            HideDemoContent(popupPanel);
    }

    ChestLootOfferCard FindOfferSlot(string slotName)
    {
        if (offerRoot == null)
            return null;

        Transform slot = FindChildRecursive(offerRoot, slotName);
        if (slot == null)
            return null;

        var card = slot.GetComponent<ChestLootOfferCard>();
        if (card == null)
            Debug.LogError(
                $"ChestLootPopupUI: '{slotName}' is missing ChestLootOfferCard component.");

        return card;
    }

    static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>Shows the popup and binds offers. Returns false if UI is not wired (caller must not wait for input).</summary>
    public bool Show(IReadOnlyList<ChestLootOfferEntry> offers)
    {
        ResolveReferences();

        if (!IsReady)
        {
            Debug.LogError(
                "ChestLootPopupUI: offer slots not wired. Assign Offer_A / Offer_B on ChestLootOverlay, or run FatesRoll → Equipment → Create Chest Loot Popup In Main Scene.");
            return false;
        }

        // Title, subtitle, and button labels come from the prefab/scene — do not overwrite here.
        if (IsAlive(bodyLabel))
            bodyLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(bodyLabel.text));

        if (IsAlive(offerSlotA))
            offerSlotA.PrepareForBind();

        if (IsAlive(offerSlotB))
            offerSlotB.PrepareForBind();

        int bound = 0;
        if (offers != null && offers.Count > 0)
            bound += BindOfferSlot(offerSlotA, offers, 0) ? 1 : 0;

        if (offers != null && offers.Count > 1)
            bound += BindOfferSlot(offerSlotB, offers, 1) ? 1 : 0;

        if (bound == 0)
        {
            Debug.LogError("ChestLootPopupUI: no offer cards bound — check ChestLootOfferCard references and Button_Action.");
            return false;
        }

        SetVisible(true);
        return true;
    }

    public void Hide()
    {
        if (!IsAlive(this))
            return;

        ClearOfferSlots();
        SetVisible(false);
    }

    bool BindOfferSlot(ChestLootOfferCard slot, IReadOnlyList<ChestLootOfferEntry> offers, int index)
    {
        if (!IsAlive(slot) || index < 0 || index >= offers.Count)
            return false;

        var entry = offers[index];
        if (entry.item == null)
        {
            slot.Clear();
            return false;
        }

        return slot.Bind(entry.item, entry.onPick);
    }

    void ClearOfferSlots()
    {
        if (IsAlive(offerSlotA))
            offerSlotA.Clear();

        if (IsAlive(offerSlotB))
            offerSlotB.Clear();
    }

    void SetVisible(bool visible)
    {
        if (!IsAlive(this))
            return;

        if (IsAlive(overlayRoot))
            overlayRoot.gameObject.SetActive(visible);
        else
            gameObject.SetActive(visible);

        if (IsAlive(popupPanel))
            popupPanel.gameObject.SetActive(visible);
    }

    static bool IsAlive(Object obj) => obj;

    void HideDemoContent(Transform popup)
    {
        Transform contentDemo = popup.Find("Content_Demo");
        if (contentDemo != null)
            contentDemo.gameObject.SetActive(false);

        Transform buttonOk = FindDeepChild(popup, "Button_OK");
        if (buttonOk != null)
            buttonOk.gameObject.SetActive(false);
    }

    static TextMeshProUGUI FindNamedTmp(Transform root, string objectName)
    {
        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject.name == objectName)
                return tmp;
        }

        return null;
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

    public struct ChestLootOfferEntry
    {
        public EquipmentInstance item;
        public System.Action onPick;
    }
}
