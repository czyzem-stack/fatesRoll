using System.Collections.Generic;
using UnityEngine;

/// <summary>Drives pack animator controllers that lack Speed/Attack parameters (direct state cross-fade).</summary>
[DisallowMultipleComponent]
public class MonsterLocomotionDriver : MonoBehaviour
{
    private const int AnimLayer = 0;

    private Animator animator;
    private MonsterAnimProfile profile;
    private int idleHash;
    private int walkHash;
    private int combatIdleHash;
    private int attackHash;
    private int getHitHash;
    private int dieHash;
    private int tauntHash;
    private int defenseHash;
    private int currentLocomotionHash;
    private POIType poiType;
    private float lastActionTime;

    public bool UsesParameterMode => profile.useAnimatorParameters;
    public bool UsesStatePlay => !profile.useAnimatorParameters;
    public bool HasTauntState => tauntHash != 0;

    public bool IsInProtectedAnimation => Time.time - lastActionTime < (GlobalSettings.Instance != null ? GlobalSettings.Instance.getHitAnimationDuration : 0.5f);

    public void Bind(POIType type, Animator targetAnimator)
    {
        poiType = type;
        profile = MonsterAnimProfile.Get(type);
        animator = targetAnimator;
        lastActionTime = -10f;

        if (animator == null || UsesParameterMode)
            return;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        idleHash = ResolveStateHash(profile.idleState);
        walkHash = ResolveStateHash(profile.walkState);
        combatIdleHash = ResolveStateHash(profile.combatIdleState);
        attackHash = ResolveStateHash(profile.attackState);
        getHitHash = ResolveStateHash(profile.getHitState);
        dieHash = ResolveStateHash(profile.dieState);
        tauntHash = ResolveStateHash(profile.tauntState, "Taunt", "Taunting");
        defenseHash = ResolveStateHash("Defense", "Defence", "Block");

        if (idleHash != 0)
            CrossFadeLocomotion(idleHash, 0.05f);
    }

    public void UpdateLocomotion(float agentSpeed, bool inCombat, bool isAttacking)
    {
        if (animator == null || UsesParameterMode || isAttacking || IsInProtectedAnimation)
            return;

        int target;
        if (agentSpeed > 0.35f)
            target = walkHash;
        else if (inCombat)
            target = combatIdleHash;
        else
            target = idleHash;

        CrossFadeLocomotion(target, 0.18f);
    }

    public void PlayAttack()
    {
        if (animator == null)
            return;

        lastActionTime = Time.time;

        if (UsesParameterMode)
        {
            // For Skeletons and others with multiple attacks, set a random index
            if (HeroAnimatorParams.HasParameter(animator, "AttackIndex"))
            {
                animator.SetInteger("AttackIndex", Random.Range(0, 2));
            }

            HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.Attack);
            HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Attack);
            return;
        }

        CrossFadeState(attackHash, 0.08f);
    }

    public void PlayDefense()
    {
        if (animator == null)
            return;

        lastActionTime = Time.time;

        if (UsesParameterMode)
        {
            HeroAnimatorParams.SetTriggerSafe(animator, "Defense");
            return;
        }

        if (defenseHash != 0)
            CrossFadeState(defenseHash, 0.1f);
    }

    public void PlayGetHit()
    {
        if (animator == null)
            return;

        lastActionTime = Time.time;

        if (UsesParameterMode)
        {
            HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.GetHit);
            return;
        }

        CrossFadeState(getHitHash, 0.06f);
    }

    public void PlayDie()
    {
        if (animator == null)
            return;

        if (UsesParameterMode)
        {
            HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Die);
            HeroAnimatorParams.SetBoolSafe(animator, "IsDead", true);
            return;
        }

        CrossFadeState(dieHash, 0.05f);
    }

    public void PlayTaunt()
    {
        if (animator == null || tauntHash == 0)
            return;

        if (UsesParameterMode)
        {
            HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Taunt);
            return;
        }

        CrossFadeState(tauntHash, 0.1f);
    }

    private int ResolveStateHash(string primary, params string[] alternates)
    {
        if (animator == null)
            return 0;

        var names = new List<string>();
        if (!string.IsNullOrEmpty(primary))
            names.Add(primary);
        if (alternates != null)
        {
            foreach (var alt in alternates)
            {
                if (!string.IsNullOrEmpty(alt) && !names.Contains(alt))
                    names.Add(alt);
            }
        }

        string layerName = animator.GetLayerName(AnimLayer);
        foreach (string stateName in names)
        {
            foreach (string path in BuildPathCandidates(layerName, stateName))
            {
                int hash = Animator.StringToHash(path);
                if (animator.HasState(AnimLayer, hash))
                    return hash;
            }
        }

        return 0;
    }

    private static IEnumerable<string> BuildPathCandidates(string layerName, string stateName)
    {
        yield return stateName;
        if (!string.IsNullOrEmpty(layerName))
            yield return layerName + "." + stateName;
        if (layerName != "Base Layer")
            yield return "Base Layer." + stateName;
    }

    private void CrossFadeLocomotion(int stateHash, float duration)
    {
        if (stateHash == 0 || stateHash == currentLocomotionHash)
            return;

        if (!animator.HasState(AnimLayer, stateHash))
            return;

        animator.CrossFadeInFixedTime(stateHash, duration, AnimLayer);
        currentLocomotionHash = stateHash;
    }

    private void CrossFadeState(int stateHash, float duration)
    {
        if (stateHash == 0 || animator == null)
            return;

        if (!animator.HasState(AnimLayer, stateHash))
            return;

        animator.CrossFadeInFixedTime(stateHash, duration, AnimLayer);
        currentLocomotionHash = stateHash;
    }
}
