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
        if (animator != null) animator.applyRootMotion = false;
        
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();

        // Ensure consistent agent setup
        agent.speed = 4.0f;
        agent.acceleration = 24.0f; // Snappier
        agent.stoppingDistance = 1.0f; // Prevent jitter
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance; // Don't avoid dice

        SetupPathLines();
        SetLayerRecursive(gameObject, 8);
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
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
            float speed = isMoving ? agent.velocity.magnitude : 0f;
            // Lower threshold to match animator (0.1)
            if (speed < 0.1f) speed = 0f;
            animator.SetFloat("Speed", speed);
        }

        UpdatePathLines();
            
        if (isMoving)
        {
            // 1. Arrival Check (Normal pathing)
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                FinalizeMovement("Destination reached");
            }

            // 2. Proximity Check (POI) - Larger trigger area
            GameObject poi = GetNearestPOI();
            if (poi != null && Vector3.Distance(transform.position, poi.transform.position) < 2.5f)
            {
                FinalizeMovement("Reached POI");
                
                // Despawn POI when reached
                var pm = POIManager.Instance;
                if (pm != null)
                {
                    pm.ResolvePOI(poi);
                }
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
        GameObject poi = GetNearestPOI();
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
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.updateRotation = false;
        }
        Debug.Log($"HeroController: Movement Finalized. Reason: {reason}");
    }

    private GameObject GetNearestPOI()
    {
        GameObject[] pois = GameObject.FindGameObjectsWithTag("POI");
        GameObject nearest = null;
        float minDist = float.MaxValue;
        foreach (var p in pois)
        {
            // If it's a POINode, we check its type or existence
            float dist = Vector3.Distance(transform.position, p.transform.position);
            
            // Ignore POIs that are too close (likely the one we are standing on/just reached)
            if (dist < 1.0f) continue;
            
            if (dist < minDist)
            {
                minDist = dist;
                nearest = p;
            }
        }
        return nearest;
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

        GameObject poi = GetNearestPOI();
        if (poi == null) 
        {
            Debug.LogWarning("HeroController: No valid POI found to move towards.");
            return;
        }

        // 1. Calculate the FULL path to the POI
        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(transform.position, poi.transform.position, NavMesh.AllAreas, path))
        {
            if (path.status != NavMeshPathStatus.PathInvalid)
            {
                // 2. Find the point along this path that corresponds to totalMeters
                Vector3 targetPoint = GetPointOnPath(path, totalMeters);
                
                agent.isStopped = false;
                agent.updateRotation = true;
                if (agent.SetDestination(targetPoint))
                {
                    isMoving = true;
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
