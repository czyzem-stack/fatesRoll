using UnityEngine;
using UnityEngine.AI;

public class HeroController : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private bool isMoving = false;
    public bool IsMoving => isMoving;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        
        // Ensure consistent agent setup
        agent.speed = 4.0f;
        agent.acceleration = 12.0f;
        agent.stoppingDistance = 0.15f;
        agent.autoBraking = true;
    }

    void Update()
    {
        if (agent == null) return;

        // Visuals
        if (animator != null)
        {
            animator.SetFloat("Speed", agent.velocity.magnitude);
        }
            
        if (isMoving)
        {
            // 1. Arrival Check
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                FinalizeMovement("Destination reached");
            }

            // 2. Proximity Check (POI)
            GameObject poi = GameObject.FindWithTag("POI");
            if (poi != null && Vector3.Distance(transform.position, poi.transform.position) < 1.4f)
            {
                FinalizeMovement("Reached POI");
                var pm = Object.FindAnyObjectByType<POIManager>();
                if (pm != null) pm.SpawnNewPOI();
            }
        }
    }

    private void FinalizeMovement(string reason)
    {
        isMoving = false;
        agent.isStopped = true;
        agent.ResetPath();
        Debug.Log($"HeroController: Movement Finalized. Reason: {reason}");
    }

    public void MoveSteps(int diceResult)
    {
        GlobalSettings settings = GlobalSettings.Instance;
        
        // CALCULATION: Total meters to travel
        float totalMeters = diceResult * settings.stepsPerDiceValue * settings.metersPerStep;
        
        Debug.Log($"HeroController: Processing Move. Roll: {diceResult}, Unit Scale: {settings.stepsPerDiceValue}x{settings.metersPerStep}m. Target Distance: {totalMeters}m");

        if (agent == null) agent = GetComponent<NavMeshAgent>();

        // NavMesh Sanity Check
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogError("HeroController: Hero is off NavMesh and cannot warp back!");
                return;
            }
        }

        // Direction Logic
        Vector3 direction = transform.forward;
        GameObject poi = GameObject.FindWithTag("POI");
        float distToObjective = float.MaxValue;

        if (poi != null)
        {
            Vector3 offset = (poi.transform.position - transform.position);
            offset.y = 0;
            distToObjective = offset.magnitude;
            if (distToObjective > 0.1f)
            {
                direction = offset.normalized;
            }
        }

        // Clamp travel distance so we don't overshoot the POI
        float clampedDistance = Mathf.Min(totalMeters, distToObjective);
        Vector3 targetPos = transform.position + (direction * clampedDistance);

        // Find best NavMesh landing spot
        NavMeshHit finalHit;
        if (NavMesh.SamplePosition(targetPos, out finalHit, 2.0f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            if (agent.SetDestination(finalHit.position))
            {
                isMoving = true;
                transform.rotation = Quaternion.LookRotation(direction);
                Debug.Log($"HeroController: Moving to {finalHit.position}. Distance: {Vector3.Distance(transform.position, finalHit.position):F2}m");
            }
        }
        else
        {
            Debug.LogError($"HeroController: NavMesh gap at destination {targetPos}. Movement aborted.");
        }
    }

    public void SetTarget(GameObject target)
    {
        Debug.Log($"HeroController: Objective target is {target.name}");
    }
}
