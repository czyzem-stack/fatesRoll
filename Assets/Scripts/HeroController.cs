using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Steve — combat, health, death. Travel is <see cref="SteveMovement"/>; animation is <see cref="SteveAnimator"/>.
/// Registers with <see cref="GameServices"/> in Awake (not <see cref="GameServiceBehaviour{T}"/>).
/// </summary>
public class HeroController : MonoBehaviour
{
    private NavMeshAgent agent;
    private SteveAnimator steveAnim;
    private SteveMovement movement;

    private bool isCelebrating;
    private bool isDead;
    private bool isRespawning;
    public bool IsCelebrating => isCelebrating;
    public bool IsDead => isDead;
    public bool IsRespawning => isRespawning;
    public bool IsMoving => movement != null && movement.IsMoving;
    public Enemy ApproachingEnemy => movement != null ? movement.ApproachingEnemy : null;
    public bool IsBlockedForDice =>
        isDead ||
        isRespawning ||
        isCelebrating ||
        (RunDeathController.HasInstance && RunDeathController.Instance.IsDeathInProgress) ||
        (RogueLiteManager.HasInstance && RogueLiteManager.Instance.IsRewardFlowActive) ||
        (EquipmentLootManager.HasInstance && EquipmentLootManager.Instance.IsRewardFlowActive);
    public float LevelUpCelebrationSeconds => 2.7f;

    private Coroutine celebrationRoutine;
    private bool visualYawFixApplied;

    [Tooltip("Extra Y rotation on the MC02 visual after auto-align (tune if still sideways).")]
    [SerializeField] private float extraVisualYawDegrees;

    private PlayerStats stats;
    private bool inCombat;
    public bool InCombat => inCombat;
    public GameObject currentEnemy;

    public UnityEngine.UI.Slider healthSlider;

    private Coroutine engageRoutine;
    public bool IsEngageBusy => engageRoutine != null;

    private Vector3 visualRigAuthoringLocal;
    private float agentBaseOffsetAuthoring;
    private bool hasAuthoringBaseline;
    private const float MaxSavedVisualLocalY = 0.6f;

    private Animator Animator => steveAnim != null ? steveAnim.RigAnimator : null;

    public void CaptureAuthoringBaseline(bool force = false)
    {
        if (hasAuthoringBaseline && !force)
            return;

        var anim = Animator;
        if (anim == null)
            return;

        Transform visual = HeroLocomotionUtility.GetVisualRigRoot(transform, anim);
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (Mathf.Abs(visual.localPosition.y) > MaxSavedVisualLocalY)
        {
            visual.localPosition = new Vector3(visual.localPosition.x, 0f, visual.localPosition.z);
            if (agent != null)
                agent.baseOffset = 0f;
        }

        visualRigAuthoringLocal = visual.localPosition;
        agentBaseOffsetAuthoring = agent != null ? agent.baseOffset : 0f;
        hasAuthoringBaseline = true;
    }

    public void ResetFeetAlignmentAuthoring()
    {
        var anim = Animator;
        if (anim == null)
            return;

        Transform visual = HeroLocomotionUtility.GetVisualRigRoot(transform, anim);
        visual.localPosition = new Vector3(visual.localPosition.x, 0f, visual.localPosition.z);

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.baseOffset = 0f;

        hasAuthoringBaseline = false;
        CaptureAuthoringBaseline(force: true);
    }

    public void CaptureVisualBaseline() => CaptureAuthoringBaseline();

    public void SnapBodyToGround()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        HeroLocomotionUtility.EnsureNavMeshFootPivot(agent);

        var anim = Animator;
        if (anim == null)
            return;

        if (!hasAuthoringBaseline)
            CaptureAuthoringBaseline();

        Transform visual = HeroLocomotionUtility.GetVisualRigRoot(transform, anim);
        visual.localPosition = visualRigAuthoringLocal;
        if (agent != null)
            agent.baseOffset = agentBaseOffsetAuthoring;

        anim.Update(0f);
        HeroLocomotionUtility.AlignVisualFeetToGround(
            transform, anim, agent, visualRigAuthoringLocal, agentBaseOffsetAuthoring);
    }

    public void ApplyVisualLocomotionAlignment()
    {
        var anim = Animator;
        if (anim == null)
            return;

        visualYawFixApplied = false;
        HeroLocomotionUtility.AlignVisualToAgentForward(
            transform, anim, ref visualYawFixApplied, extraVisualYawDegrees);
    }

    private void Awake()
    {
        GameServices.RegisterHero(this);
    }

    private void OnDestroy()
    {
        GameServices.UnregisterHero(this);
    }

    private void Start()
    {
        if (!GameServices.TryGet(out HeroController registered) || registered != this)
            GameServices.RegisterHero(this);

        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = gameObject.AddComponent<NavMeshAgent>();
        HeroLocomotionUtility.EnsureNavMeshFootPivot(agent);

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }
        else if (HeroSpawnUtility.TryResolveSpawnPosition(transform.position, out Vector3 grounded))
        {
            agent.Warp(grounded);
        }

        stats = GetComponent<PlayerStats>();
        if (stats == null)
            stats = gameObject.AddComponent<PlayerStats>();

        if (GetComponent<HeroEquipment>() == null)
            gameObject.AddComponent<HeroEquipment>();

        steveAnim = GetComponent<SteveAnimator>();
        if (steveAnim == null)
            steveAnim = gameObject.AddComponent<SteveAnimator>();

        movement = GetComponent<SteveMovement>();
        if (movement == null)
            movement = gameObject.AddComponent<SteveMovement>();

        steveAnim.Initialize(transform);
        movement.Initialize(agent, steveAnim);

        ResetFeetAlignmentAuthoring();
        ApplyVisualLocomotionAlignment();
        SnapBodyToGround();

        AutoAssignHealthUI();

        var settings = GlobalSettings.Instance;
        if (settings != null)
            agent.speed = settings.heroTravelSpeed;
        agent.acceleration = 30f;
        agent.stoppingDistance = 1f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        SetLayerRecursive(gameObject, 8);

        if (RunDeathController.Instance != null)
            RunDeathController.Instance.RecordHeroSpawn(this);
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        if (obj.GetComponent<UnityEngine.Canvas>() != null ||
            obj.GetComponent<UnityEngine.RectTransform>() != null ||
            obj.GetComponent<UnityEngine.LineRenderer>() != null ||
            obj.name.Contains("PathLine"))
        {
            int targetLayer = obj.GetComponent<UnityEngine.LineRenderer>() != null || obj.name.Contains("PathLine") ? 0 : 5;
            SetLayerRecursiveInternal(obj, targetLayer);
            return;
        }

        var renderer = obj.GetComponent<Renderer>();
        obj.layer = renderer != null ? layer : 0;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private static void SetLayerRecursiveInternal(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursiveInternal(child.gameObject, layer);
    }

    private void AutoAssignHealthUI()
    {
        if (healthSlider == null)
        {
            GameObject sliderGO = GameObject.Find("MainUI_Canvas/HUD_Profile/Slider_Bottom");
            if (sliderGO != null)
                healthSlider = sliderGO.GetComponent<UnityEngine.UI.Slider>();
        }

        UpdateHealthUI();
    }

    public void UpdateHealthUI()
    {
        if (healthSlider != null && stats != null)
        {
            healthSlider.maxValue = stats.MaxHP;
            healthSlider.value = stats.currentHP;
        }
    }

    public void FaceTarget(Transform target, bool instant = false, float speed = 15f)
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = instant
            ? targetRotation
            : Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * speed);
    }

    private void Update()
    {
        if (agent == null || isDead || isRespawning)
            return;

        if (healthSlider != null && stats != null)
        {
            if (healthSlider.maxValue != stats.MaxHP)
                healthSlider.maxValue = stats.MaxHP;
            healthSlider.value = stats.currentHP;
        }

        if ((inCombat && !IsMoving) || IsEngageBusy)
        {
            if (agent.isOnNavMesh)
            {
                if (!agent.isStopped)
                    agent.isStopped = true;
                if (agent.velocity.sqrMagnitude > 0.0001f)
                    agent.velocity = Vector3.zero;
            }
        }

        bool blockTravel = isCelebrating || IsEngageBusy;
        movement?.Tick(blockTravel);

        if (inCombat && currentEnemy != null && engageRoutine == null && !IsMoving)
        {
            var engaged = GetEnemyFromTarget(currentEnemy);
            if (engaged != null && IsWithinMeleeEngageRange(engaged, 0.35f))
                FaceTarget(engaged.transform, false, 12f);
        }
    }

    public void RecordRoll(int rollTotal) => movement?.RecordRoll(rollTotal);

    public void MoveSteps(int diceResult)
    {
        if (isCelebrating || isDead || isRespawning)
            return;

        movement?.MoveAfterRoll(diceResult);
    }

    public void ResetDiceMovement() => movement?.ResetAll();

    public void OnPOIDefeated(POINode node) => movement?.NotifyPoiDefeated(node);

    public Enemy GetPendingCombatEnemy() => movement != null ? movement.GetPendingCombatEnemy() : null;

    public Enemy GetCurrentEnemy() => GetEnemyFromTarget(currentEnemy);

    public void OnMovementArrivedAtEnemy(Enemy enemy)
    {
        int roll = movement != null ? movement.LastRollValue : 0;
        if (TryBeginMeleeWithRoll(enemy, roll))
            movement?.EndRoute();
        else
            movement?.OnRollSegmentEnded();
    }

    public void OnMovementArrivedAtPoi(POINode poi, GameObject target)
    {
        movement?.EndRoute();
        OnPOIDefeated(poi);
        POIResolve.Resolve(target);
    }

    public bool IsWithinChestInteractRange(Enemy chest)
    {
        if (chest == null || chest.isDead)
            return false;

        Vector3 a = transform.position;
        Vector3 b = chest.GetChestInteractPosition();
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b) <= GlobalSettings.GetChestInteractDistance();
    }

    public void OnMovementArrivedAtTreasureChest(Enemy chest)
    {
        if (chest == null || chest.isDead)
            return;

        if (!IsWithinChestInteractRange(chest))
        {
            movement?.OnRollSegmentEnded();
            return;
        }

        chest.OpenTreasureChest();
        movement?.EndRoute();
    }

    public bool TryBeginMeleeWithRoll(Enemy enemy, int rollTotal)
    {
        if (enemy == null || enemy.isDead || IsEngageBusy || inCombat)
            return false;

        if (enemy.IsTreasureChest)
            return false;

        if (!IsWithinMeleeEngageRange(enemy, 0.35f))
        {
            CombatLog.Info(
                $"Steve {DistanceToEnemy(enemy):F1}m out of fight range (engage {GlobalSettings.GetMeleeEngageDistance():F1}m) — roll again to close");
            return false;
        }

        movement?.RecordRoll(rollTotal);
        movement?.EndRoute();
        movement?.Stop();

        bool isCrit;
        int damage = CalculateRollDamage(rollTotal, out isCrit);

        CombatLog.Info($"Steve reached fight range ({DistanceToEnemy(enemy):F1}m) — attacks first");
        EnterCombat(enemy.gameObject);
        if (engageRoutine != null)
            StopCoroutine(engageRoutine);
        engageRoutine = StartCoroutine(SteveArrivalAttackRoutine(enemy, damage));
        return true;
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

        var settings = GlobalSettings.Instance;
        if (settings != null)
            yield return new WaitForSeconds(settings.combatReactionDelay);
        if (enemy != null && !enemy.isDead && inCombat)
            enemy.PerformAttack(this);

        engageRoutine = null;
    }

    public IEnumerator HeroAttackRoutine(Enemy enemy, int damage)
    {
        if (enemy == null || enemy.isDead)
            yield break;

        var settings = GlobalSettings.Instance;
        FaceTarget(enemy.transform, false, 16f);
        enemy.FaceTarget(transform, false, 16f);
        yield return new WaitForSeconds(settings != null ? settings.combatFaceDelay : 0.06f);

        steveAnim?.PlayAttack();
        yield return new WaitForSeconds(settings != null ? settings.combatHeroHitDelay : 0.22f);

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
            heroDamage *= 1f + stats.CritDamage / 100f;
        }

        if (stats != null)
            CombatLog.CritCheck("Steve", stats.CritChance, critRoll, isCrit);

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(heroDamage));
        CombatLog.DamageCalc("Steve", $"dice roll {rollTotal} | base {baseDamage:F0} × roll/7 → {finalDamage}" + (isCrit ? " (crit)" : ""));
        return finalDamage;
    }

    public void EnterCombat(GameObject enemy)
    {
        Enemy ec = GetEnemyFromTarget(enemy);
        if (ec == null)
            return;

        inCombat = true;
        currentEnemy = ec.gameObject;
        movement?.Stop();

        if (agent != null)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.updateRotation = true;
        }

        ec.SetHealthBarVisible(true);
        CombatLog.EnterCombat(gameObject.name, ec.gameObject.name);
        steveAnim?.SetSpeed(0f);
        steveAnim?.ResetActionTriggers();
    }

    public void ExitCombat()
    {
        inCombat = false;
        currentEnemy = null;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.stoppingDistance = 1f;
            agent.updateRotation = true;
        }
    }

    public bool TakeDamage(int amount, string attackerName = "Enemy", bool attackerCrit = false)
    {
        if (isDead || stats == null || stats.currentHP <= 0)
            return false;

        if (attackerCrit)
            CombatLog.DamageCalc(attackerName, $"hit Steve for {amount} (critical)");

        bool tookDamage = stats.TakeDamage(amount);
        UpdateHealthUI();
        if (!tookDamage)
            return false;

        if (Application.isPlaying)
        {
            GameObject go = new GameObject("FloatingText_Damage");
            go.transform.position = transform.position + Vector3.up * 2.2f;
            go.AddComponent<FloatingText>().Setup($"-{amount} HP", Color.red);
        }

        if (stats.currentHP <= 0)
        {
            CombatLog.Death("Steve");
            steveAnim?.PlayDie();
            BeginDeathSequence();
        }
        else
            steveAnim?.PlayGetHit();

        return true;
    }

    private void BeginDeathSequence()
    {
        if (isDead)
            return;
        isDead = true;

        if (engageRoutine != null)
        {
            StopCoroutine(engageRoutine);
            engageRoutine = null;
        }

        movement?.Stop();
        ExitCombat();

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (RunDeathController.Instance == null)
            new GameObject("RunDeathController").AddComponent<RunDeathController>();

        if (GameServices.TryGet(out DiceSpawner diceSpawner))
            diceSpawner.CancelActiveRoll();
        RunDeathController.Instance.HandleHeroDeath(this);
    }

    public void PrepareForRespawnAtSpawn(Vector3 position, Quaternion rotation)
    {
        isRespawning = true;
        isDead = true;
        isCelebrating = false;

        if (engageRoutine != null)
        {
            StopCoroutine(engageRoutine);
            engageRoutine = null;
        }

        ExitCombat();
        movement?.ResetAll();

        HeroSpawnUtility.PlaceHero(this, position, rotation);

        if (stats != null)
        {
            stats.RestoreFullHealth();
            UpdateHealthUI();
        }

        steveAnim?.Initialize(transform);
        ApplyVisualLocomotionAlignment();
        SnapBodyToGround();

        var anim = Animator;
        if (anim != null)
        {
            steveAnim?.SetSpeed(0f);
            steveAnim?.ResetActionTriggers();
            if (!HeroDeathAnimHelper.PlayDeadStay(anim))
                steveAnim?.PlayDie();
        }

        GlobalSettings.LogGameplay("HeroController: Steve at spawn (dead pose).");
    }

    public IEnumerator PlayStandUpFromDeathRoutine(float standUpSeconds)
    {
        var anim = Animator;
        if (anim != null)
        {
            if (!HeroDeathAnimHelper.PlayGetUp(anim))
            {
                HeroAnimatorParams.SetBoolSafe(anim, "IsDead", false);
                HeroAnimatorParams.ResetTriggerSafe(anim, HeroAnimatorParams.Die);
            }
        }

        if (standUpSeconds > 0f)
            yield return new WaitForSeconds(standUpSeconds);

        if (anim != null)
            HeroDeathAnimHelper.ResetToIdle(anim);

        HeroSpawnUtility.PlaceHero(this, transform.position, transform.rotation);
        steveAnim?.Initialize(transform);
        ApplyVisualLocomotionAlignment();

        isDead = false;
        isRespawning = false;
        movement?.ResetAll();
        GlobalSettings.LogGameplay("HeroController: Steve stood up — ready to roll.");
    }

    public void PrepareForRespawn(Vector3 position, Quaternion rotation) =>
        PrepareForRespawnAtSpawn(position, rotation);

    public void VictoryFlourish() => steveAnim?.PlayVictory();

    public void PlayLevelUpCelebration()
    {
        isCelebrating = true;
        movement?.Stop();

        if (stats != null)
        {
            stats.RestoreFullHealth();
            UpdateHealthUI();
        }

        EnergyManager.Instance?.RestoreFull();
        steveAnim?.PlayLevelUp();

        if (celebrationRoutine != null)
            StopCoroutine(celebrationRoutine);
        celebrationRoutine = StartCoroutine(LevelUpCelebrationRoutine());
    }

    private IEnumerator LevelUpCelebrationRoutine()
    {
        yield return new WaitForSeconds(LevelUpCelebrationSeconds);

        if (RogueLiteManager.Instance != null)
            yield return RogueLiteManager.Instance.RunPostCelebrationRewards();

        isCelebrating = false;
        celebrationRoutine = null;
    }

    public void PlayChestRewardCelebration(bool instantOpen = false)
    {
        if (EquipmentLootManager.Instance == null || !EquipmentLootManager.Instance.HasPendingChestRewards)
            return;

        isCelebrating = true;
        movement?.ResetAll();

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (!instantOpen)
            steveAnim?.PlayLevelUp();

        if (celebrationRoutine != null)
            StopCoroutine(celebrationRoutine);
        celebrationRoutine = StartCoroutine(ChestRewardCelebrationRoutine(instantOpen));
    }

    private IEnumerator ChestRewardCelebrationRoutine(bool instantOpen)
    {
        if (!instantOpen)
            yield return new WaitForSeconds(LevelUpCelebrationSeconds);

        if (EquipmentLootManager.Instance != null)
            yield return EquipmentLootManager.Instance.RunChestRewards();

        isCelebrating = false;
        celebrationRoutine = null;
    }

    private static Enemy GetEnemyFromTarget(GameObject target)
    {
        if (target == null)
            return null;
        return target.GetComponent<Enemy>() ?? target.GetComponentInChildren<Enemy>();
    }

    private float DistanceToEnemy(Enemy enemy)
    {
        if (enemy == null)
            return float.MaxValue;
        Vector3 a = transform.position;
        Vector3 b = enemy.GetEngagePosition();
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private bool IsWithinMeleeEngageRange(Enemy enemy, float extraBuffer = 0f) =>
        enemy != null && DistanceToEnemy(enemy) <= GlobalSettings.GetMeleeEngageDistance() + extraBuffer;
}
