using UnityEngine;
using UnityEngine.AI;

public class HeroController : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private bool isMoving = false;
    public bool IsMoving => isMoving;

    private LineRenderer pathLine;
    private LineRenderer fullPathLine;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        
        // Ensure consistent agent setup
        agent.speed = 4.0f;
        agent.acceleration = 12.0f;
        agent.stoppingDistance = 0.5f;
        agent.autoBraking = true;

        SetupPathLines();
    }

    private void SetupPathLines()
    {
        // Path for current roll
        GameObject goPath = new GameObject("RollPathLine");
        goPath.transform.SetParent(transform);
        pathLine = goPath.AddComponent<LineRenderer>();
        ConfigureLine(pathLine, Color.yellow, 0.2f);

        // Full path to POI
        GameObject goFull = new GameObject("FullPathLine");
        goFull.transform.SetParent(transform);
        fullPathLine = goFull.AddComponent<LineRenderer>();
        ConfigureLine(fullPathLine, Color.magenta, 0.1f);
    }

    private void ConfigureLine(LineRenderer lr, Color color, float width)
    {
        lr.startWidth = width;
        lr.endWidth = width;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.positionCount = 0;
        lr.enabled = false;
    }

    void Update()
    {
        if (agent == null) return;

        // Visuals
        if (animator != null)
        {
            animator.SetFloat("Speed", agent.velocity.magnitude);
        }

        UpdatePathLines();
            
        if (isMoving)
        {
            // 1. Arrival Check
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                FinalizeMovement("Destination reached");
            }

            // 2. Proximity Check (POI)
            GameObject poi = GameObject.FindWithTag("POI");
            if (poi != null && Vector3.Distance(transform.position, poi.transform.position) < 1.5f)
            {
                FinalizeMovement("Reached POI");
            }
        }
    }

    private void UpdatePathLines()
    {
        bool show = GlobalSettings.Instance.showPath;

        // 1. Roll Path (Yellow)
        if (show && agent.hasPath)
        {
            pathLine.enabled = true;
            pathLine.positionCount = agent.path.corners.Length;
            for (int i = 0; i < agent.path.corners.Length; i++)
            {
                pathLine.SetPosition(i, agent.path.corners[i] + Vector3.up * 0.15f);
            }
        }
        else
        {
            pathLine.enabled = false;
        }

        // 2. Full Path to POI (Magenta)
        GameObject poi = GameObject.FindWithTag("POI");
        if (show && poi != null)
        {
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(transform.position, poi.transform.position, NavMesh.AllAreas, path))
            {
                fullPathLine.enabled = true;
                fullPathLine.positionCount = path.corners.Length;
                for (int i = 0; i < path.corners.Length; i++)
                {
                    fullPathLine.SetPosition(i, path.corners[i] + Vector3.up * 0.1f);
                }
            }
            else
            {
                fullPathLine.enabled = false;
            }
        }
        else
        {
            fullPathLine.enabled = false;
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
        float totalMeters = diceResult * settings.stepsPerDiceValue * settings.metersPerStep;
        
        Debug.Log($"HeroController: Processing Move. Roll: {diceResult}, Target Distance: {totalMeters}m");

        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else return;
        }

        GameObject poi = GameObject.FindWithTag("POI");
        if (poi == null) return;

        // 1. Calculate the FULL path to the POI
        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(transform.position, poi.transform.position, NavMesh.AllAreas, path))
        {
            if (path.status != NavMeshPathStatus.PathInvalid)
            {
                // 2. Find the point along this path that corresponds to totalMeters
                Vector3 targetPoint = GetPointOnPath(path, totalMeters);
                
                agent.isStopped = false;
                if (agent.SetDestination(targetPoint))
                {
                    isMoving = true;
                    // Note: Agent will handle rotation towards velocity
                }
            }
        }
    }

    private Vector3 GetPointOnPath(NavMeshPath path, float distance)
    {
        if (path.corners.Length < 2) return transform.position;

        float accumulatedDistance = 0;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            float segmentDistance = Vector3.Distance(path.corners[i], path.corners[i+1]);
            if (accumulatedDistance + segmentDistance >= distance)
            {
                float remainingDistance = distance - accumulatedDistance;
                float fraction = remainingDistance / segmentDistance;
                return Vector3.Lerp(path.corners[i], path.corners[i+1], fraction);
            }
            accumulatedDistance += segmentDistance;
        }

        // If distance is longer than path, return last corner (POI)
        return path.corners[path.corners.Length - 1];
    }

    public void SetTarget(GameObject target)
    {
        Debug.Log($"HeroController: Objective target is {target.name}");
    }
}
