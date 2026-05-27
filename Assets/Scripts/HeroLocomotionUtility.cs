using UnityEngine;
using UnityEngine.AI;

/// <summary>Aligns modular hero visual rigs with NavMeshAgent facing (RPG Tiny Hero pack).</summary>
public static class HeroLocomotionUtility
{
    private const float AlignThresholdDegrees = 2f;
    private const float MinFootLift = 0.005f;
    private const float MaxFootLift = 0.6f;

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

    /// <summary>
    /// Ensures the agent has a sensible height for a humanoid. Does not reset baseOffset
    /// (feet alignment may set that when the animator lives on the agent root).
    /// Unity 6+ no longer exposes NavMeshAgent.center — capsule offset is on the agent type.
    /// </summary>
    public static void EnsureNavMeshFootPivot(NavMeshAgent agent)
    {
        if (agent == null) return;

        if (agent.height < 1.6f)
            agent.height = 1.6f;
    }

    /// <summary>
    /// Positions the visual rig so the lowest mesh point sits on visible ground.
    /// Uses renderer bounds (not foot bones — RPG Tiny Hero foot bones sit far below the mesh).
    /// </summary>
    public static void AlignVisualFeetToGround(
        Transform agentRoot,
        Animator animator,
        NavMeshAgent agent,
        Vector3 visualLocalAuthoring,
        float baseOffsetAuthoring,
        ref Renderer[] cachedRenderers)
    {
        if (agentRoot == null || animator == null)
            return;

        animator.Update(0f);

        if (!TryGetMeshFootWorldData(agentRoot, animator, out Vector3 footCenter, out float footY, ref cachedRenderers))
            return;

        float groundY = ResolveGroundY(footCenter, agentRoot.position);
        float lift = Mathf.Clamp(groundY - footY, -MaxFootLift, MaxFootLift);
        if (Mathf.Abs(lift) < MinFootLift)
            return;

        // Keep MC02 at local zero so the NavMesh capsule stays centered on the agent root.
        if (agent != null)
        {
            agent.baseOffset = baseOffsetAuthoring + lift;
            if (agent.isOnNavMesh)
                agent.Warp(agentRoot.position);
        }
        else
        {
            Transform visualRoot = GetVisualRigRoot(agentRoot, animator);
            if (visualRoot != agentRoot)
            {
                Vector3 localLift = visualRoot.parent != null
                    ? visualRoot.parent.InverseTransformVector(Vector3.up * lift)
                    : Vector3.up * lift;
                visualRoot.localPosition = visualLocalAuthoring + localLift;
            }
            else
            {
                agentRoot.position += Vector3.up * lift;
            }
        }

        GlobalSettings.LogGameplay(
            $"Hero feet aligned: lift={lift:0.###} ground={groundY:0.###} foot={footY:0.###} (baseOffset)");
    }

    /// <summary>Legacy name — calls <see cref="AlignVisualFeetToGround"/>.</summary>
    public static void AlignVisualFeetToAgentRoot(Transform agentRoot, Animator animator)
    {
        if (agentRoot == null || animator == null)
            return;

        Transform visualRoot = GetVisualRigRoot(agentRoot, animator);
        NavMeshAgent agent = agentRoot.GetComponent<NavMeshAgent>();
        float baseOffset = agent != null ? agent.baseOffset : 0f;
        Renderer[] cached = null;
        AlignVisualFeetToGround(agentRoot, animator, agent, visualRoot.localPosition, baseOffset, ref cached);
    }

    public static bool TrySampleVisualGroundY(Vector3 worldPosition, out float groundY, float rayStartHeight = 40f, float maxDistance = 120f)
    {
        Vector3 origin = worldPosition + Vector3.up * rayStartHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
            return true;
        }

        groundY = worldPosition.y;
        return false;
    }

    private static float ResolveGroundY(Vector3 footCenter, Vector3 agentPosition)
    {
        if (TrySampleVisualGroundY(footCenter, out float footGroundY))
            return footGroundY;

        if (TrySampleVisualGroundY(agentPosition, out float agentGroundY))
            return agentGroundY;

        return agentPosition.y;
    }

    private static bool TryGetMeshFootWorldData(
        Transform agentRoot,
        Animator animator,
        out Vector3 footCenter,
        out float footY,
        ref Renderer[] cachedRenderers)
    {
        footCenter = Vector3.zero;
        footY = 0f;

        Transform visualRoot = GetVisualRigRoot(agentRoot, animator);
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            cachedRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);

        var renderers = cachedRenderers;
        if (renderers == null || renderers.Length == 0)
            return false;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i] is SpriteRenderer) continue;
            bounds.Encapsulate(renderers[i].bounds);
        }

        footCenter = bounds.center;
        footCenter.y = bounds.min.y;
        footY = bounds.min.y;
        return true;
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
    }
}
