using UnityEngine;
using UnityEngine.AI;

public class HeroController : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private bool isMoving = false;
    public bool IsMoving => isMoving;

    private PlayerStats stats;
    private bool inCombat = false;
    public bool InCombat => inCombat;
    public GameObject currentEnemy;

    public UnityEngine.UI.Slider healthSlider;

    private LineRenderer pathLine;
    private LineRenderer fullPathLine;

    private GameObject currentTarget;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();

        // Ensure agent is on NavMesh immediately
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 5.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log($"HeroController: Warped {gameObject.name} to NavMesh at {hit.position}");
            }
        }

        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;

        stats = GetComponent<PlayerStats>();
        if (stats == null) stats = gameObject.AddComponent<PlayerStats>();
        
        AutoAssignHealthUI();

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
        // Never put UI or Path Lines on the highlight layer
        if (obj.GetComponent<UnityEngine.Canvas>() != null || 
            obj.GetComponent<UnityEngine.RectTransform>() != null || 
            obj.GetComponent<UnityEngine.LineRenderer>() != null ||
            obj.name.Contains("PathLine"))
        {
            // PathLines and UI must stay off Layer 8
            int targetLayer = (obj.GetComponent<UnityEngine.LineRenderer>() != null || obj.name.Contains("PathLine")) ? 0 : 5;
            SetLayerRecursiveInternal(obj, targetLayer);
            return;
        }

        // Only put character Renderers on the highlight layer
        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            obj.layer = layer;
        }
        else
        {
            obj.layer = 0;
        }

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    private void SetLayerRecursiveInternal(GameObject obj, int layer)
{
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursiveInternal(child.gameObject, layer);
        }
    }

    private void AutoAssignHealthUI()
    {
        if (healthSlider == null)
        {
            GameObject sliderGO = GameObject.Find("MainUI_Canvas/HUD_Profile/Slider_Bottom");
            if (sliderGO != null) healthSlider = sliderGO.GetComponent<UnityEngine.UI.Slider>();
        }
        UpdateHealthUI();
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null && stats != null)
        {
            healthSlider.maxValue = stats.MaxHP;
            healthSlider.value = stats.currentHP;
        }
    }

    private void SetupPathLines()
    {
        // Path for current roll
        GameObject goPath = new GameObject("RollPathLine");
        goPath.transform.SetParent(transform);
        goPath.layer = 0; // Force to Default layer
        pathLine = goPath.AddComponent<LineRenderer>();
        ConfigureLine(pathLine, Color.yellow, 0.2f);

        // Full path to POI
        GameObject goFull = new GameObject("FullPathLine");
        goFull.transform.SetParent(transform);
        goFull.layer = 0; // Force to Default layer
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
            // Ensure Root Motion doesn't hijack NavMeshAgent movement
            if (animator.applyRootMotion) animator.applyRootMotion = false;

            // Use direct agent velocity for the animator speed, with a floor if we are meant to be moving
            float currentSpeed = agent.velocity.magnitude;
            float animatorSpeed = isMoving ? Mathf.Max(currentSpeed, 2.0f) : currentSpeed;
            
            // Smoother threshold check
            if (animatorSpeed < 0.05f) animatorSpeed = 0f;
            animator.SetFloat("Speed", animatorSpeed);
            
            if (isMoving && animatorSpeed < 0.1f)
            {
                Debug.LogWarning($"HeroController: Moving but Speed is low! {animatorSpeed}");
            }
        }

        UpdatePathLines();
            
        if (inCombat && currentEnemy != null)
        {
            // Keep facing the enemy during combat
            Vector3 direction = (currentEnemy.transform.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5.0f);
            }
        }

        if (isMoving)
{
            // 1. Arrival Check (Normal pathing)
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                FinalizeMovement("Destination reached");
            }

            // 2. Proximity Check (POI) - Larger trigger area
            if (currentTarget != null)
            {
                float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
                // Increased distance and check if we have a path to ensure we're actively moving toward it
                if (dist < 3.0f)
                {
                    Debug.Log($"HeroController: Close enough to {currentTarget.name} (Dist: {dist:F2}m). Entering combat/resolution.");
                    GameObject poi = currentTarget;
                    
                    // Stop immediately
                    FinalizeMovement("Proximity trigger");
                    
                    // Check if it's an enemy
                    var enemy = poi.GetComponent<EnemyCombatant>();
                    if (enemy != null)
                    {
                        EnterCombat(poi);
                        
                        // Initial damage from leftover roll
                        if (leftoverDamage > 0)
                        {
                            StartCoroutine(InitialAttackCoroutine(enemy, leftoverDamage));
                        }
                    }
else
                    {
                        var pm = POIManager.Instance;
                        if (pm != null) pm.ResolvePOI(poi);
                    }
                    currentTarget = null;
                }
            }
        }
    }

    private System.Collections.IEnumerator InitialAttackCoroutine(EnemyCombatant enemy, int diceRoll)
    {
        // Steve faces enemy immediately
        Vector3 direction = (enemy.transform.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);

        // Calculate damage based on stats and the roll
        float baseDamage = stats != null ? stats.AttackDamage : 20f;
        float rollMultiplier = diceRoll / 7.0f;
        float heroDamage = baseDamage * rollMultiplier;

        // Crit check for arrival attack
        bool isCrit = false;
        if (stats != null && Random.Range(0f, 100f) < stats.CritChance)
        {
            isCrit = true;
            heroDamage *= (1f + (stats.CritDamage / 100f));
        }

        int finalDamage = Mathf.RoundToInt(heroDamage);

        // Reset triggers before starting arrival attack
        if (animator != null) animator.ResetTrigger("Attack");
        if (animator != null) animator.SetTrigger("Attack");

        // Perfect strike timing (aligned with sword impact)
        yield return new WaitForSeconds(0.25f);
        if (enemy != null)
        {
            enemy.TakeDamage(finalDamage);
            string critMsg = isCrit ? " (CRITICAL!)" : "";
            Debug.Log($"HeroController: Initial Arrival Attack dealt {finalDamage} damage{critMsg}.");

            if (enemy.IsDead)
            {
                VictoryFlourish();
                yield break;
            }

            // Snappier reaction
            yield return new WaitForSeconds(GlobalSettings.Instance.combatReactionDelay);

            if (enemy != null && enemy.gameObject != null && !enemy.IsDead)
            {
                enemy.FaceTarget(this.transform);
                enemy.PerformAttack(this);
            }
        }
    }

    private int leftoverDamage = 0;

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
        if (show && currentTarget != null)
        {
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(transform.position, currentTarget.transform.position, NavMesh.AllAreas, path))
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

        if (currentTarget == null)
        {
            currentTarget = GetNearestPOI();
        }

        if (currentTarget == null) 
        {
            Debug.LogWarning("HeroController: No valid POI found to move towards.");
            return;
        }

        // 1. Calculate the FULL path to the POI
        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(transform.position, currentTarget.transform.position, NavMesh.AllAreas, path))
        {
            if (path.status != NavMeshPathStatus.PathInvalid)
            {
                float pathDist = 0;
                for (int i = 0; i < path.corners.Length - 1; i++) pathDist += Vector3.Distance(path.corners[i], path.corners[i+1]);

                // Calculate leftover damage based on remaining dice value
                if (pathDist < totalMeters)
                {
                    float usedDiceValue = pathDist / (settings.stepsPerDiceValue * settings.metersPerStep);
                    float remainingDiceValue = diceResult - usedDiceValue;
                    leftoverDamage = Mathf.RoundToInt(remainingDiceValue * GlobalSettings.Instance.combatDamageMultiplier);
                }
                else
                {
                    leftoverDamage = 0;
                }

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

    public void EnterCombat(GameObject enemy)
    {
        inCombat = true;
        currentEnemy = enemy;
        isMoving = false;
        if (agent != null) 
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        // Show Enemy Health Bar
        var enemyCombatant = enemy.GetComponent<EnemyCombatant>();
        if (enemyCombatant != null) enemyCombatant.SetHealthBarVisible(true);

        // Safety: Reset pending triggers to avoid 'random' animation hiccups
if (animator != null)
        {
            animator.ResetTrigger("Throw");
            animator.ResetTrigger("LevelUp");
            animator.ResetTrigger("Victory");
            animator.ResetTrigger("Attack");
            animator.ResetTrigger("GetHit");
        }
        
        Debug.Log($"HeroController: Entered COMBAT with {enemy.name}");

        // Face the enemy
        Vector3 direction = (enemy.transform.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    public void ExitCombat()
    {
        inCombat = false;
        currentEnemy = null;
        Debug.Log("HeroController: Combat resolved.");
    }

    public bool TakeDamage(int amount)
    {
        if (stats == null || stats.currentHP <= 0) return false;

        bool tookDamage = stats.TakeDamage(amount);
        UpdateHealthUI();
        
        if (tookDamage)
        {
            if (Application.isPlaying)
            {
                GameObject go = new GameObject("FloatingText_Damage");
                go.transform.position = transform.position + Vector3.up * 2.2f;
                var ft = go.AddComponent<FloatingText>();
                ft.Setup($"-{amount} HP", Color.red);
            }

            if (stats.currentHP <= 0)
            {
                if (animator != null) animator.SetTrigger("Die");
                Debug.LogError("Hero Died!");
            }
            else
            {
                if (animator != null) animator.SetTrigger("GetHit");
            }
        }

        return tookDamage;
    }

    public void VictoryFlourish()
    {
        if (animator != null) animator.SetTrigger("Victory");
    }
}
