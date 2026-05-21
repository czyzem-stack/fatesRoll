using UnityEngine;
using UnityEngine.AI;

public class HeroController : MonoBehaviour
{
    public float stepDistance = 2.5f; // Approx 1 yard in game scale
    private NavMeshAgent agent;
    private Animator animator;
    private bool isMoving = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        
        agent.speed = 4.0f;
        agent.acceleration = 16.0f;
        agent.stoppingDistance = 0.1f;
        agent.autoBraking = true;
    }

    void Update()
    {
        if (animator != null && agent != null)
        {
            animator.SetFloat("Speed", agent.velocity.magnitude);
            
            if (isMoving)
            {
                // Check if we arrived at the roll destination
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
                {
                    isMoving = false;
                    agent.isStopped = true;
                    Debug.Log("HeroController: Move complete.");
                }

                // Check if we passed an Orc (POI) during this move
                GameObject poi = GameObject.FindWithTag("POI");
                if (poi != null && Vector3.Distance(transform.position, poi.transform.position) < 1.5f)
                {
                    Debug.Log("HeroController: Reached Orc! Spawning next target.");
                    var pm = Object.FindAnyObjectByType<POIManager>();
                    if (pm != null) pm.SpawnNewPOI();
                }
            }
        }
    }

    public void MoveSteps(int steps)
    {
        stepDistance = 3.0f; // Force 1 yard = 3m calibration
        float totalDistance = steps * stepDistance;
        Debug.Log($"HeroController: Moving {steps} steps ({totalDistance:F1}m).");

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        
        // Configuration reset
        agent.speed = 4.5f;
        agent.acceleration = 20.0f;
        agent.stoppingDistance = 0.05f;

        // Ensure we are on the NavMesh
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 10.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogError("HeroController: Cannot move - not on NavMesh!");
                return;
            }
        }

        GameObject poi = GameObject.FindWithTag("POI");
        Vector3 moveDirection;
        
        if (poi != null)
        {
            moveDirection = (poi.transform.position - transform.position);
            moveDirection.y = 0;
            if (moveDirection.sqrMagnitude > 0.001f)
            {
                moveDirection.Normalize();
            }
            else
            {
                moveDirection = transform.forward;
            }
        }
        else
        {
            moveDirection = transform.forward;
            moveDirection.y = 0;
            moveDirection.Normalize();
        }

        // Calculate destination
        Vector3 targetPoint = transform.position + moveDirection * totalDistance;

        NavMeshHit navHit;
        // Search in a large radius to ensure we find a valid point near the calculated destination
        if (NavMesh.SamplePosition(targetPoint, out navHit, 10.0f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            if (agent.SetDestination(navHit.position))
            {
                isMoving = true;
                // Face the direction of travel
                if (moveDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(moveDirection);
                }
                Debug.Log($"HeroController: SetDestination to {navHit.position}. Distance to reach: {Vector3.Distance(transform.position, navHit.position):F1}m");
            }
        }
        else
        {
            Debug.LogError($"HeroController: FAILED to find valid ground for destination {targetPoint}");
        }
    }

    public void SetTarget(GameObject target)
    {
        // Just log for context
        Debug.Log($"HeroController: New target spawned at {target.transform.position}");
    }
}
