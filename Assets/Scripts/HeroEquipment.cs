using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("Default head (when slot empty)")]
    [SerializeField] private string defaultHeadBaseId = "Head01_Male";
    [SerializeField] private string defaultEyesId = "Eye01";
    [SerializeField] private string defaultEyebrowId = "Eyebrow01";

    private readonly Dictionary<EquipmentSlotType, EquipmentInstance> equipped = new Dictionary<EquipmentSlotType, EquipmentInstance>();
    private readonly Dictionary<EquipmentSlotType, GameObject> spawnedVisuals = new Dictionary<EquipmentSlotType, GameObject>();
    private readonly Dictionary<string, Transform> rigChildrenByName = new Dictionary<string, Transform>();
    private readonly HashSet<string> bodyArmorChildNames = new HashSet<string>();
    private readonly HashSet<string> headBaseChildNames = new HashSet<string>();
    private readonly HashSet<string> headArmorChildNames = new HashSet<string>();
    private readonly HashSet<string> hatChildNames = new HashSet<string>();
    private readonly HashSet<string> hairChildNames = new HashSet<string>();
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
        HideAllHeadVariants();
        EnsureBaseBodyVisible();
        EnsureDefaultHeadParts();
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
        headBaseChildNames.Clear();
        headArmorChildNames.Clear();
        hatChildNames.Clear();
        hairChildNames.Clear();

        if (rigRoot == null)
            return;

        foreach (var t in rigRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!rigChildrenByName.ContainsKey(t.name))
                rigChildrenByName[t.name] = t;

            string n = t.name;
            if (n.StartsWith("Body") && n.Length <= 7)
                bodyArmorChildNames.Add(n);
            else if (n.StartsWith("HeadArmor") && n.Length <= 12)
                headArmorChildNames.Add(n);
            else if (n.StartsWith("Head") && n.Length <= 12 && !n.Contains("Armor"))
                headBaseChildNames.Add(n);
            else if (n.StartsWith("Hat") && n.Length <= 7)
                hatChildNames.Add(n);
            else if (n.StartsWith("Hair") && n.Length <= 8)
                hairChildNames.Add(n);
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

    private void HideAllHeadBaseVariants()
    {
        foreach (string name in headBaseChildNames)
        {
            if (rigChildrenByName.TryGetValue(name, out Transform t))
                t.gameObject.SetActive(false);
        }
    }

    private void HideAllHeadArmorVariants()
    {
        foreach (string name in headArmorChildNames)
        {
            if (rigChildrenByName.TryGetValue(name, out Transform t))
                t.gameObject.SetActive(false);
        }
    }

    private void HideAllHatVariants()
    {
        foreach (string name in hatChildNames)
        {
            if (rigChildrenByName.TryGetValue(name, out Transform t))
                t.gameObject.SetActive(false);
        }
    }

    private void HideAllHairVariants()
    {
        foreach (string name in hairChildNames)
        {
            if (rigChildrenByName.TryGetValue(name, out Transform t))
                t.gameObject.SetActive(false);
        }
    }

    private void HideAllHeadVariants()
    {
        HideAllHeadArmorVariants();
        HideAllHatVariants();
        HideAllHairVariants();
    }

    private void EnsureBaseBodyVisible()
    {
        if (equipped.ContainsKey(EquipmentSlotType.BodyArmor))
            return;

        if (rigChildrenByName.TryGetValue("Body01", out Transform body01))
            body01.gameObject.SetActive(true);
    }

    /// <summary>Re-applies rig toggles / prefab visuals after respawn (inventory is unchanged).</summary>
    public void ReapplyEquippedVisuals()
    {
        ResolveRig();
        CacheRigChildren();

        var snapshot = new List<KeyValuePair<EquipmentSlotType, EquipmentInstance>>(equipped);
        foreach (var pair in snapshot)
        {
            if (pair.Value?.definition == null)
                continue;

            UnequipVisual(pair.Key);
            ApplyVisual(pair.Value);
        }

        RefreshStatBonuses();
        hero?.GetComponent<SteveAnimator>()?.UpdateStance();
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
        if (slot == EquipmentSlotType.MainHand || slot == EquipmentSlotType.OffHand)
            hero?.GetComponent<SteveAnimator>()?.UpdateStance();
        GlobalSettings.LogGameplay($"Equipped {instance.BuildChoiceLabel()} in {slot}.");
        return true;
    }

    public void Unequip(EquipmentSlotType slot)
    {
        UnequipVisual(slot);
        equipped.Remove(slot);
        RefreshStatBonuses();

        if (slot == EquipmentSlotType.MainHand || slot == EquipmentSlotType.OffHand)
            hero?.GetComponent<SteveAnimator>()?.UpdateStance();
    }

    private void UnequipVisual(EquipmentSlotType slot)
    {
        if (spawnedVisuals.TryGetValue(slot, out GameObject go) && go != null)
            DestroyEquippedObject(go);
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
        else if (slot == EquipmentSlotType.HeadHelmet)
        {
            HideAllHeadVariants();
        }
        else if (slot == EquipmentSlotType.HeadBase)
        {
            HideAllHeadBaseVariants();
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
            }
            else if (def.slot == EquipmentSlotType.Cape)
            {
                DisableCloakChildren();
            }
            else if (def.slot == EquipmentSlotType.HeadHelmet)
            {
                HideAllHeadVariants();
            }
            else if (def.slot == EquipmentSlotType.HeadBase)
            {
                HideAllHeadBaseVariants();
            }

            if (rigChildrenByName.TryGetValue(def.rigChildName, out Transform child))
                child.gameObject.SetActive(true);

            return;
        }

        if (def.visualPrefab == null)
            return;

        // If it's a HeadHelmet prefab, we should hide built-in hair/hats/armor to prevent clipping
        if (def.slot == EquipmentSlotType.HeadHelmet)
        {
            HideAllHeadVariants();
        }

        Transform parent = GetSocket(def.slot);
        if (parent == null)
        {
            Debug.LogWarning($"HeroEquipment: no socket for {def.slot}");
            return;
        }

        if (def.slot == EquipmentSlotType.MainHand && IsTwoHanded(def))
            Unequip(EquipmentSlotType.OffHand);

        GameObject visual = SpawnVisualPrefab(def.visualPrefab, parent);
if (visual == null)
            return;

        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        // All equipment should use the X-Ray layer (8) so they glow blue when obscured,
        // just like Steve's base body. The renderer stencil logic prevents them from glowing in the open.
        SetLayerRecursiveInternal(visual, 8);

        spawnedVisuals[def.slot] = visual;
    }

    /// <summary>Updates layers of all currently equipped visuals (usually called after character initialization).</summary>
    public void RefreshAttachmentLayers()
    {
        foreach (var kv in spawnedVisuals)
        {
            if (kv.Value == null) continue;
            SetLayerRecursiveInternal(kv.Value, 8);
        }
    }

    private static void SetLayerRecursiveInternal(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursiveInternal(child.gameObject, layer);
    }

    private static GameObject SpawnVisualPrefab(GameObject prefab, Transform parent)
    {
        if (prefab == null || parent == null)
            return null;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#endif
        return Instantiate(prefab, parent);
    }

    private static void DestroyEquippedObject(Object obj)
    {
        if (obj == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(obj);
            return;
        }
#endif
        Object.Destroy(obj);
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
            EquipmentSlotType.HeadBase => socketHead,
            EquipmentSlotType.HeadEyes => socketHead,
            EquipmentSlotType.HeadHelmet => socketHead,
            EquipmentSlotType.HeadFacial => socketHead,
            EquipmentSlotType.Cape => FindBone(rigRoot, "CloakBone01") ?? rigRoot,
            _ => null
        };
    }

    /// <summary>Base head + eyes + brows for any empty head slots.</summary>
    public void EnsureDefaultHeadParts()
    {
        if (rigRoot == null)
            ResolveRig();

        TryEquipDefaultCatalogItem(defaultHeadBaseId, EquipmentSlotType.HeadBase);
        TryEquipDefaultCatalogItem(defaultEyesId, EquipmentSlotType.HeadEyes);
        TryEquipDefaultCatalogItem(defaultEyebrowId, EquipmentSlotType.HeadFacial);
    }

    /// <summary>Clears all head-slot visuals and re-applies defaults (editor setup).</summary>
    public void ResetHeadPartsToDefault()
    {
        foreach (EquipmentSlotType slot in new[]
                 {
                     EquipmentSlotType.HeadBase, EquipmentSlotType.HeadEyes, EquipmentSlotType.HeadHelmet,
                     EquipmentSlotType.HeadFacial
                 })
        {
            Unequip(slot);
        }

        EnsureDefaultHeadParts();
    }

    private void TryEquipDefaultCatalogItem(string itemId, EquipmentSlotType slot)
    {
        if (string.IsNullOrEmpty(itemId) || GetEquipped(slot) != null)
            return;

        var def = ResolveCatalogDefinition(itemId);
        if (def == null)
            return;

        Equip(new EquipmentInstance(def, new List<EquipmentStatBonus>(), 0));
    }

    private static EquipmentItemDefinition ResolveCatalogDefinition(string itemId)
    {
#if UNITY_EDITOR
        var catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>("Assets/Data/Equipment/EquipmentCatalog.asset");
        if (catalog != null)
        {
            foreach (var item in catalog.items)
            {
                if (item != null && item.itemId == itemId)
                    return item;
            }
        }
#endif
        var resourcesCatalog = Resources.Load<EquipmentCatalog>("Equipment/EquipmentCatalog");
        if (resourcesCatalog == null)
            return null;

        foreach (var item in resourcesCatalog.items)
        {
            if (item != null && item.itemId == itemId)
                return item;
        }

        return null;
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
                    ?? GetEquipped(EquipmentSlotType.HeadBase)
                    ?? GetEquipped(EquipmentSlotType.HeadHelmet)
                    ?? GetEquipped(EquipmentSlotType.HeadEyes)
                    ?? GetEquipped(EquipmentSlotType.HeadFacial)
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
