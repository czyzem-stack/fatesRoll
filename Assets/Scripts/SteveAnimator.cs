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
    private const string GameplayControllerPath = "Assets/Heroes/Animator/SwordAndShieldStance.controller";

    [SerializeField] private RuntimeAnimatorController gameplayController;
    [SerializeField] private float speedDamp = 0.12f;

    private Animator animator;
    private bool yawApplied;

    public Animator RigAnimator => animator;

    public void Initialize(Transform agentRoot)
    {
        var equipment = GetComponent<HeroEquipment>();
        equipment?.ResolveRig();
        animator = equipment != null ? equipment.GetRigAnimator() : GetComponentInChildren<Animator>();

        if (animator == null)
            return;

#if UNITY_EDITOR
        if (gameplayController == null)
            gameplayController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GameplayControllerPath);
#endif

        if (gameplayController != null)
            animator.runtimeAnimatorController = gameplayController;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        yawApplied = false;
        HeroLocomotionUtility.AlignVisualToAgentForward(agentRoot, animator, ref yawApplied);

        SetSpeed(0f);
        ResetActionTriggers();
    }

    public void SetTravelSpeed(float speed)
    {
        if (animator == null)
            return;

        HeroAnimatorParams.SetFloatSafe(animator, HeroAnimatorParams.Speed, speed, speedDamp, Time.deltaTime);
    }

    public void SetSpeed(float speed)
    {
        if (animator == null)
            return;

        HeroAnimatorParams.SetFloatSafe(animator, HeroAnimatorParams.Speed, speed);
    }

    public void PlayThrow()
    {
        if (animator == null)
            return;

        SetSpeed(0f);
        HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Throw);
    }

    public void PlayAttack()
    {
        if (animator == null)
            return;

        SetSpeed(0f);
        HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.Attack);
        HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Attack);
    }

    public void PlayGetHit()
    {
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
        SetSpeed(0f);
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
        gameplayController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GameplayControllerPath);
        EditorUtility.SetDirty(this);
    }
#endif
}
