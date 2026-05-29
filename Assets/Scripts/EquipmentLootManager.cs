using System.Collections;

using System.Collections.Generic;

using TMPro;

using UnityEngine;

#if UNITY_EDITOR

using UnityEditor;

#endif



/// <summary>Treasure chest rewards — shows visual loot cards and adds items via <see cref="EquipmentManager"/>.</summary>

[AddComponentMenu("FatesRoll/Equipment Loot Manager")]

public class EquipmentLootManager : GameServiceBehaviour<EquipmentLootManager>

{

    private const string DefaultCatalogPath = "Assets/Data/Equipment/EquipmentCatalog.asset";

    private const string CatalogResourcesPath = "Equipment/EquipmentCatalog";



    [Header("Catalog")]
    [SerializeField] private EquipmentCatalog catalog;

    [Header("Legacy counters")]

    [SerializeField] private float startingStatBonus = 1f;

    [SerializeField] private float chestPowerScalar = 1.2f;

    [SerializeField] private int chestsOpenedCount;



    readonly Queue<ChestRewardRequest> pendingChests = new Queue<ChestRewardRequest>();

    ChestLootPopupUI popup;



    bool rewardFlowActive;

    bool waitingForChoice;



    public bool IsRewardFlowActive => rewardFlowActive;

    public bool HasPendingChestRewards => pendingChests.Count > 0;

    public EquipmentCatalog Catalog => catalog;

    public int ChestsOpenedCount => chestsOpenedCount;



    struct ChestRewardRequest

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



    protected override void OnDestroy()
    {
        popup = null;
        base.OnDestroy();
    }



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
            popup = ChestLootPopupUI.FindInScene();
            if (popup == null)
            {
                Debug.LogError(
                    "EquipmentLootManager: ChestLootOverlay missing from scene. Run FatesRoll → Equipment → Create Chest Loot Popup In Main Scene.");
                yield break;
            }

            while (pendingChests.Count > 0)
            {
                var request = pendingChests.Dequeue();
                yield return ShowChestPopupAndWait(request);

                if (GameServices.TryGet(out EquipmentManager equipmentManager))
                    equipmentManager.RegisterChestOpened();
                else
                    chestsOpenedCount++;
            }
        }
        finally
        {
            waitingForChoice = false;
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            rewardFlowActive = false;

            if (popup != null)
                popup.Hide();
        }

    }



    public float GetCurrentStatBonusPerRoll()

    {

        return startingStatBonus * Mathf.Pow(chestPowerScalar, chestsOpenedCount);

    }



    IEnumerator ShowChestPopupAndWait(ChestRewardRequest request)
    {
        waitingForChoice = false;

        if (!TryBuildChestOffer(request, out EquipmentInstance optionA, out EquipmentInstance optionB))
        {
            Debug.LogWarning("EquipmentLootManager: could not build chest offers — skipping.");
            yield break;
        }

        var offers = new List<ChestLootPopupUI.ChestLootOfferEntry>
        {
            BuildOfferEntry(optionA),
            BuildOfferEntry(optionB)
        };

        if (popup == null || !popup.Show(offers))
        {
            Debug.LogWarning("EquipmentLootManager: chest UI failed — auto-granting first offer so the game does not freeze.");
            CompleteChoice(optionA);
            yield break;
        }

        waitingForChoice = true;

        const float choiceTimeoutSeconds = 120f;
        float elapsed = 0f;
        while (waitingForChoice)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= choiceTimeoutSeconds)
            {
                Debug.LogWarning("EquipmentLootManager: chest choice timed out — auto-granting first offer.");
                CompleteChoice(optionA);
                break;
            }

            yield return null;
        }

        popup.Hide();
    }



    ChestLootPopupUI.ChestLootOfferEntry BuildOfferEntry(EquipmentInstance item)
    {
        return new ChestLootPopupUI.ChestLootOfferEntry
        {
            item = item,
            onPick = () => CompleteChoice(item)
        };
    }



    void CompleteChoice(EquipmentInstance chosen)

    {

        if (chosen != null && GameServices.TryGet(out EquipmentManager manager))

            manager.AcquireItem(chosen);

        else if (chosen != null)

            FindHeroEquipment()?.Equip(chosen);



        waitingForChoice = false;

    }



    static HeroEquipment FindHeroEquipment()

    {

        var hero = GameServices.Hero;

        if (hero == null)

            return null;

        var equip = hero.GetComponent<HeroEquipment>();

        if (equip == null)

            equip = hero.gameObject.AddComponent<HeroEquipment>();

        return equip;

    }



    bool TryBuildChestOffer(
        ChestRewardRequest request,
        out EquipmentInstance optionA,
        out EquipmentInstance optionB)
    {
        optionA = null;
        optionB = null;

        if (!GameServices.TryGet(out EquipmentManager manager))
        {
            Debug.LogError("EquipmentLootManager: EquipmentManager missing on bootstrap.");
            return false;
        }

        manager.EnsureReferences();

        if (request.forcedA != null)
            optionA = manager.GenerateChestItemForSlot(request.forcedA.slot, request.forcedA);
        else
            optionA = manager.GenerateRandomPlayerSlotItem();

        if (request.forcedB != null)
            optionB = manager.GenerateChestItemForSlot(request.forcedB.slot, request.forcedB);
        else
            optionB = manager.GenerateRandomPlayerSlotItem();

        return optionA != null && optionB != null;
    }
}


