using UnityEngine;

/// <summary>Aligns modular hero visual rigs with NavMeshAgent facing (RPG Tiny Hero pack).</summary>
public static class HeroLocomotionUtility
{
    private const float AlignThresholdDegrees = 2f;

    /// <summary>
    /// Rotates the visual rig (MC02) so its forward matches the agent root forward.
    /// Fixes sideways locomotion when the pack mesh faces ±X instead of +Z.
    /// </summary>
    public static void AlignVisualToAgentForward(
        Transform agentRoot,
        Animator animator,
        ref bool appliedFlag,
        float extraYawDegrees = 0f)
    {
        if (appliedFlag || agentRoot == null || animator == null)
            return;

        Transform visualRoot = GetVisualRigRoot(agentRoot, animator);
        Vector3 agentForward = Flatten(agentRoot.forward);
        Vector3 visualForward = Flatten(visualRoot.forward);

        if (agentForward.sqrMagnitude < 0.01f || visualForward.sqrMagnitude < 0.01f)
            return;

        float yaw = Vector3.SignedAngle(visualForward, agentForward, Vector3.up) + extraYawDegrees;
        if (Mathf.Abs(yaw) < AlignThresholdDegrees)
        {
            appliedFlag = true;
            return;
        }

        Vector3 euler = visualRoot.localEulerAngles;
        euler.y += yaw;
        visualRoot.localRotation = Quaternion.Euler(euler);
        appliedFlag = true;

        GlobalSettings.LogGameplay(
            $"Hero locomotion: aligned {visualRoot.name} yaw by {yaw:0.#}° (mesh forward → agent forward).");
    }

    /// <summary>Legacy name — calls <see cref="AlignVisualToAgentForward"/>.</summary>
    public static void ApplyVisualYawFix(Transform agentRoot, Animator animator, ref bool appliedFlag)
    {
        AlignVisualToAgentForward(agentRoot, animator, ref appliedFlag);
    }

    public static Transform GetVisualRigRoot(Transform agentRoot, Animator animator)
    {
        Transform visualRoot = animator.transform;
        while (visualRoot.parent != null && visualRoot.parent != agentRoot)
            visualRoot = visualRoot.parent;
        return visualRoot;
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
    }
}
