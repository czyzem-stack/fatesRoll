#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Chest loot UI setup. Offer card layout lives in <see cref="ChestLootOfferCard.PrefabAssetPath"/> only —
/// menus wire references and place prefab instances; they do not regenerate card hierarchy.
/// </summary>
public static class ChestLootPopupSetup
{
    const string MainPath = MainSceneBootstrapCleanup.MainScenePath;
    const string CanvasPath = "MainUI_Canvas";
    const string OverlayName = "ChestLootOverlay";
    const string PopupInnerPath = "Assets/Resources/UI/ChestLootPopup.prefab";
    const string PopupInnerSourcePath =
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_Component_Popups/Popup_01_Basic_Demo.prefab";
    const string FramePath = "Assets/Resources/UI/ItemFrame_03_Green.prefab";
    const string FrameSourcePath =
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_Component_Frames/ItemFrame_03_Green.prefab";
    const string ButtonPath = "Assets/Resources/UI/Button_Rectangle_01_Convex_Green.prefab";
    const string ButtonSourcePath =
        "Assets/UI/GUI Pro-FantasyRPG/Prefabs/Prefabs_Component_Buttons/Button_Rectangle_01_Convex_Green.prefab";

    static readonly Vector2 PopupSize = new Vector2(1040f, 560f);
    static readonly float PopupScale = 0.88f;
    static readonly Vector2 CardSize = new Vector2(260f, 320f);

    [MenuItem("FatesRoll/Equipment/Bootstrap Chest Loot Offer Card Prefab")]
    public static void BootstrapChestLootOfferCardPrefab()
    {
        EquipmentBootstrapSetup.CopyChestLootUiPrefabsToResources();
        BuildOfferCardPrefabAsset(overwrite: true);
        Debug.Log(
            $"ChestLootPopupSetup: offer card prefab ready at {ChestLootOfferCard.PrefabAssetPath}. Open it in Prefab Mode to edit layout.");
    }

    [MenuItem("FatesRoll/Equipment/Refresh Chest Loot Overlay Offer Slots")]
    public static void RefreshChestLootOverlayOfferSlots()
    {
        if (!File.Exists(ChestLootPopupUI.PrefabAssetPath))
        {
            Debug.LogError($"RefreshChestLootOverlayOfferSlots: missing {ChestLootPopupUI.PrefabAssetPath}.");
            return;
        }

        EnsureOfferCardPrefabExists();

        GameObject root = PrefabUtility.LoadPrefabContents(ChestLootPopupUI.PrefabAssetPath);
        Transform offerCards = EnsureOfferCardsHost(root.transform);
        RemoveOfferSlotChildren(offerCards);
        PlaceOfferCardPrefabInstance(offerCards, ChestLootPopupUI.OfferSlotAName, unpackForScene: false);
        PlaceOfferCardPrefabInstance(offerCards, ChestLootPopupUI.OfferSlotBName, unpackForScene: false);
        WirePopupComponent(root);

        PrefabUtility.SaveAsPrefabAsset(root, ChestLootPopupUI.PrefabAssetPath);
        PrefabUtility.UnloadPrefabContents(root);
        CopyOverlayPrefabToResources();
        AssetDatabase.SaveAssets();

        Debug.Log(
            "ChestLootPopupSetup: overlay Offer_A/Offer_B are now nested instances of ChestLootOfferCard.prefab. Edit the card prefab to change layout.");
    }

    [MenuItem("FatesRoll/Equipment/Refresh Chest Loot Offer Cards In Main Scene")]
    public static void RefreshChestLootOfferCardsInMainScene()
    {
        if (!File.Exists(MainPath))
        {
            Debug.LogError($"ChestLootPopupSetup: missing {MainPath}.");
            return;
        }

        EnsureOfferCardPrefabExists();
        Scene scene = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        GameObject overlay = FindOrCreateSceneOverlay();
        if (overlay == null)
            return;

        SetupSceneOverlay(overlay);

        EditorUtility.SetDirty(overlay);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = overlay.transform.Find($"ChestLootPopup/OfferCards/{ChestLootPopupUI.OfferSlotAName}")?.gameObject ?? overlay;
        Debug.Log(
            "ChestLootPopupSetup: Offer_A and Offer_B are scene GameObjects under ChestLootOverlay — edit them in the Hierarchy.");
    }

    [MenuItem("FatesRoll/Equipment/Wire Chest Loot Popup In Main Scene")]
    public static void WireChestLootPopupInMainScene()
    {
        if (!File.Exists(MainPath))
        {
            Debug.LogError($"ChestLootPopupSetup: missing {MainPath}.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        GameObject overlay = FindSceneOverlay();
        if (overlay == null)
        {
            Debug.LogError("WireChestLootPopupInMainScene: no ChestLootOverlay — run Create Chest Loot Popup In Main Scene first.");
            return;
        }

        SetupSceneOverlay(overlay);
        EditorUtility.SetDirty(overlay);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = overlay;
        Debug.Log("ChestLootPopupSetup: wired ChestLootOverlay references (Offer_A, Offer_B, labels).");
    }

    [MenuItem("FatesRoll/Equipment/Create Chest Loot Popup In Main Scene")]
    public static void CreateChestLootPopupInMainScene()
    {
        EquipmentBootstrapSetup.CopyChestLootUiPrefabsToResources();
        EnsureOfferCardPrefabExists();

        if (!File.Exists(MainPath))
        {
            Debug.LogError($"ChestLootPopupSetup: missing {MainPath}.");
            return;
        }

        if (!File.Exists(ChestLootPopupUI.PrefabAssetPath))
            BuildOverlayPrefabAsset();

        Scene scene = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        Transform canvas = FindMainUiCanvas();
        if (canvas == null)
        {
            Debug.LogError("ChestLootPopupSetup: MainUI_Canvas not found in main.unity.");
            return;
        }

        GameObject overlay = FindOrCreateSceneOverlay();
        if (overlay == null)
            return;

        SetupSceneOverlay(overlay);

        overlay.transform.SetAsLastSibling();
        overlay.SetActive(false);

        EditorUtility.SetDirty(overlay);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = overlay;
        Debug.Log(
            "ChestLootPopupSetup: ChestLootOverlay placed in main.unity. Edit Offer_A / Offer_B as GameObjects under MainUI_Canvas → ChestLootOverlay → ChestLootPopup → OfferCards.");
    }

    [MenuItem("GameObject/FatesRoll/Chest Loot Offer Card", false, 10)]
    static void CreateChestLootOfferCardGameObject(MenuCommand command)
    {
        EnsureOfferCardPrefabExists();
        Transform parent = command.context as Transform;
        if (parent == null)
            return;

        GameObject cardGo = PlaceOfferCardAsSceneGameObject(parent, "ChestLootOfferCard");
        Selection.activeGameObject = cardGo;
    }

    [MenuItem("GameObject/FatesRoll/Chest Loot Offer Card", true)]
    static bool ValidateCreateChestLootOfferCardGameObject() =>
        Selection.activeTransform != null;

    static GameObject FindOrCreateSceneOverlay()
    {
        GameObject overlay = FindSceneOverlay();
        if (overlay != null)
            return overlay;

        Transform canvas = FindMainUiCanvas();
        if (canvas == null)
        {
            Debug.LogError("ChestLootPopupSetup: MainUI_Canvas not found in main.unity.");
            return null;
        }

        GameObject overlayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestLootPopupUI.PrefabAssetPath);
        overlay = overlayPrefab != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(overlayPrefab, canvas)
            : BuildOverlayHierarchy(canvas);
        overlay.name = OverlayName;
        return overlay;
    }

    static GameObject FindSceneOverlay()
    {
        ChestLootPopupUI existing = Object.FindAnyObjectByType<ChestLootPopupUI>(FindObjectsInactive.Include);
        if (existing != null)
            return existing.gameObject;

        Transform canvas = FindMainUiCanvas();
        if (canvas != null)
        {
            Transform byName = FindChildRecursive(canvas, OverlayName);
            if (byName != null)
                return byName.gameObject;
        }

        GameObject byNameGlobal = GameObject.Find(ChestLootPopupUI.OverlayObjectName);
        if (byNameGlobal != null && byNameGlobal.scene.IsValid())
            return byNameGlobal;

        return null;
    }

    static void SetupSceneOverlay(GameObject overlay)
    {
        UnpackPrefabInstanceRoot(overlay);
        EnsureSceneOfferCards(overlay);
        WirePopupComponent(overlay);
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

    static void EnsureSceneOfferCards(GameObject overlay)
    {
        Transform offerCards = EnsureOfferCardsHost(overlay.transform);
        if (offerCards == null)
            return;

        EnsureSceneOfferSlot(offerCards, ChestLootPopupUI.OfferSlotAName);
        EnsureSceneOfferSlot(offerCards, ChestLootPopupUI.OfferSlotBName);
    }

    static void EnsureSceneOfferSlot(Transform offerRoot, string slotName)
    {
        Transform existing = offerRoot.Find(slotName);
        if (existing == null)
        {
            PlaceOfferCardAsSceneGameObject(offerRoot, slotName);
            return;
        }

        ChestLootOfferCard card = existing.GetComponent<ChestLootOfferCard>();
        if (card == null)
            card = existing.gameObject.AddComponent<ChestLootOfferCard>();
        card.ResolveReferences();
    }

    /// <summary>Unpacks only the outermost prefab instance root (never nested children).</summary>
    static void UnpackPrefabInstanceRoot(GameObject go)
    {
        if (go == null || !PrefabUtility.IsPartOfPrefabInstance(go))
            return;

        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        if (root == null || !PrefabUtility.IsPartOfPrefabInstance(root))
            return;

        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    static void BuildOverlayPrefabAsset()
    {
        var host = new GameObject("ChestLootOverlay_Temp", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ChestLootPopupUI));
        var overlayRt = host.GetComponent<RectTransform>();
        Stretch(overlayRt);

        var dim = host.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        BuildPopupUnderOverlay(host.transform);
        WirePopupComponent(host);

        Directory.CreateDirectory("Assets/Prefabs/UI");
        PrefabUtility.SaveAsPrefabAsset(host, ChestLootPopupUI.PrefabAssetPath);
        Object.DestroyImmediate(host);
        CopyOverlayPrefabToResources();
    }

    static void BuildPopupUnderOverlay(Transform overlay)
    {
        GameObject popupInner = LoadPopupInnerPrefab();
        GameObject popup = popupInner != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(popupInner, overlay)
            : new GameObject("ChestLootPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        popup.name = "ChestLootPopup";
        var popupRt = popup.GetComponent<RectTransform>();
        popupRt.anchorMin = popupRt.anchorMax = new Vector2(0.5f, 0.5f);
        popupRt.pivot = new Vector2(0.5f, 0.5f);
        popupRt.anchoredPosition = Vector2.zero;
        popupRt.sizeDelta = PopupSize;
        popupRt.localScale = Vector3.one * PopupScale;

        HideDemoContent(popup.transform);
        RepositionSubtitle(FindNamedTmp(popup.transform, "Text_Info"));

        Transform offerCards = EnsureOfferCardsHost(popup.transform);
        RemoveOfferSlotChildren(offerCards);
        PlaceOfferCardPrefabInstance(offerCards, ChestLootPopupUI.OfferSlotAName, unpackForScene: false);
        PlaceOfferCardPrefabInstance(offerCards, ChestLootPopupUI.OfferSlotBName, unpackForScene: false);
    }

    static GameObject BuildOverlayHierarchy(Transform canvas)
    {
        GameObject overlayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestLootPopupUI.PrefabAssetPath);
        if (overlayPrefab != null)
            return (GameObject)PrefabUtility.InstantiatePrefab(overlayPrefab, canvas);

        var overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ChestLootPopupUI));
        overlay.transform.SetParent(canvas, false);
        overlay.transform.SetAsLastSibling();

        var overlayRt = overlay.GetComponent<RectTransform>();
        Stretch(overlayRt);

        var dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        BuildPopupUnderOverlay(overlay.transform);
        overlay.SetActive(false);
        return overlay;
    }

    static Transform EnsureOfferCardsHost(Transform popupOrOverlay)
    {
        Transform popup = popupOrOverlay.name == "ChestLootPopup"
            ? popupOrOverlay
            : popupOrOverlay.Find("ChestLootPopup");

        if (popup == null)
            return null;

        Transform offerCards = popup.Find("OfferCards");
        if (offerCards != null)
            return offerCards;

        var offerHost = new GameObject("OfferCards", typeof(RectTransform));
        offerHost.transform.SetParent(popup, false);
        var hostRt = offerHost.GetComponent<RectTransform>();
        hostRt.anchorMin = new Vector2(0.04f, 0.10f);
        hostRt.anchorMax = new Vector2(0.96f, 0.68f);
        hostRt.offsetMin = Vector2.zero;
        hostRt.offsetMax = Vector2.zero;

        var layout = offerHost.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 36f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        return offerHost.transform;
    }

    static void RemoveOfferSlotChildren(Transform offerRoot)
    {
        if (offerRoot == null)
            return;

        for (int i = offerRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = offerRoot.GetChild(i);
            if (child.name == ChestLootPopupUI.OfferSlotAName || child.name == ChestLootPopupUI.OfferSlotBName)
                Object.DestroyImmediate(child.gameObject);
        }
    }

    static GameObject PlaceOfferCardAsSceneGameObject(Transform offerRoot, string slotName)
    {
        ChestLootOfferCard card = PlaceOfferCardPrefabInstance(offerRoot, slotName, unpackForScene: true);
        return card != null ? card.gameObject : null;
    }

    static ChestLootOfferCard PlaceOfferCardPrefabInstance(Transform offerRoot, string slotName, bool unpackForScene)
    {
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestLootOfferCard.PrefabAssetPath);
        if (cardPrefab == null)
        {
            Debug.LogError($"PlaceOfferCardPrefabInstance: missing {ChestLootOfferCard.PrefabAssetPath}. Run Bootstrap Chest Loot Offer Card Prefab.");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, offerRoot);
        instance.name = slotName;

        if (unpackForScene)
            UnpackPrefabInstanceRoot(instance);

        var cardRt = instance.GetComponent<RectTransform>();
        if (cardRt != null)
            cardRt.sizeDelta = CardSize;

        var card = instance.GetComponent<ChestLootOfferCard>();
        if (card == null)
            card = instance.AddComponent<ChestLootOfferCard>();

        ChestLootOfferCard.DisableFrameDemoIconLayer(instance.transform);

        string pickLabel = slotName == ChestLootPopupUI.OfferSlotBName ? "B" : "A";
        WireOfferCardComponent(card, pickLabel, "SLOT", "Preview Item", "+1 STAT", "+1 STAT");

        card.ResolveReferences();
        return card;
    }

    static void EnsureOfferCardPrefabExists()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ChestLootOfferCard.PrefabAssetPath) == null)
            BuildOfferCardPrefabAsset(overwrite: false);
    }

    static void BuildOfferCardPrefabAsset(bool overwrite)
    {
        if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(ChestLootOfferCard.PrefabAssetPath) != null)
            return;

        Directory.CreateDirectory("Assets/Prefabs/UI");

        GameObject framePrefab = LoadFramePrefab();
        GameObject cardGo = framePrefab != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(framePrefab)
            : new GameObject("ChestLootOfferCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        cardGo.name = "ChestLootOfferCard";

        var cardRt = cardGo.GetComponent<RectTransform>();
        if (cardRt != null)
            cardRt.sizeDelta = CardSize;

        BuildCardLayout(cardGo.transform, "A", "WEAPON", "Iron Sword", "+1 STR", "+1 AGI");
        ChestLootOfferCard.DisableFrameDemoIconLayer(cardGo.transform);

        var card = cardGo.GetComponent<ChestLootOfferCard>();
        if (card == null)
            card = cardGo.AddComponent<ChestLootOfferCard>();

        WireOfferCardComponent(card, "A", "WEAPON", "Iron Sword", "+1 STR", "+1 AGI");

        PrefabUtility.SaveAsPrefabAsset(cardGo, ChestLootOfferCard.PrefabAssetPath);
        Object.DestroyImmediate(cardGo);
        AssetDatabase.SaveAssets();
    }

    static void BuildCardLayout(
        Transform card,
        string buttonLabel,
        string previewSlot,
        string previewName,
        string previewStat1,
        string previewStat2)
    {
        ChestLootOfferCard.DisableFrameDemoIconLayer(card);
        EnsureIconRoot(card);
        EnsureLabel(card, "Text_Slot", new Vector2(0.5f, 0.86f), new Vector2(220f, 24f), 18f, FontStyles.Bold,
            new Color(0.85f, 0.75f, 0.45f), previewSlot);
        EnsureLabel(card, "Text_Name", new Vector2(0.5f, 0.76f), new Vector2(220f, 28f), 22f, FontStyles.Bold,
            Color.white, previewName);
        EnsureLabel(card, "Text_Stat1", new Vector2(0.5f, 0.28f), new Vector2(220f, 24f), 18f, FontStyles.Normal,
            Color.white, previewStat1);
        EnsureLabel(card, "Text_Stat2", new Vector2(0.5f, 0.20f), new Vector2(220f, 24f), 18f, FontStyles.Normal,
            Color.white, previewStat2);
        EnsureActionButton(card, buttonLabel);
    }

    static void EnsureIconRoot(Transform card)
    {
        Transform iconRoot = card.Find("IconRoot");
        if (iconRoot == null)
        {
            var iconRootGo = new GameObject("IconRoot", typeof(RectTransform));
            iconRootGo.transform.SetParent(card, false);
            iconRoot = iconRootGo.transform;
            var iconRootRt = (RectTransform)iconRoot;
            iconRootRt.anchorMin = iconRootRt.anchorMax = new Vector2(0.5f, 0.52f);
            iconRootRt.pivot = new Vector2(0.5f, 0.5f);
            iconRootRt.sizeDelta = new Vector2(96f, 96f);
        }

        Transform icon = iconRoot.Find("Icon");
        if (icon == null)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(iconRoot, false);
            Stretch(iconGo.GetComponent<RectTransform>());
            var image = iconGo.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
        }
    }

    static void EnsureLabel(
        Transform parent,
        string name,
        Vector2 anchor,
        Vector2 size,
        float fontSize,
        FontStyles style,
        Color color,
        string previewText)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing == null)
        {
            go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            go.AddComponent<TextMeshProUGUI>();
        }
        else
        {
            go = existing.gameObject;
        }

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = previewText;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.raycastTarget = false;
    }

    static void EnsureActionButton(Transform card, string buttonLabel)
    {
        Transform existing = card.Find("Button_Action");
        GameObject buttonGo;
        if (existing != null)
        {
            buttonGo = existing.gameObject;
        }
        else
        {
            GameObject buttonPrefab = LoadButtonPrefab();
            buttonGo = buttonPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, card)
                : new GameObject("Button_Action", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGo.name = "Button_Action";
        }

        var rt = buttonGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.06f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(180f, 52f);

        var label = buttonGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null)
        {
            var textGo = new GameObject("Text_Action", typeof(RectTransform));
            textGo.transform.SetParent(buttonGo.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.fontSize = 28f;
        }

        label.gameObject.name = "Text_Action";
        label.text = buttonLabel;
        label.fontSize = 28f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
    }

    static void WireOfferCardComponent(
        ChestLootOfferCard card,
        string buttonLabel,
        string previewSlot,
        string previewName,
        string previewStat1,
        string previewStat2)
    {
        card.ResolveReferences();

        var so = new SerializedObject(card);
        so.FindProperty("iconImage").objectReferenceValue =
            card.transform.Find("IconRoot/Icon")?.GetComponent<Image>();
        so.FindProperty("slotLabel").objectReferenceValue = FindNamedTmp(card.transform, "Text_Slot");
        so.FindProperty("nameLabel").objectReferenceValue = FindNamedTmp(card.transform, "Text_Name");
        so.FindProperty("statLine1").objectReferenceValue = FindNamedTmp(card.transform, "Text_Stat1");
        so.FindProperty("statLine2").objectReferenceValue = FindNamedTmp(card.transform, "Text_Stat2");
        so.FindProperty("actionButton").objectReferenceValue =
            card.transform.Find("Button_Action")?.GetComponent<Button>();
        so.FindProperty("actionLabel").objectReferenceValue = FindNamedTmpDeep(card.transform, "Text_Action");
        so.ApplyModifiedPropertiesWithoutUndo();

        if (FindNamedTmp(card.transform, "Text_Slot") != null)
            FindNamedTmp(card.transform, "Text_Slot").text = previewSlot;
        if (FindNamedTmp(card.transform, "Text_Name") != null)
            FindNamedTmp(card.transform, "Text_Name").text = previewName;
        if (FindNamedTmp(card.transform, "Text_Stat1") != null)
            FindNamedTmp(card.transform, "Text_Stat1").text = previewStat1;
        if (FindNamedTmp(card.transform, "Text_Stat2") != null)
            FindNamedTmp(card.transform, "Text_Stat2").text = previewStat2;
        if (FindNamedTmpDeep(card.transform, "Text_Action") != null)
            FindNamedTmpDeep(card.transform, "Text_Action").text = buttonLabel;

        EditorUtility.SetDirty(card);
    }

    static void WirePopupComponent(GameObject overlay)
    {
        var ui = overlay.GetComponent<ChestLootPopupUI>();
        if (ui == null)
            ui = overlay.AddComponent<ChestLootPopupUI>();

        Transform popup = overlay.transform.Find("ChestLootPopup");
        Transform offerCards = popup != null ? popup.Find("OfferCards") : null;

        var slotA = offerCards != null
            ? offerCards.Find(ChestLootPopupUI.OfferSlotAName)?.GetComponent<ChestLootOfferCard>()
            : null;
        var slotB = offerCards != null
            ? offerCards.Find(ChestLootPopupUI.OfferSlotBName)?.GetComponent<ChestLootOfferCard>()
            : null;

        var so = new SerializedObject(ui);
        so.FindProperty("overlayRoot").objectReferenceValue = overlay.GetComponent<RectTransform>();
        so.FindProperty("popupPanel").objectReferenceValue = popup != null ? popup.GetComponent<RectTransform>() : null;
        so.FindProperty("titleLabel").objectReferenceValue =
            popup != null ? FindNamedTmp(popup, "Text_Title") : null;
        so.FindProperty("bodyLabel").objectReferenceValue =
            popup != null
                ? FindNamedTmp(popup, "Text_Info") ?? FindNamedTmp(popup, "Text_Description")
                : null;
        so.FindProperty("offerRoot").objectReferenceValue =
            offerCards != null ? offerCards.GetComponent<RectTransform>() : null;
        so.FindProperty("offerSlotA").objectReferenceValue = slotA;
        so.FindProperty("offerSlotB").objectReferenceValue = slotB;
        so.ApplyModifiedPropertiesWithoutUndo();

        ui.ResolveReferences();
        EditorUtility.SetDirty(ui);
    }

    static Transform FindMainUiCanvas()
    {
        GameObject canvasGo = GameObject.Find(CanvasPath);
        if (canvasGo != null)
            return canvasGo.transform;

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (canvas != null && canvas.gameObject.name.Contains("MainUI", System.StringComparison.OrdinalIgnoreCase))
                return canvas.transform;
        }

        return null;
    }

    static GameObject LoadPopupInnerPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupInnerPath);
        return prefab != null ? prefab : AssetDatabase.LoadAssetAtPath<GameObject>(PopupInnerSourcePath);
    }

    static GameObject LoadFramePrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FramePath);
        return prefab != null ? prefab : AssetDatabase.LoadAssetAtPath<GameObject>(FrameSourcePath);
    }

    static GameObject LoadButtonPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath);
        return prefab != null ? prefab : AssetDatabase.LoadAssetAtPath<GameObject>(ButtonSourcePath);
    }

    static void HideDemoContent(Transform popup)
    {
        Transform contentDemo = popup.Find("Content_Demo");
        if (contentDemo != null)
            contentDemo.gameObject.SetActive(false);

        Transform buttonOk = FindDeepChild(popup, "Button_OK");
        if (buttonOk != null)
            buttonOk.gameObject.SetActive(false);
    }

    static void RepositionSubtitle(TextMeshProUGUI body)
    {
        if (body == null)
            return;

        var rt = body.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.74f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(880f, 40f);
        rt.anchoredPosition = Vector2.zero;
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

    static TextMeshProUGUI FindNamedTmpDeep(Transform root, string objectName) => FindNamedTmp(root, objectName);

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

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void CopyOverlayPrefabToResources()
    {
        if (!File.Exists(ChestLootPopupUI.PrefabAssetPath))
            return;

        Directory.CreateDirectory("Assets/Resources/UI");
        string dest = "Assets/Resources/UI/ChestLootPopupOverlay.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(dest) == null)
            AssetDatabase.CopyAsset(ChestLootPopupUI.PrefabAssetPath, dest);
        else
            File.Copy(ChestLootPopupUI.PrefabAssetPath, dest, true);

        AssetDatabase.Refresh();
    }
}
#endif
