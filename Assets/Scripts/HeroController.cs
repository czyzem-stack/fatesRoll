using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class HeroController : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private bool isMoving = false;
    public bool IsMoving => isMoving;

    private bool isCelebrating = false;
    public bool IsCelebrating => isCelebrating;
    public float LevelUpCelebrationSeconds => 2.7f;

    private Coroutine celebrationRoutine;

    private PlayerStats stats;
    private bool inCombat = false;
    public bool InCombat => inCombat;
    public GameObject currentEnemy;

    public UnityEngine.UI.Slider healthSlider;

    private LineRenderer pathLine;
    private LineRenderer fullPathLine;

    private GameObject currentTarget;
    private Enemy approachingEnemy;
    private int nextPOIOrder = 0;
    private Coroutine engageRoutine;
    private int lastRollValue;

    public bool IsEngageBusy => engageRoutine != null;

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

        agent.speed = GlobalSettings.Instance.heroTravelSpeed;
        agent.acceleration = 30.0f;
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

    public void FaceTarget(Transform target, bool instant = false, float speed = 15.0f)
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
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * speed);
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
            if (isCelebrating || IsEngageBusy || (inCombat && !isMoving))
            {
                animator.SetFloat("Speed", 0f, 0.15f, Time.deltaTime);
            }
            else if (isMoving)
            {
                float animatorSpeed = Mathf.Max(agent.velocity.magnitude, 2.0f);
                animator.SetFloat("Speed", animatorSpeed, 0.15f, Time.deltaTime);
            }
            else
            {
                float animatorSpeed = agent.velocity.magnitude < 0.35f ? 0f : agent.velocity.magnitude;
                animator.SetFloat("Speed", animatorSpeed, 0.15f, Time.deltaTime);
            }
        }

        if ((inCombat && !isMoving) || IsEngageBusy)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        UpdatePathLines();

        if (inCombat && currentEnemy != null && engageRoutine == null)
        {
            var engaged = GetEnemyFromTarget(currentEnemy);
            if (engaged != null && engaged.IsWithinEngageRange(this))
                FaceTarget(engaged.transform, false, 20.0f);
        }

        if (isMoving && !isCelebrating && !IsEngageBusy)
        {
            Enemy targetEnemy = approachingEnemy ?? GetEnemyFromTarget(currentTarget);
            if (targetEnemy != null)
            {
                if (targetEnemy.IsWithinEngageRange(this))
                {
                    OnArrivedNearEnemy(targetEnemy);
                    return;
                }

                bool reachedDest = !agent.pathPending &&
                    agent.remainingDistance <= agent.stoppingDistance + 0.2f;

                if (reachedDest)
                {
                    OnArrivedNearEnemy(targetEnemy);
                    return;
                }
            }
            else if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                FinalizeMovement("Destination reached");
                GameObject target = currentTarget;
                currentTarget = null;
                if (target != null)
                {
                    var poi = target.GetComponent<POINode>() ?? target.GetComponentInParent<POINode>();
                    OnPOIDefeated(poi);
                    if (POIManager.Instance != null)
                        POIManager.Instance.ResolvePOI(target);
                }
            }
        }
    }

    private static Enemy GetEnemyFromTarget(GameObject target)
    {
        if (target == null) return null;
        return target.GetComponent<Enemy>() ?? target.GetComponentInChildren<Enemy>();
    }

    private float DistanceToEnemy(Enemy enemy)
    {
        if (enemy == null) return float.MaxValue;
        Vector3 a = transform.position;
        Vector3 b = enemy.GetEngagePosition();
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void SnapToMeleeRange(Enemy enemy)
    {
        if (enemy == null || agent == null || !agent.isOnNavMesh)
            return;

        float standoff = GlobalSettings.Instance.heroMeleeStandoff;
        if (DistanceToEnemy(enemy) <= standoff + 0.6f)
            return;

        Vector3 approach = enemy.GetMeleeApproachPoint(transform.position);
        if (NavMesh.SamplePosition(approach, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    public void RecordRoll(int rollTotal)
    {
        lastRollValue = rollTotal;
    }

    /// <summary>Mark a POI as done so the next roll targets the following visit order.</summary>
    public void OnPOIDefeated(POINode node)
    {
        currentTarget = null;
        approachingEnemy = null;
        if (node != null)
            nextPOIOrder = Mathf.Max(nextPOIOrder, node.order + 1);
    }

    private void AdvancePastEnemyPOI(Enemy enemy)
    {
        if (enemy == null) return;
        OnPOIDefeated(enemy.GetComponentInParent<POINode>());
    }

    private bool IsCurrentTargetUsable()
    {
        if (currentTarget == null) return false;
        var poi = currentTarget.GetComponent<POINode>();
        if (poi == null) poi = currentTarget.GetComponentInParent<POINode>();
        if (poi == null) return false;

        var enemy = GetEnemyFromTarget(currentTarget);
        if (enemy != null && enemy.isDead) return false;
        return true;
    }

    /// <summary>Steve finished dice movement near a POI enemy — attack if in range, else wait for another roll.</summary>
    private void OnArrivedNearEnemy(Enemy enemy)
    {
        approachingEnemy = null;
        isMoving = false;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.updateRotation = false;
            agent.stoppingDistance = 1.0f;
        }

        if (enemy == null || enemy.isDead)
        {
            AdvancePastEnemyPOI(enemy);
            return;
        }

        if (IsEngageBusy) return;

        float dist = DistanceToEnemy(enemy);
        if (!enemy.IsWithinEngageRange(this))
        {
            CombatLog.Info($"Steve stopped {dist:F1}m out of fight range — roll again to close");
            ExitCombat();
            return;
        }

        currentTarget = null;
        AdvancePastEnemyPOI(enemy);

        SnapToMeleeRange(enemy);

        CombatLog.Info($"Steve reached fight range ({dist:F1}m) — attacks first");
        bool isCrit;
        int damage = CalculateRollDamage(lastRollValue, out isCrit);
        EnterCombat(enemy.gameObject);
        if (engageRoutine != null) StopCoroutine(engageRoutine);
        engageRoutine = StartCoroutine(SteveArrivalAttackRoutine(enemy, damage));
    }

    /// <summary>Enemy closed distance first — orc attacks, then Steve can roll in combat.</summary>
    public void OnEnemyAggroAttack(Enemy enemy)
    {
        if (enemy == null || enemy.isDead || IsEngageBusy) return;
        if (!enemy.IsWithinEngageRange(this))
        {
            CombatLog.Info("Enemy aggro ignored — Steve out of fight range");
            return;
        }

        SnapToMeleeRange(enemy);

        CombatLog.Info("Enemy reached Steve first — enemy attacks");
        EnterCombat(enemy.gameObject);
        if (engageRoutine != null) StopCoroutine(engageRoutine);
        engageRoutine = StartCoroutine(EnemyFirstAttackRoutine(enemy));
    }

    private IEnumerator SteveArrivalAttackRoutine(Enemy enemy, int damage)
    {
        yield return HeroAttackRoutine(enemy, damage);

        if (enemy != null && enemy.isDead)
        {
            VictoryFlourish();
            engageRoutine = null;
            yield break;
        }

        yield return new WaitForSeconds(GlobalSettings.Instance.combatReactionDelay);
        if (enemy != null && !enemy.isDead && inCombat)
            enemy.PerformAttack(this);

        engageRoutine = null;
    }

    private IEnumerator EnemyFirstAttackRoutine(Enemy enemy)
    {
        enemy.PerformAttack(this);
        yield return new WaitForSeconds(enemy.AttackDurationSeconds);
        engageRoutine = null;
    }

    public IEnumerator HeroAttackRoutine(Enemy enemy, int damage)
    {
        if (enemy == null || enemy.isDead) yield break;

        var settings = GlobalSettings.Instance;
        FaceTarget(enemy.transform, true);
        enemy.FaceTarget(transform, true);
        yield return new WaitForSeconds(settings.combatFaceDelay);

        if (animator != null)
        {
            animator.ResetTrigger("Attack");
            animator.SetTrigger("Attack");
        }

        yield return new WaitForSeconds(settings.combatHeroHitDelay);

        if (enemy != null && !enemy.isDead)
        {
            CombatLog.AttackStart("Steve", enemy.name, "hero melee");
            bool hit = enemy.TakeDamage(damage, "Steve");
            if (!hit)
                CombatLog.DamageMitigated("Steve", enemy.name, "dodged");
        }
    }

    public int CalculateRollDamage(int rollTotal, out bool isCrit)
    {
        isCrit = false;
        float baseDamage = stats != null ? stats.AttackDamage : 20f;
        float heroDamage = baseDamage * (rollTotal / 7.0f);
        float critRoll = stats != null ? Random.Range(0f, 100f) : 100f;

        if (stats != null && critRoll < stats.CritChance)
        {
            isCrit = true;
            heroDamage *= (1f + (stats.CritDamage / 100f));
        }

        if (stats != null)
            CombatLog.CritCheck("Steve", stats.CritChance, critRoll, isCrit);

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(heroDamage));
        CombatLog.DamageCalc("Steve", $"dice roll {rollTotal} | base {baseDamage:F0} × roll/7 → {finalDamage}" + (isCrit ? " (crit)" : ""));
        return finalDamage;
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
            Enemy pathEnemy = GetEnemyFromTarget(currentTarget);
            Vector3 pathGoal = pathEnemy != null
                ? pathEnemy.GetMeleeApproachPoint(transform.position)
                : currentTarget.transform.position;
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(transform.position, pathGoal, NavMesh.AllAreas, path))
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
        approachingEnemy = null;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.updateRotation = false;
        }
        Debug.Log($"HeroController: Movement Finalized. Reason: {reason}");
    }

    public void MoveSteps(int diceResult)
    {
        if (isCelebrating) return;

        lastRollValue = diceResult;
        GlobalSettings settings = GlobalSettings.Instance;
        float totalMeters = diceResult * settings.stepsPerDiceValue * settings.metersPerStep;
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 3.0f, NavMesh.AllAreas)) agent.Warp(hit.position);
            else return;
        }

        if (!IsCurrentTargetUsable())
            currentTarget = null;

        if (currentTarget == null && POIManager.Instance != null)
            currentTarget = POIManager.Instance.GetPOIByOrder(nextPOIOrder);

        if (currentTarget == null) return;

        Enemy enemy = GetEnemyFromTarget(currentTarget);
        Vector3 pathGoal = enemy != null
            ? enemy.GetMeleeApproachPoint(transform.position)
            : currentTarget.transform.position;

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, pathGoal, NavMesh.AllAreas, path) ||
            path.status == NavMeshPathStatus.PathInvalid)
        {
            return;
        }

        float pathDist = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
            pathDist += Vector3.Distance(path.corners[i], path.corners[i + 1]);

        float moveMeters = Mathf.Min(totalMeters, pathDist);
        Vector3 targetPoint = GetPointOnPath(path, moveMeters);

        NavMeshHit destHit;
        if (NavMesh.SamplePosition(targetPoint, out destHit, 2.0f, NavMesh.AllAreas))
            targetPoint = destHit.position;

        approachingEnemy = enemy;
        agent.speed = settings.heroTravelSpeed;
        agent.stoppingDistance = enemy != null ? 0.05f : 1.0f;
        agent.isStopped = false;
        agent.updateRotation = true;

        if (agent.SetDestination(targetPoint))
        {
            isMoving = true;
            if (enemy != null)
                CombatLog.Info($"Steve moving toward {enemy.name} (up to {moveMeters:F0}m on path)");
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

        var ec = enemy != null ? enemy.GetComponent<Enemy>() : null;
        if (ec != null)
        {
            ec.SetHealthBarVisible(true);
            CombatLog.EnterCombat(gameObject.name, enemy.name);
        }

        if (animator != null)
        {
            animator.ResetTrigger("Throw"); animator.ResetTrigger("Attack"); animator.ResetTrigger("GetHit");
        }

        Vector3 faceFrom = ec != null ? ec.GetEngagePosition() : enemy.transform.position;
        Vector3 direction = (faceFrom - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
    }

    public void ExitCombat()
    {
        inCombat = false;
        currentEnemy = null;
        approachingEnemy = null;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.stoppingDistance = 1.0f;
            agent.updateRotation = true;
        }
    }

    public bool TakeDamage(int amount, string attackerName = "Enemy", bool attackerCrit = false)
    {
        if (stats == null || stats.currentHP <= 0) return false;

        if (attackerCrit)
            CombatLog.DamageCalc(attackerName, $"hit Steve for {amount} (critical)");

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
                CombatLog.Death("Steve");
                if (animator != null) animator.SetTrigger("Die");
            }
            else if (animator != null) animator.SetTrigger("GetHit");
        }
        return tookDamage;
    }

    public void VictoryFlourish() { if (animator != null) animator.SetTrigger("Victory"); }

    public void PlayLevelUpCelebration()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();

        isCelebrating = true;
        isMoving = false;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (stats != null)
        {
            stats.RestoreFullHealth();
            UpdateHealthUI();
        }

        if (EnergyManager.Instance != null)
        {
            EnergyManager.Instance.RestoreFull();
        }

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.ResetTrigger("LevelUp");
            animator.SetTrigger("LevelUp");
        }

        if (celebrationRoutine != null) StopCoroutine(celebrationRoutine);
        celebrationRoutine = StartCoroutine(LevelUpCelebrationRoutine());
    }

    private System.Collections.IEnumerator LevelUpCelebrationRoutine()
    {
        yield return new WaitForSeconds(LevelUpCelebrationSeconds);
        isCelebrating = false;
        celebrationRoutine = null;
    }
}