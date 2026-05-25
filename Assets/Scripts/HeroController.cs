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
    private float leftoverDiceValue = 0;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();

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

        agent.speed = 4.0f;
        agent.acceleration = 24.0f;
        agent.stoppingDistance = 1.0f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        SetupPathLines();
        SetLayerRecursive(gameObject, 8);
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        if (obj.GetComponent<UnityEngine.Canvas>() != null || 
            obj.GetComponent<UnityEngine.RectTransform>() != null || 
            obj.GetComponent<UnityEngine.LineRenderer>() != null ||
            obj.name.Contains("PathLine"))
        {
            int targetLayer = (obj.GetComponent<UnityEngine.LineRenderer>() != null || obj.name.Contains("PathLine")) ? 0 : 5;
            SetLayerRecursiveInternal(obj, targetLayer);
            return;
        }

        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null) obj.layer = layer;
        else obj.layer = 0;

        foreach (Transform child in obj.transform) SetLayerRecursive(child.gameObject, layer);
    }

    private void SetLayerRecursiveInternal(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform) SetLayerRecursiveInternal(child.gameObject, layer);
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
        GameObject goPath = new GameObject("RollPathLine");
        goPath.transform.SetParent(transform);
        goPath.layer = 0;
        pathLine = goPath.AddComponent<LineRenderer>();
        ConfigureLine(pathLine, Color.yellow, 0.2f);

        GameObject goFull = new GameObject("FullPathLine");
        goFull.transform.SetParent(transform);
        goFull.layer = 0;
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

    public void FaceTarget(Transform target, bool instant = false)
    {
        if (target == null) return;
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            if (instant)
            {
                transform.rotation = targetRotation;
            }
            else
            {
                // Smooth rotation using speed 15.0f
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15.0f);
            }
        }
    }

    void Update()
    {
        if (agent == null) return;

        if (healthSlider != null && stats != null)
        {
            if (healthSlider.maxValue != stats.MaxHP) healthSlider.maxValue = stats.MaxHP;
            healthSlider.value = stats.currentHP;
        }

        if (animator != null)
        {
            if (animator.applyRootMotion) animator.applyRootMotion = false;
            float currentSpeed = agent.velocity.magnitude;
            float animatorSpeed = isMoving ? Mathf.Max(currentSpeed, 2.0f) : currentSpeed;
            if (animatorSpeed < 0.05f) animatorSpeed = 0f;
            animator.SetFloat("Speed", animatorSpeed);
        }

        UpdatePathLines();
            
        if (isMoving)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                FinalizeMovement("Destination reached");
            }

            if (currentTarget != null)
            {
                float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
                if (dist < 3.0f)
                {
                    Debug.Log($"HeroController: Close enough to {currentTarget.name} (Dist: {dist:F2}m). Entering combat/resolution.");
                    GameObject poi = currentTarget;
                    FinalizeMovement("Proximity trigger");
                    
                    var enemy = poi.GetComponent<EnemyCombatant>();
                    if (enemy != null)
                    {
                        EnterCombat(poi);
                        StartCoroutine(InitialAttackCoroutine(enemy, leftoverDiceValue));
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

    private System.Collections.IEnumerator InitialAttackCoroutine(EnemyCombatant enemy, float diceValue)
    {
        Debug.Log($"<b>[Combat Flow]</b> Starting Arrival Attack. Leftover steps: {diceValue:F2}");
        
        FaceTarget(enemy.transform, true);
        enemy.FaceTarget(this.transform, true);

        // Wait a beat after settle/facing
        yield return new WaitForSeconds(0.45f);

        // FORMULA: Damage = (leftoverSteps * Multiplier) + (BaseAttack * 0.5)
        float damageFromSteps = diceValue * GlobalSettings.Instance.leftoverStepDamageMultiplier;
        float baseDmgBonus = stats != null ? stats.AttackDamage * 0.5f : 30f;
        float heroDamage = damageFromSteps + baseDmgBonus;

        if (stats != null && Random.Range(0f, 100f) < stats.CritChance)
        {
            heroDamage *= (1f + (stats.CritDamage / 100f));
            Debug.Log("<b>[Combat Flow]</b> <color=red>CRITICAL ARRIVAL HIT!</color>");
        }

        int finalDamage = Mathf.RoundToInt(heroDamage);
        if (finalDamage == 0 && diceValue > 0.01f) finalDamage = 1;

        if (animator != null)
        {
            animator.ResetTrigger("Attack");
            animator.SetTrigger("Attack");
        }

        // Wait for sword swing
        yield return new WaitForSeconds(0.35f);
        
        if (enemy != null)
        {
            Debug.Log($"<b>[Combat Flow]</b> Steve hits {enemy.name} for {finalDamage} arrival damage.");
            enemy.TakeDamage(finalDamage);

            if (enemy.IsDead)
            {
                VictoryFlourish();
                yield break;
            }

            yield return new WaitForSeconds(GlobalSettings.Instance.combatReactionDelay);

            if (enemy != null && enemy.gameObject != null && !enemy.IsDead)
            {
                enemy.PerformAttack(this);
            }
        }
    }

    private void UpdatePathLines()
    {
        bool show = GlobalSettings.Instance.showPath;
        if (show && agent.hasPath)
        {
            pathLine.enabled = true;
            pathLine.positionCount = agent.path.corners.Length;
            for (int i = 0; i < agent.path.corners.Length; i++) pathLine.SetPosition(i, agent.path.corners[i] + Vector3.up * 0.15f);
        }
        else pathLine.enabled = false;

        if (show && currentTarget != null)
        {
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(transform.position, currentTarget.transform.position, NavMesh.AllAreas, path))
            {
                fullPathLine.enabled = true;
                fullPathLine.positionCount = path.corners.Length;
                for (int i = 0; i < path.corners.Length; i++) fullPathLine.SetPosition(i, path.corners[i] + Vector3.up * 0.1f);
            }
            else fullPathLine.enabled = false;
        }
        else fullPathLine.enabled = false;
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
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < 1.0f) continue;
            if (dist < minDist) { minDist = dist; nearest = p; }
        }
        return nearest;
    }

    public void MoveSteps(int diceResult)
    {
        GlobalSettings settings = GlobalSettings.Instance;
        float totalMeters = diceResult * settings.stepsPerDiceValue * settings.metersPerStep;
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3.0f, NavMesh.AllAreas)) agent.Warp(hit.position);
            else return;
        }

        if (currentTarget == null) currentTarget = GetNearestPOI();
        if (currentTarget == null) return;

        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(transform.position, currentTarget.transform.position, NavMesh.AllAreas, path))
        {
            if (path.status != NavMeshPathStatus.PathInvalid)
            {
                float pathDist = 0;
                for (int i = 0; i < path.corners.Length - 1; i++) pathDist += Vector3.Distance(path.corners[i], path.corners[i+1]);

                if (pathDist < totalMeters)
                {
                    float usedDiceValue = pathDist / (settings.stepsPerDiceValue * settings.metersPerStep);
                    leftoverDiceValue = Mathf.Max(0, (float)diceResult - usedDiceValue);
                }
                else leftoverDiceValue = 0;

                Vector3 targetPoint = GetPointOnPath(path, totalMeters);
                agent.isStopped = false;
                agent.updateRotation = true;
                if (agent.SetDestination(targetPoint)) isMoving = true;
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
        return path.corners[path.corners.Length - 1];
    }

    public void EnterCombat(GameObject enemy)
    {
        inCombat = true;
        currentEnemy = enemy;
        isMoving = false;
        if (agent != null) { agent.isStopped = true; agent.velocity = Vector3.zero; }

        var ec = enemy.GetComponent<EnemyCombatant>();
        if (ec != null) ec.SetHealthBarVisible(true);

        if (animator != null)
        {
            animator.ResetTrigger("Throw"); animator.ResetTrigger("Attack"); animator.ResetTrigger("GetHit");
        }
        
        Vector3 direction = (enemy.transform.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
    }

    public void ExitCombat() { inCombat = false; currentEnemy = null; }

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
            if (stats.currentHP <= 0) { if (animator != null) animator.SetTrigger("Die"); }
            else if (animator != null) animator.SetTrigger("GetHit");
        }
        return tookDamage;
    }

    public void VictoryFlourish() { if (animator != null) animator.SetTrigger("Victory"); }
}