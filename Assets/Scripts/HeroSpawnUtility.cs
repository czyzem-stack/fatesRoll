using UnityEngine;
using UnityEngine.AI;

/// <summary>Places Steve on the NavMesh / ground (spawn point, death respawn).</summary>
public static class HeroSpawnUtility
{
    public static bool TryResolveSpawnPosition(Vector3 desired, out Vector3 resolved, float navSampleRadius = 12f)
    {
        resolved = desired;

        if (NavMesh.SamplePosition(desired, out NavMeshHit navHit, navSampleRadius, NavMesh.AllAreas))
        {
            resolved = navHit.position;
            return true;
        }

        Vector3 rayOrigin = desired + Vector3.up * 40f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, 120f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            resolved = groundHit.point;
            if (NavMesh.SamplePosition(resolved, out navHit, navSampleRadius, NavMesh.AllAreas))
                resolved = navHit.position;
            return true;
        }

        return false;
    }

    /// <summary>Warp agent root onto NavMesh, then snap MC02 feet to the surface.</summary>
    public static void PlaceHero(HeroController hero, Vector3 desiredPosition, Quaternion rotation)
    {
        if (hero == null) return;

        NavMeshAgent navAgent = hero.GetComponent<NavMeshAgent>();
        HeroLocomotionUtility.EnsureNavMeshFootPivot(navAgent);
        TryResolveSpawnPosition(desiredPosition, out Vector3 resolved);

        if (navAgent != null)
        {
            navAgent.enabled = false;
            hero.transform.SetPositionAndRotation(resolved, rotation);
            navAgent.enabled = true;

            if (NavMesh.SamplePosition(resolved, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                navAgent.Warp(hit.position);
            else
                navAgent.Warp(resolved);

            navAgent.isStopped = true;
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
            navAgent.updateRotation = true;
        }
        else
        {
            hero.transform.SetPositionAndRotation(resolved, rotation);
        }

        hero.SnapBodyToGround();
    }
}
