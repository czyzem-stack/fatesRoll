using UnityEngine;

/// <summary>Safe animator API — RPG Tiny Hero stance controllers except SwordAndShield lack float/trigger params.</summary>
public static class HeroAnimatorParams
{
    public const string Speed = "Speed";
    public const string Throw = "Throw";
    public const string Attack = "Attack";
    public const string LevelUp = "LevelUp";
    public const string GetHit = "GetHit";
    public const string Die = "Die";
    public const string Victory = "Victory";

    public static bool HasParameter(Animator animator, string name)
    {
        if (animator == null || string.IsNullOrEmpty(name))
            return false;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == name)
                return true;
        }

        return false;
    }

    public static bool SupportsLocomotion(Animator animator) => HasParameter(animator, Speed);

    public static void SetFloatSafe(Animator animator, string name, float value, float dampTime = 0f, float deltaTime = 0f)
    {
        if (!HasParameter(animator, name))
            return;

        if (dampTime > 0f)
            animator.SetFloat(name, value, dampTime, deltaTime);
        else
            animator.SetFloat(name, value);
    }

    public static void SetTriggerSafe(Animator animator, string name)
    {
        if (HasParameter(animator, name))
            animator.SetTrigger(name);
    }

    public static void ResetTriggerSafe(Animator animator, string name)
    {
        if (HasParameter(animator, name))
            animator.ResetTrigger(name);
    }
}
