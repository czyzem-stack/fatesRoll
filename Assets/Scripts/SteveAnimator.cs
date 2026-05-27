using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Single gameplay animator for Steve — SwordAndShieldStance (Speed walk blend, Attack/Throw triggers).
/// Equipment visuals are separate; this controller always drives locomotion and combat clips.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("FatesRoll/Steve Animator")]
public class SteveAnimator : MonoBehaviour
{
    [Header("Stance controllers")]
    [SerializeField] private RuntimeAnimatorController swordAndShieldStance;
    [SerializeField] private RuntimeAnimatorController dualSwordStance;
    [SerializeField] private RuntimeAnimatorController noWeaponStance;
    [SerializeField] private float speedDamp = 0.12f;

    private Animator animator;
    private bool yawApplied;
    private float lastActionTime;

    public Animator RigAnimator => animator;

    public bool IsInProtectedAnimation => Time.time - lastActionTime < (GlobalSettings.Instance != null ? GlobalSettings.Instance.getHitAnimationDuration : 0.5f);

    public void Initialize(Transform agentRoot)
    {
        var equipment = GetComponent<HeroEquipment>();
        equipment?.ResolveRig();
        animator = equipment != null ? equipment.GetRigAnimator() : GetComponentInChildren<Animator>();

        if (animator == null)
            return;

        UpdateStance();

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        yawApplied = false;
        HeroLocomotionUtility.AlignVisualToAgentForward(agentRoot, animator, ref yawApplied);

        SetSpeed(0f);
        ResetActionTriggers();
        lastActionTime = -10f;
    }

    public void UpdateStance()
    {
        if (animator == null) return;

        var equipment = GetComponent<HeroEquipment>();
        RuntimeAnimatorController targetController = ResolveTargetController(equipment);
        
        if (animator.runtimeAnimatorController == targetController && targetController != null) return;

        // Capture current state
        float lastSpeed = animator.gameObject.activeInHierarchy ? animator.GetFloat(HeroAnimatorParams.Speed) : 0f;
        bool inCombat = animator.gameObject.activeInHierarchy ? animator.GetBool("InCombat") : false;

        animator.runtimeAnimatorController = targetController;
        
        // Restore params
        SetSpeed(lastSpeed);
        SetInCombat(inCombat);
        ResetActionTriggers();
        lastActionTime = -10f;

        if (targetController != null)
            GlobalSettings.LogGameplay($"Steve Animator: Switched controller to {targetController.name}");
    }

    private RuntimeAnimatorController ResolveTargetController(HeroEquipment equipment)
    {
        if (equipment == null) return null;

        var main = equipment.GetEquipped(EquipmentSlotType.MainHand);
        var off = equipment.GetEquipped(EquipmentSlotType.OffHand);

        // 1. Check for Sword and Shield combo
        if (main != null && off != null)
        {
            string mName = main.definition.name.ToLower();
            string oName = off.definition.name.ToLower();
            if ((mName.Contains("ohs") || mName.Contains("sword")) && 
                (oName.Contains("shield") || oName.Contains("defend")))
            {
                return swordAndShieldStance;
            }

            // 2. Check for Dual Wield
            if ((mName.Contains("ohs") || mName.Contains("sword")) && 
                (oName.Contains("ohs") || oName.Contains("sword")))
            {
                return dualSwordStance;
            }
        }

        // 3. Use Main Hand's override if specified
        if (main != null && main.definition.animatorOverride != null)
            return main.definition.animatorOverride;

        // 4. Fallback to NoWeapon controller
        return noWeaponStance;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AssignStanceIfEmpty(ref swordAndShieldStance, "Assets/Heroes/Animator/SwordAndShieldStance.controller");
        AssignStanceIfEmpty(ref dualSwordStance, "Assets/Heroes/Animator/DoubleSwordStance.controller");
        AssignStanceIfEmpty(ref noWeaponStance, "Assets/Heroes/Animator/NoWeaponStance.controller");
    }

    static void AssignStanceIfEmpty(ref RuntimeAnimatorController field, string assetPath)
    {
        if (field != null)
            return;
        field = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(assetPath);
    }
#endif

    public void SetTravelSpeed(float speed)
    {
        if (animator == null || IsInProtectedAnimation)
            return;

        HeroAnimatorParams.SetFloatSafe(animator, HeroAnimatorParams.Speed, speed, speedDamp, Time.deltaTime);
    }

    public void SetSpeed(float speed)
    {
        if (animator == null || IsInProtectedAnimation)
            return;

        HeroAnimatorParams.SetFloatSafe(animator, HeroAnimatorParams.Speed, speed);
    }

    public void PlayThrow()
    {
        if (animator == null)
            return;

        lastActionTime = Time.time;
        SetSpeed(0f);
        HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Throw);
    }

    public void PlayAttack()
    {
        if (animator == null)
            return;

        lastActionTime = Time.time;
        SetSpeed(0f);
        HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.Attack);
        HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Attack);
    }

    public void PlayGetHit()
    {
        lastActionTime = Time.time;
        if (animator != null)
        {
            HeroAnimatorParams.SetFloatSafe(animator, "HitType", Random.Range(0, 2));
        }
        HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.GetHit);
    }

    public void PlayDie()
    {
        SetSpeed(0f);
        HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Die);
    }

    public void PlayVictory()
    {
        HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Victory);
    }

    public void PlayLevelUp()
    {
        lastActionTime = Time.time;
        HeroAnimatorParams.SetFloatSafe(animator, HeroAnimatorParams.Speed, 0f);
        HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.LevelUp);
        HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.LevelUp);
    }

    public void SetInCombat(bool inCombat)
    {
        HeroAnimatorParams.SetBoolSafe(animator, "InCombat", inCombat);
    }

    public void ResetActionTriggers()
    {
        if (animator == null)
            return;

        HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.Throw);
        HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.Attack);
        HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.GetHit);
        HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.LevelUp);
        HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.Victory);
    }

#if UNITY_EDITOR
    public void EditorAssignController()
    {
        UpdateStance();
        EditorUtility.SetDirty(this);
    }
#endif
}
