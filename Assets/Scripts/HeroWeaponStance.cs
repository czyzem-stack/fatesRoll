using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Picks the correct RPG Tiny Hero animator controller from equipped weapons.</summary>
[AddComponentMenu("FatesRoll/Hero Weapon Stance")]
public class HeroWeaponStance : MonoBehaviour
{
    private const string AnimFolder = "Assets/Heroes/Animator/";

    [SerializeField] private RuntimeAnimatorController unarmed;
    [SerializeField] private RuntimeAnimatorController singleSword;
    [SerializeField] private RuntimeAnimatorController swordAndShield;
    [SerializeField] private RuntimeAnimatorController twoHanded;
    [SerializeField] private RuntimeAnimatorController spear;
    [SerializeField] private RuntimeAnimatorController dualWield;
    [SerializeField] private RuntimeAnimatorController wand;
    [SerializeField] private RuntimeAnimatorController bow;

    private Animator animator;
    private bool yawFixApplied;
    private HeroStanceKind currentStance = HeroStanceKind.Unarmed;

    private enum HeroStanceKind
    {
        Unarmed,
        SingleSword,
        SwordAndShield,
        TwoHanded,
        Spear,
        DualWield,
        Wand,
        Bow
    }

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        var equipment = GetComponent<HeroEquipment>();
        equipment?.ResolveRig();

        animator = equipment != null ? equipment.GetRigAnimator() : GetComponentInChildren<Animator>();
        if (animator == null)
            return;

        animator.applyRootMotion = false;

        var hero = GetComponent<HeroController>();
        if (hero != null)
            hero.ApplyVisualLocomotionAlignment();
        else
            HeroLocomotionUtility.AlignVisualToAgentForward(transform, animator, ref yawFixApplied);

        RefreshFromEquipment();
    }

    public void RefreshFromEquipment()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator == null)
            return;

        HeroStanceKind next = ResolveStance(GetComponent<HeroEquipment>());
        RuntimeAnimatorController preferred = GetController(next);
        RuntimeAnimatorController gameplay = ResolveGameplayController(preferred);

        if (gameplay == null)
        {
            GlobalSettings.LogGameplayWarning($"HeroWeaponStance: no animator for {next}.");
            return;
        }

        if (next == currentStance && animator.runtimeAnimatorController == gameplay)
            return;

        animator.runtimeAnimatorController = gameplay;
        currentStance = next;

        string note = gameplay != preferred && preferred != null
            ? $" (gameplay fallback: {gameplay.name}, pack stance {preferred.name} has no Speed)"
            : string.Empty;
        GlobalSettings.LogGameplay($"HeroWeaponStance: {next} → {gameplay.name}{note}");
    }

    /// <summary>Only SwordAndShieldStance exposes Speed/Attack/Throw; other pack stances are showcase-only.</summary>
    private RuntimeAnimatorController ResolveGameplayController(RuntimeAnimatorController preferred)
    {
        if (preferred == null)
            return swordAndShield != null ? swordAndShield : unarmed;

        animator.runtimeAnimatorController = preferred;
        if (HeroAnimatorParams.SupportsLocomotion(animator))
            return preferred;

        return swordAndShield != null ? swordAndShield : preferred;
    }

    private static HeroStanceKind ResolveStance(HeroEquipment equipment)
    {
        if (equipment == null)
            return HeroStanceKind.Unarmed;

        EquipmentInstance main = equipment.GetEquipped(EquipmentSlotType.MainHand);
        EquipmentInstance off = equipment.GetEquipped(EquipmentSlotType.OffHand);

        bool hasShield = off?.definition != null && IsShield(off.definition);
        bool hasMain = main?.definition != null;

        if (!hasMain && !hasShield)
            return HeroStanceKind.Unarmed;

        if (hasShield && hasMain)
            return HeroStanceKind.SwordAndShield;

        if (hasShield)
            return HeroStanceKind.SingleSword;

        string id = (main.definition.itemId ?? main.definition.name ?? string.Empty).ToUpperInvariant();
        string display = (main.definition.displayName ?? string.Empty).ToUpperInvariant();
        string prefabName = main.definition.visualPrefab != null
            ? main.definition.visualPrefab.name.ToUpperInvariant()
            : string.Empty;

        if (ContainsAny(id, display, prefabName, "THS"))
            return HeroStanceKind.TwoHanded;
        if (ContainsAny(id, display, prefabName, "SPEAR"))
            return HeroStanceKind.Spear;
        if (ContainsAny(id, display, prefabName, "WAND"))
            return HeroStanceKind.Wand;
        if (ContainsAny(id, display, prefabName, "BOW"))
            return HeroStanceKind.Bow;
        if (ContainsAny(id, display, prefabName, "DUAL", "DOUBLE"))
            return HeroStanceKind.DualWield;

        return HeroStanceKind.SingleSword;
    }

    private static bool IsShield(EquipmentItemDefinition def)
    {
        if (def == null)
            return false;
        string id = def.itemId ?? string.Empty;
        return id.StartsWith("Shield", System.StringComparison.OrdinalIgnoreCase) ||
               (def.visualPrefab != null && def.visualPrefab.name.StartsWith("Shield"));
    }

    private static bool ContainsAny(string id, string display, string prefab, params string[] tokens)
    {
        foreach (string token in tokens)
        {
            if (id.Contains(token) || display.Contains(token) || prefab.Contains(token))
                return true;
        }

        return false;
    }

    private RuntimeAnimatorController GetController(HeroStanceKind kind)
    {
        return kind switch
        {
            HeroStanceKind.Unarmed => unarmed,
            HeroStanceKind.SingleSword => singleSword ?? swordAndShield,
            HeroStanceKind.SwordAndShield => swordAndShield ?? singleSword,
            HeroStanceKind.TwoHanded => twoHanded,
            HeroStanceKind.Spear => spear,
            HeroStanceKind.DualWield => dualWield ?? singleSword,
            HeroStanceKind.Wand => wand,
            HeroStanceKind.Bow => bow,
            _ => unarmed
        };
    }

#if UNITY_EDITOR
    public void EditorLoadControllers()
    {
        unarmed = LoadCtrl("NoWeaponStance.controller");
        singleSword = LoadCtrl("SingleSwordStance.controller");
        swordAndShield = LoadCtrl("SwordAndShieldStance.controller");
        twoHanded = LoadCtrl("THS_Stance.controller");
        spear = LoadCtrl("SpearStance.controller");
        dualWield = LoadCtrl("DoubleSwordStance.controller");
        wand = LoadCtrl("MagicWandStance.controller");
        bow = LoadCtrl("BowAndArrowStance.controller");
        EditorUtility.SetDirty(this);
    }

    private static RuntimeAnimatorController LoadCtrl(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimFolder + fileName);
    }
#endif
}
