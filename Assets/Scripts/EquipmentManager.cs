using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Owns inventory, per-slot stat progression tiers, chest item generation, and equip/unequip orchestration.
/// </summary>
[AddComponentMenu("FatesRoll/Equipment Manager")]
public class EquipmentManager : GameServiceBehaviour<EquipmentManager>
{
    private const string DefaultCatalogPath = "Assets/Data/Equipment/EquipmentCatalog.asset";
    private const string CatalogResourcesPath = "Equipment/EquipmentCatalog";
    private const string DefaultIconDatabasePath = "Assets/Data/Equipment/EquipmentIconDatabase.asset";
    private const string IconDatabaseResourcesPath = "Equipment/EquipmentIconDatabase";

    [Header("Data")]
    [SerializeField] private EquipmentCatalog catalog;
    [SerializeField] private EquipmentIconDatabase iconDatabase;

    [Header("Generation")]
    [Tooltip("How many stat lines each new chest item rolls.")]
    [SerializeField] private int statsPerItem = 2;
    [Tooltip("Starting tier for every stat on a slot the first time that slot drops loot.")]
    [SerializeField] private int startingStatTier = 1;
    [Tooltip("How much a stat tier increases when Steve acquires an item with that stat on that slot.")]
    [SerializeField] private int statTierIncrementOnAcquire = 1;
    [Tooltip("Cap for per-slot stat tiers used when generating new items.")]
    [SerializeField] private int maxStatTierPerSlot = 99;

    [Header("Inventory")]
    [SerializeField] private List<EquipmentInstance> inventory = new List<EquipmentInstance>();
    [SerializeField] private List<EquipmentSlotStatProgression> slotProgressions = new List<EquipmentSlotStatProgression>();
    [SerializeField] private int chestsOpenedCount;

    public static event Action InventoryChanged;
    public static event Action EquipmentChanged;

    public IReadOnlyList<EquipmentInstance> Inventory => inventory;
    public EquipmentCatalog Catalog => catalog;
    public int ChestsOpenedCount => chestsOpenedCount;

    protected override void Awake()
    {
        base.Awake();
        EnsureReferences();
        EnsureProgressionRows();
        foreach (var item in inventory)
            item?.EnsureInstanceId();
    }

    public void EnsureReferences()
    {
        if (catalog == null)
        {
#if UNITY_EDITOR
            catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(DefaultCatalogPath);
#endif
            if (catalog == null)
                catalog = Resources.Load<EquipmentCatalog>(CatalogResourcesPath);
        }

        if (iconDatabase == null)
        {
#if UNITY_EDITOR
            iconDatabase = AssetDatabase.LoadAssetAtPath<EquipmentIconDatabase>(DefaultIconDatabasePath);
#endif
            if (iconDatabase == null)
                iconDatabase = Resources.Load<EquipmentIconDatabase>(IconDatabaseResourcesPath);
        }
    }

    public EquipmentInstance GenerateChestItem(
        EquipmentChestCategory category,
        EquipmentItemDefinition forcedDefinition = null)
    {
        if (forcedDefinition != null)
            return GenerateChestItemForSlot(forcedDefinition.slot, forcedDefinition);

        EnsureReferences();
        var definition = PickRandomDefinition(category);
        if (definition == null)
        {
            Debug.LogWarning($"EquipmentManager: no definition for chest category {category}.");
            return null;
        }

        return GenerateChestItemForSlot(definition.slot, definition);
    }

    /// <summary>Random item across chest loot slots (weapon, armor, head, cape, ring, necklace, boots, gloves).</summary>
    public EquipmentInstance GenerateRandomPlayerSlotItem() => GenerateRandomChestLootItem();

    /// <summary>Random chest item, optionally excluding one slot so A/B can differ.</summary>
    public EquipmentInstance GenerateRandomChestLootItem(EquipmentSlotType? excludeSlot = null)
    {
        EnsureReferences();
        if (catalog == null)
            return null;

        var slots = BuildAvailableChestLootSlots();
        if (excludeSlot.HasValue)
            slots.RemoveAll(s => s == excludeSlot.Value);

        if (slots.Count == 0)
            slots = BuildAvailableChestLootSlots();

        if (slots.Count == 0)
        {
            Debug.LogWarning("EquipmentManager: catalog has no items for any chest loot slot.");
            return null;
        }

        var slot = slots[UnityEngine.Random.Range(0, slots.Count)];
        return GenerateChestItemForSlot(slot);
    }

    public EquipmentInstance GenerateChestItemForSlot(
        EquipmentSlotType slot,
        EquipmentItemDefinition forcedDefinition = null)
    {
        EnsureReferences();
        if (!EquipmentSlots.IsPlayerSlot(slot))
        {
            Debug.LogWarning($"EquipmentManager: {slot} is not a player equipment slot.");
            return null;
        }

        var definition = forcedDefinition ?? catalog?.GetRandomBySlot(slot);
        if (definition == null)
        {
            Debug.LogWarning($"EquipmentManager: no catalog item for slot {EquipmentSlots.GetDisplayName(slot)}.");
            return null;
        }

        var progression = GetOrCreateProgression(definition.slot);
        int statCount = Mathf.Clamp(statsPerItem, 1, 4);
        var bonuses = EquipmentStatRoller.RollStatsFromSlotProgression(progression, statCount);
        var icon = iconDatabase != null ? iconDatabase.PickRandomIcon(definition.slot) : null;
        var instance = new EquipmentInstance(definition, bonuses, chestsOpenedCount, icon);
        GlobalSettings.LogGameplay(
            $"EquipmentManager: generated {instance.BuildChoiceLabel()} ({EquipmentSlots.GetDisplayName(definition.slot)}).");
        return instance;
    }

    /// <summary>Adds item to inventory, advances slot stat tiers, auto-equips if slot is empty.</summary>
    public bool AcquireItem(EquipmentInstance item)
    {
        if (item?.definition == null)
            return false;

        item.EnsureInstanceId();
        if (ContainsInstance(item.instanceId))
            return false;

        inventory.Add(item);
        AdvanceStatTiersForAcquire(item);

        if (!IsSlotOccupied(item.definition.slot))
            EquipFromInventory(item);

        NotifyInventoryChanged();
        return true;
    }

    public bool EquipFromInventory(EquipmentInstance item)
    {
        if (item?.definition == null || !inventory.Contains(item))
            return false;

        var heroEquip = FindHeroEquipment();
        if (heroEquip == null)
            return false;

        EquipmentSlotType slot = item.definition.slot;
        var previous = heroEquip.GetEquipped(slot);
        if (previous != null && previous != item)
        {
            previous.EnsureInstanceId();
            if (!InventoryContainsInstance(previous.instanceId))
                inventory.Add(previous);
        }

        inventory.Remove(item);
        heroEquip.Equip(item);
        NotifyEquipmentChanged();
        NotifyInventoryChanged();
        return true;
    }

    public bool UnequipToInventory(EquipmentSlotType slot)
    {
        var heroEquip = FindHeroEquipment();
        if (heroEquip == null)
            return false;

        var equipped = heroEquip.GetEquipped(slot);
        if (equipped == null)
            return false;

        heroEquip.Unequip(slot);
        equipped.EnsureInstanceId();
        if (!inventory.Contains(equipped))
            inventory.Add(equipped);

        NotifyEquipmentChanged();
        NotifyInventoryChanged();
        return true;
    }

    public void RegisterChestOpened()
    {
        chestsOpenedCount++;
    }

    public EquipmentSlotStatProgression GetProgression(EquipmentSlotType slot)
    {
        EnsureProgressionRows();
        foreach (var row in slotProgressions)
        {
            if (row != null && row.slot == slot)
                return row;
        }

        return null;
    }

    public int GetStatTier(EquipmentSlotType slot, EquipmentPrimaryStat stat)
    {
        var row = GetProgression(slot);
        return row != null ? row.GetTier(stat) : startingStatTier;
    }

    public IEnumerable<EquipmentInstance> GetEquippedItems()
    {
        var heroEquip = FindHeroEquipment();
        if (heroEquip == null)
            yield break;

        foreach (var pair in heroEquip.Equipped)
        {
            if (pair.Value != null)
                yield return pair.Value;
        }
    }

    public bool IsSlotOccupied(EquipmentSlotType slot)
    {
        var heroEquip = FindHeroEquipment();
        return heroEquip != null && heroEquip.GetEquipped(slot) != null;
    }

    public EquipmentInstance GetEquipped(EquipmentSlotType slot)
    {
        return FindHeroEquipment()?.GetEquipped(slot);
    }

    EquipmentItemDefinition PickRandomDefinition(EquipmentChestCategory category)
    {
        if (catalog == null)
            return null;

        var pool = catalog.GetByCategory(category);
        if (pool == null || pool.Count == 0)
            return null;

        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    EquipmentSlotStatProgression GetOrCreateProgression(EquipmentSlotType slot)
    {
        slotProgressions ??= new List<EquipmentSlotStatProgression>();
        foreach (var row in slotProgressions)
        {
            if (row != null && row.slot == slot)
                return row;
        }

        var created = new EquipmentSlotStatProgression
        {
            slot = slot,
            strengthTier = startingStatTier,
            agilityTier = startingStatTier,
            vitalityTier = startingStatTier,
            luckTier = startingStatTier
        };
        slotProgressions.Add(created);
        return created;
    }

    void EnsureProgressionRows()
    {
        slotProgressions ??= new List<EquipmentSlotStatProgression>();
        foreach (var slot in EquipmentSlots.ChestLootSlots)
            GetOrCreateProgression(slot);

        foreach (var slot in EquipmentSlots.PlayerSlots)
        {
            if (System.Array.IndexOf(EquipmentSlots.ChestLootSlots, slot) >= 0)
                continue;
            GetOrCreateProgression(slot);
        }
    }

    List<EquipmentSlotType> BuildAvailableChestLootSlots()
    {
        var slots = new List<EquipmentSlotType>(EquipmentSlots.ChestLootSlots.Length);
        foreach (var slot in EquipmentSlots.ChestLootSlots)
        {
            if (catalog.GetBySlot(slot).Count > 0)
                slots.Add(slot);
        }

        return slots;
    }

    void AdvanceStatTiersForAcquire(EquipmentInstance item)
    {
        if (item?.statBonuses == null || item.definition == null)
            return;

        var progression = GetOrCreateProgression(item.definition.slot);
        foreach (var bonus in item.statBonuses)
            progression.AdvanceTier(bonus.stat, statTierIncrementOnAcquire, maxStatTierPerSlot);
    }

    bool ContainsInstance(string instanceId)
    {
        return InventoryContainsInstance(instanceId) || EquippedContainsInstance(instanceId);
    }

    bool InventoryContainsInstance(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return false;

        foreach (var item in inventory)
        {
            if (item != null && item.instanceId == instanceId)
                return true;
        }

        return false;
    }

    bool EquippedContainsInstance(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return false;

        foreach (var equipped in GetEquippedItems())
        {
            if (equipped != null && equipped.instanceId == instanceId)
                return true;
        }

        return false;
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

    static void NotifyInventoryChanged() => InventoryChanged?.Invoke();
    static void NotifyEquipmentChanged() => EquipmentChanged?.Invoke();
}
