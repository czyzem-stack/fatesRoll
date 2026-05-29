using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>
/// Chest loot popup under MainUI_Canvas. Hierarchy is scene GameObjects (Offer_A / Offer_B); runtime only binds loot data.
/// </summary>
[AddComponentMenu("FatesRoll/UI/Chest Loot Popup")]
public class ChestLootPopupUI : MonoBehaviour
{
    public const string OverlayObjectName = "ChestLootOverlay";
    public const string MainCanvasObjectName = "MainUI_Canvas";
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
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == null)
                    continue;

                Transform canvas = FindChildRecursive(root.transform, MainCanvasObjectName);
                if (canvas != null)
                {
                    Transform overlay = FindChildRecursive(canvas, OverlayObjectName);
                    if (overlay != null)
                    {
                        var onCanvas = overlay.GetComponent<ChestLootPopupUI>();
                        if (onCanvas != null)
                            return onCanvas;
                    }
                }

                var ui = root.GetComponentInChildren<ChestLootPopupUI>(true);
                if (ui != null)
                    return ui;
            }
        }

        GameObject byName = GameObject.Find(OverlayObjectName);
        return byName != null ? byName.GetComponent<ChestLootPopupUI>() : null;
    }

    public static ChestLootPopupUI EnsureInScene() => FindInScene();

    public void ResolveReferences()
    {
        overlayRoot ??= transform as RectTransform;

        Transform popup = FindChildRecursive(transform, "ChestLootPopup");
        if (popup != null)
            popupPanel = popup as RectTransform;

        if (titleLabel == null && popupPanel != null)
            titleLabel = FindNamedTmp(popupPanel, "Text_Title");

        if (bodyLabel == null && popupPanel != null)
        {
            bodyLabel = FindNamedTmp(popupPanel, "Text_Info");
            if (bodyLabel == null)
                bodyLabel = FindNamedTmp(popupPanel, "Text_Description");
        }

        if (offerRoot == null && popupPanel != null)
        {
            Transform cards = FindChildRecursive(popupPanel, "OfferCards");
            offerRoot = cards != null ? cards as RectTransform : null;
        }

        ResolveOfferSlot(ref offerSlotA, OfferSlotAName);
        ResolveOfferSlot(ref offerSlotB, OfferSlotBName);

        if (offerSlotA != null)
            offerSlotA.ResolveReferences();

        if (offerSlotB != null)
            offerSlotB.ResolveReferences();

        if (popupPanel != null)
            HideDemoContent(popupPanel);
    }

    void ResolveOfferSlot(ref ChestLootOfferCard slot, string slotName)
    {
        if (slot != null && IsUnderOfferRoot(slot.transform))
            return;

        slot = FindOfferSlot(slotName);
    }

    bool IsUnderOfferRoot(Transform t)
    {
        if (t == null || offerRoot == null)
            return false;

        return t == offerRoot || t.IsChildOf(offerRoot);
    }

    ChestLootOfferCard FindOfferSlot(string slotName)
    {
        if (offerRoot == null)
            return null;

        Transform slot = FindChildRecursive(offerRoot, slotName);
        if (slot == null)
        {
            Debug.LogError($"ChestLootPopupUI: missing '{slotName}' under OfferCards.");
            return null;
        }

        var card = slot.GetComponent<ChestLootOfferCard>();
        if (card == null)
            card = slot.gameObject.AddComponent<ChestLootOfferCard>();

        return card;
    }

    bool BindOfferToSlot(string expectedName, ChestLootOfferCard slot, ChestLootOfferEntry entry)
    {
        if (!IsAlive(slot))
            return false;

        if (slot.name != expectedName)
            Debug.LogWarning($"ChestLootPopupUI: expected '{expectedName}' but bound '{slot.name}'.");

        if (entry.item == null)
        {
            slot.Clear();
            return false;
        }

        return slot.Bind(entry.item, entry.onPick);
    }

    /// <summary>Shows the popup and binds offers. Returns false if UI is not wired (caller must not wait for input).</summary>
    public bool Show(IReadOnlyList<ChestLootOfferEntry> offers)
    {
        ResolveReferences();

        if (!IsReady)
        {
            Debug.LogError(
                "ChestLootPopupUI: offer slots not wired. Select ChestLootOverlay and assign Offer_A / Offer_B, or run FatesRoll → Equipment → Wire Chest Loot Popup In Main Scene.");
            return false;
        }

        EnsureVisibleHierarchy();

        if (IsAlive(bodyLabel))
            bodyLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(bodyLabel.text));

        if (IsAlive(offerSlotA))
            offerSlotA.PrepareForBind();

        if (IsAlive(offerSlotB))
            offerSlotB.PrepareForBind();

        int bound = 0;
        if (offers != null && offers.Count > 0)
            bound += BindOfferToSlot(OfferSlotAName, offerSlotA, offers[0]) ? 1 : 0;

        if (offers != null && offers.Count > 1)
            bound += BindOfferToSlot(OfferSlotBName, offerSlotB, offers[1]) ? 1 : 0;

        if (bound == 0)
        {
            Debug.LogError(
                "ChestLootPopupUI: no offer cards bound — check ChestLootOfferCard Button_Action and text fields on Offer_A / Offer_B.");
            return false;
        }

        SetVisible(true);
        transform.SetAsLastSibling();
        return true;
    }

    public void Hide()
    {
        if (!IsAlive(this))
            return;

        ClearOfferSlots();
        SetVisible(false);
    }

    void EnsureVisibleHierarchy()
    {
        Transform node = transform;
        while (node != null)
        {
            if (!node.gameObject.activeSelf)
                node.gameObject.SetActive(true);
            node = node.parent;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && !canvas.gameObject.activeInHierarchy)
            canvas.gameObject.SetActive(true);
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

        gameObject.SetActive(visible);

        if (IsAlive(overlayRoot) && overlayRoot.gameObject != gameObject)
            overlayRoot.gameObject.SetActive(visible);

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

    static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

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

    static Transform FindDeepChild(Transform parent, string childName)
    {
        return FindChildRecursive(parent, childName);
    }

    public struct ChestLootOfferEntry
    {
        public EquipmentInstance item;
        public System.Action onPick;
    }
}
