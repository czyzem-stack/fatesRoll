using UnityEngine;

/// <summary>Cross-fades hero pack die-stay / get-up states (stance-specific names).</summary>
public static class HeroDeathAnimHelper
{
    private static readonly string[] DieStayStateNames =
    {
        "Die01_Stay_SwordAndShield",
        "Die01Stay_SingleSword",
        "Die01Stay_THS",
        "Die01Stay_Spear",
        "Die01Stay_DoubleSword",
        "Die01Stay_NoWeapon",
        "Die01Stay_MagicWand",
        "Die01Stay_BowAndArrow"
    };

    private static readonly string[] GetUpStateNames =
    {
        "GetUp_SwordAndShield",
        "GetUp_SingleSword",
        "GetUp_THS",
        "GetUp_Spear",
        "GetUp_DoubleSword",
        "GetUp_NoWeapon",
        "GetUp_MagicWand",
        "GetUp_BowAndArrow"
    };

    private static readonly string[] IdleStateNames =
    {
        "Idle_Normal_SwordAndShield",
        "Idle_Normal_SingleSword",
        "Idle_Normal_DoubleSword",
        "Idle_Normal_THS",
        "Idle_Normal_Spear",
        "Idle_Normal_NoWeapon",
        "Idle_Normal_MagicWand",
        "Idle_Normal_BowAndArrow"
    };

    public static void ResetToIdle(Animator animator)
    {
        if (animator == null) return;

        animator.Rebind();
        animator.Update(0f);

        HeroAnimatorParams.SetBoolSafe(animator, "IsDead", false);
        HeroAnimatorParams.SetFloatSafe(animator, HeroAnimatorParams.Speed, 0f);
        HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.Die);
        HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.GetHit);
        HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.Attack);

        TryCrossFade(animator, IdleStateNames, 0.05f);
    }

    public static bool PlayDeadStay(Animator animator)
    {
        if (animator == null) return false;

        HeroAnimatorParams.SetBoolSafe(animator, "IsDead", true);
        HeroAnimatorParams.SetFloatSafe(animator, HeroAnimatorParams.Speed, 0f);
        return TryCrossFade(animator, DieStayStateNames, 0.05f);
    }

    public static bool PlayGetUp(Animator animator)
    {
        if (animator == null) return false;

        HeroAnimatorParams.SetBoolSafe(animator, "IsDead", false);
        HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.Die);
        return TryCrossFade(animator, GetUpStateNames, 0.08f);
    }

    private static bool TryCrossFade(Animator animator, string[] stateNames, float duration)
    {
        const int layer = 0;
        string layerName = animator.GetLayerName(layer);

        foreach (string stateName in stateNames)
        {
            foreach (string path in BuildPathCandidates(layerName, stateName))
            {
                int hash = Animator.StringToHash(path);
                if (!animator.HasState(layer, hash))
                    continue;

                animator.CrossFadeInFixedTime(hash, duration, layer);
                return true;
            }
        }

        return false;
    }

    private static System.Collections.Generic.IEnumerable<string> BuildPathCandidates(string layerName, string stateName)
    {
        yield return stateName;
        if (!string.IsNullOrEmpty(layerName))
            yield return layerName + "." + stateName;
        if (layerName != "Base Layer")
            yield return "Base Layer." + stateName;
    }
}
