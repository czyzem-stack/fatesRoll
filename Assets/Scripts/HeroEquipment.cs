using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Equips gear on Steve's modular rig: visuals on sockets / body toggles; stat-only slots apply bonuses only.
/// </summary>
[AddComponentMenu("FatesRoll/Hero Equipment")]
public class HeroEquipment : MonoBehaviour
{
    [Tooltip("Modular character root (e.g. MC02 / Steve_Base). Auto-found in children if empty.")]
    [SerializeField] private Transform rigRoot;

    [Header("Sockets (auto-resolved from rig bone names)")]
    [SerializeField] private Transform socketMainHand;
    [SerializeField] private Transform socketOffHand;
    [SerializeField] private Transform socketHead;

    private readonly Dictionary<EquipmentSlotType, EquipmentInstance> equipped = new Dictionary<EquipmentSlotType, EquipmentInstance>();
    private readonly Dictionary<EquipmentSlotType, GameObject> spawnedVisuals = new Dictionary<EquipmentSlotType, GameObject>();
    private readonly Dictionary<string, Transform> rigChildrenByName = new Dictionary<string, Transform>();
    private readonly HashSet<string> bodyArmorChildNames = new HashSet<string>();
    private PlayerStats playerStats;
    private HeroController hero;

    public IReadOnlyDictionary<EquipmentSlotType, EquipmentInstance> Equipped => equipped;

    public EquipmentInstance GetEquipped(EquipmentSlotType slot) =>
        equipped.TryGetValue(slot, out EquipmentInstance inst) ? inst : null;

    public Animator GetRigAnimator() => rigRoot != null ? rigRoot.GetComponent<Animator>() : null;

    private void Awake()
    {
        hero = GetComponent<HeroController>();
        playerStats = GetComponent<PlayerStats>();
        ResolveRig();
        CacheRigChildren();
        HideAllBodyVariants();
        EnsureBaseBodyVisible();
    }

    public void ResolveRig()
    {
        if (rigRoot != null)
        {
            ResolveSockets();
            return;
        }

        foreach (var anim in GetComponentsInChildren<Animator>(true))
        {
            if (anim.gameObject.name.StartsWith("MC") || anim.gameObject.name.Contains("Steve_Base"))
            {
                rigRoot = anim.transform;
                break;
            }
        }

        if (rigRoot == null)
        {
            foreach (Transform child in transform)
            {
                if (child.GetComponentInChildren<Animator>(true) != null)
                {
                    rigRoot = child;
                    break;
                }
            }
        }

        ResolveSockets();
    }

    private void ResolveSockets()
    {
        if (rigRoot == null)
            return;

        socketMainHand ??= FindBone(rigRoot, "weapon_r");
        socketOffHand ??= FindBone(rigRoot, "weapon_l");
        socketHead ??= FindBone(rigRoot, "head");
    }

    private static Transform FindBone(Transform root, string boneName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == boneName)
                return t;
        }

        return null;
    }

    private void CacheRigChildren()
    {
        rigChildrenByName.Clear();
        bodyArmorChildNames.Clear();
        if (rigRoot == null)
            return;

        foreach (var t in rigRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!rigChildrenByName.ContainsKey(t.name))
                rigChildrenByName[t.name] = t;

            if (t.name.StartsWith("Body") && t.name.Length <= 7)
                bodyArmorChildNames.Add(t.name);
        }
    }

    private void HideAllBodyVariants()
    {
        foreach (string name in bodyArmorChildNames)
        {
            if (rigChildrenByName.TryGetValue(name, out Transform t))
                t.gameObject.SetActive(false);
        }
    }

    private void EnsureBaseBodyVisible()
    {
        if (equipped.ContainsKey(EquipmentSlotType.BodyArmor))
            return;

        if (rigChildrenByName.TryGetValue("Body01", out Transform body01))
            body01.gameObject.SetActive(true);
    }

    public bool Equip(EquipmentInstance instance)
    {
        if (instance?.definition == null)
            return false;

        EquipmentSlotType slot = instance.definition.slot;
        UnequipVisual(slot);
        equipped[slot] = instance;
        ApplyVisual(instance);
        RefreshStatBonuses();
        RefreshWeaponStance();
        GlobalSettings.LogGameplay($"Equipped {instance.BuildChoiceLabel()} in {slot}.");
        return true;
    }

    public void Unequip(EquipmentSlotType slot)
    {
        UnequipVisual(slot);
        equipped.Remove(slot);
        RefreshStatBonuses();
        RefreshWeaponStance();
    }

    private void RefreshWeaponStance()
    {
        var stance = GetComponent<HeroWeaponStance>();
        if (stance != null)
            stance.RefreshFromEquipment();
    }

    private void UnequipVisual(EquipmentSlotType slot)
    {
        if (spawnedVisuals.TryGetValue(slot, out GameObject go) && go != null)
            Destroy(go);
        spawnedVisuals.Remove(slot);

        if (slot == EquipmentSlotType.BodyArmor)
        {
            HideAllBodyVariants();
            EnsureBaseBodyVisible();
        }
        else if (slot == EquipmentSlotType.Cape)
        {
            DisableCloakChildren();
        }
    }

    private void ApplyVisual(EquipmentInstance instance)
    {
        var def = instance.definition;
        if (def.IsStatOnlySlot)
            return;

        if (def.useRigChildToggle && !string.IsNullOrEmpty(def.rigChildName))
        {
            if (def.slot == EquipmentSlotType.BodyArmor)
            {
                HideAllBodyVariants();
                if (rigChildrenByName.TryGetValue(def.rigChildName, out Transform body))
                    body.gameObject.SetActive(true);
            }
            else if (def.slot == EquipmentSlotType.Cape)
            {
                DisableCloakChildren();
                if (rigChildrenByName.TryGetValue(def.rigChildName, out Transform cloak))
                    cloak.gameObject.SetActive(true);
            }

            return;
        }

        if (def.visualPrefab == null)
            return;

        Transform parent = GetSocket(def.slot);
        if (parent == null)
        {
            Debug.LogWarning($"HeroEquipment: no socket for {def.slot}");
            return;
        }

        if (def.slot == EquipmentSlotType.MainHand && IsTwoHanded(def))
            Unequip(EquipmentSlotType.OffHand);

        GameObject visual = Instantiate(def.visualPrefab, parent);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        spawnedVisuals[def.slot] = visual;
    }

    private void DisableCloakChildren()
    {
        foreach (var kv in rigChildrenByName)
        {
            if (kv.Key.StartsWith("Cloak") && kv.Key.Length <= 7)
                kv.Value.gameObject.SetActive(false);
        }
    }

    private Transform GetSocket(EquipmentSlotType slot)
    {
        return slot switch
        {
            EquipmentSlotType.MainHand => socketMainHand,
            EquipmentSlotType.OffHand => socketOffHand,
            EquipmentSlotType.Head => socketHead,
            EquipmentSlotType.Cape => FindBone(rigRoot, "CloakBone01") ?? rigRoot,
            _ => null
        };
    }

    private static bool IsTwoHanded(EquipmentItemDefinition def)
    {
        if (def == null)
            return false;
        string id = def.itemId ?? string.Empty;
        string name = def.displayName ?? string.Empty;
        return id.Contains("THS") || name.Contains("Two-Hand") ||
               (def.visualPrefab != null && def.visualPrefab.name.Contains("THS"));
    }

    public void RefreshStatBonuses()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

        if (playerStats == null)
            return;

        float str = 0f, agi = 0f, vit = 0f, luck = 0f;
        foreach (var kv in equipped)
        {
            if (kv.Value?.statBonuses == null)
                continue;

            foreach (var b in kv.Value.statBonuses)
            {
                switch (b.stat)
                {
                    case EquipmentPrimaryStat.Strength: str += b.amount; break;
                    case EquipmentPrimaryStat.Agility: agi += b.amount; break;
                    case EquipmentPrimaryStat.Vitality: vit += b.amount; break;
                    case EquipmentPrimaryStat.Luck: luck += b.amount; break;
                }
            }
        }

        float oldMax = playerStats.MaxHP;
        playerStats.SetEquipmentBonuses(str, agi, vit, luck);
        playerStats.CalculateAllDerivedStats();
        playerStats.currentHP = Mathf.Min(playerStats.currentHP + (playerStats.MaxHP - oldMax), playerStats.MaxHP);

        if (hero != null)
            hero.UpdateHealthUI();
    }

    /// <summary>Best equipped item in a chest loot category (for upgrade rolls).</summary>
    public EquipmentInstance GetReferenceForCategory(EquipmentChestCategory category)
    {
        switch (category)
        {
            case EquipmentChestCategory.Weapon:
                return GetEquipped(EquipmentSlotType.MainHand) ?? GetEquipped(EquipmentSlotType.OffHand);
            case EquipmentChestCategory.Armor:
                return GetEquipped(EquipmentSlotType.BodyArmor)
                    ?? GetEquipped(EquipmentSlotType.Head)
                    ?? GetEquipped(EquipmentSlotType.Cape)
                    ?? GetEquipped(EquipmentSlotType.Boots)
                    ?? GetEquipped(EquipmentSlotType.Gloves);
            case EquipmentChestCategory.Accessory:
                return GetEquipped(EquipmentSlotType.Ring) ?? GetEquipped(EquipmentSlotType.Necklace);
            default:
                return null;
        }
    }

    public EquipmentInstance GetReferenceForItemDefinition(EquipmentItemDefinition def)
    {
        if (def == null)
            return null;

        var inSlot = GetEquipped(def.slot);
        if (inSlot != null)
            return inSlot;

        return GetReferenceForCategory(def.chestCategory);
    }

#if UNITY_EDITOR
    public void EditorSetRigRoot(Transform root)
    {
        rigRoot = root;
        ResolveSockets();
        CacheRigChildren();
    }
#endif
}
