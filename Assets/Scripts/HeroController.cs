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
    private bool hasFearEffect = false;
    private int burnTurnsRemaining = 0;
    private int burnDamagePerTurn = 0;
    private int curseTurnsRemaining = 0;
    private float curseReductionPercent = 0f;
    private int poisonTurnsRemaining = 0;
    private int poisonDamagePerTurn = 0;

    public bool IsCelebrating => isCelebrating;
    public bool IsDead => isDead;
    public bool IsRespawning => isRespawning;
    public bool HasFearEffect => hasFearEffect;
    public bool IsBurned => burnTurnsRemaining > 0;
    public bool IsCursed => curseTurnsRemaining > 0;
    public bool IsPoisoned => poisonTurnsRemaining > 0;
    public float CurseReductionPercent => curseReductionPercent;
public bool IsMoving => movement != null && movement.IsMoving;
    public Enemy ApproachingEnemy => movement != null ? movement.ApproachingEnemy : null;
    public bool IsBlockedForDice =>
        isDead ||
        isRespawning ||
        isCelebrating ||
        (GameServices.TryGet(out RunDeathController runDeath) && runDeath.IsDeathInProgress) ||
        (RogueLiteManager.HasInstance && RogueLiteManager.Instance.IsRewardFlowActive) ||
        (EquipmentLootManager.HasInstance && EquipmentLootManager.Instance.IsRewardFlowActive);
    public float LevelUpCelebrationSeconds => 2.7f;

    private Coroutine celebrationRoutine;
    private bool visualYawFixApplied;

    [Tooltip("Extra Y rotation on the MC02 visual after auto-align (tune if still sideways).")]
    [SerializeField] private float extraVisualYawDegrees;

    private PlayerStats stats;

    /// <summary>Simple combat state machine for consistent tracking across Hero, Enemy queries, and POI resolution.</summary>
    public enum CombatState { Idle, Moving, InCombat, Dead }

    private CombatState combatState = CombatState.Idle;
    /// <summary>Current combat state. Single source of truth for Hero/Enemy/POI interactions.</summary>
    public CombatState State => combatState;

    /// <summary>True when the hero is actively engaged with an enemy.</summary>
    public bool InCombat => combatState == CombatState.InCombat;

    /// <summary>Current enemy being fought (cleared on exit/death).</summary>
    public GameObject currentEnemy;

    public UnityEngine.UI.Slider healthSlider;
    public float healthLerpSpeed = 5f;

    private Coroutine engageRoutine;
    public bool IsEngageBusy => engageRoutine != null;

    private Vector3 visualRigAuthoringLocal;
    private float agentBaseOffsetAuthoring;
    private bool hasAuthoringBaseline;
    private Renderer[] footMeshRenderers;
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
            transform, anim, agent, visualRigAuthoringLocal, agentBaseOffsetAuthoring, ref footMeshRenderers);
    }

    public void ApplyVisualLocomotionAlignment()
    {
        var anim = Animator;
        if (anim == null)
            return;

        visualYawFixApplied = false;
        footMeshRenderers = null;
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
        agent.acceleration = GameConstants.DefaultHeroAcceleration;
        agent.stoppingDistance = GameConstants.DefaultHeroStoppingDistance;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        SetLayerRecursive(gameObject, 8);

        if (GameServices.TryGet(out RunDeathController runDeath))
            runDeath.RecordHeroSpawn(this);
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
            healthSlider = MainUiHud.FindComponentAlongPaths<UnityEngine.UI.Slider>(
                "MainUI_Canvas/Profile/Slider_Bottom",
                "MainUI_Canvas/HUD_Profile/Slider_Bottom",
                "MainUI_Canvas/HUD_Profile/Slider_LevelProfile/Slider_Bottom");
        }

        UpdateHealthUI();
    }

    public void UpdateHealthUI()
    {
        if (healthSlider != null && stats != null)
        {
            if (healthSlider.maxValue != stats.MaxHP)
                healthSlider.maxValue = stats.MaxHP;

            if (Application.isPlaying)
            {
                healthSlider.value = Mathf.Lerp(healthSlider.value, stats.currentHP, Time.deltaTime * healthLerpSpeed);
                if (Mathf.Abs(healthSlider.value - stats.currentHP) < 0.01f)
                    healthSlider.value = stats.currentHP;
            }
            else
            {
                healthSlider.value = stats.currentHP;
            }
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
        UpdateHealthUI();

        if (agent == null || isDead || isRespawning)
            return;

        if ((InCombat && !IsMoving) || IsEngageBusy)
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

        if (InCombat && currentEnemy != null && engageRoutine == null && !IsMoving)
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

        if (!InCombat && State != CombatState.Moving && State != CombatState.Dead)
            TransitionCombatState(CombatState.Moving, "dice travel");

        movement?.MoveAfterRoll(diceResult);
    }

    public void ResetDiceMovement() => movement?.ResetAll();

    public void OnPOIDefeated(POINode node) 
    {
        ClearFearEffect();
        ClearBurnEffect();
        ClearCurseEffect();
        ClearPoisonEffect();
        movement?.NotifyPoiDefeated(node);
    }

    public Enemy GetPendingCombatEnemy() => movement != null ? movement.GetPendingCombatEnemy() : null;

    public Enemy GetCurrentEnemy() => GetEnemyFromTarget(currentEnemy);

    public void OnMovementArrivedAtEnemy(Enemy enemy)
    {
        int roll = movement != null ? movement.LastRollValue : 0;
        bool started = TryBeginMeleeWithRoll(enemy, roll)
            || TryBeginMeleeWithRoll(enemy, roll, engageExtraBuffer: 1.75f);
        if (started)
            movement?.EndRoute();
        else
            movement?.OnRollSegmentEnded();
    }

    public void OnMovementArrivedAtPoi(POINode poi, GameObject target)
    {
        Enemy enemy = GetEnemyFromTarget(target);
        if (enemy != null && !enemy.IsTreasureChest && !enemy.isDead)
        {
            int roll = movement != null ? movement.LastRollValue : 0;
            bool started = TryBeginMeleeWithRoll(enemy, roll)
                || TryBeginMeleeWithRoll(enemy, roll, engageExtraBuffer: 1.75f);
            if (started)
            {
                movement?.EndRoute();
                return;
            }

            CombatLog.Info("Steve reached POI — still outside fight range; roll again to close distance.");
            movement?.OnRollSegmentEnded();
            return;
        }

        movement?.EndRoute();
        OnPOIDefeated(poi);
        TransitionCombatState(CombatState.Idle, "arrived at POI (resolved)");
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
        TransitionCombatState(CombatState.Idle, "arrived at treasure chest");
    }

    public bool TryBeginMeleeWithRoll(Enemy enemy, int rollTotal, float engageExtraBuffer = 0.35f)
    {
        if (enemy == null || enemy.isDead || IsEngageBusy || InCombat || State == CombatState.Dead)
            return false;

        if (enemy.IsTreasureChest)
            return false;

        if (!IsWithinMeleeEngageRange(enemy, engageExtraBuffer))
        {
            if (engageExtraBuffer <= 0.5f)
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
        if (enemy != null && !enemy.isDead && InCombat)
            enemy.PerformAttack(this);

        engageRoutine = null;
    }

    /// <summary>Core attack coroutine used by both arrival and dice-triggered attacks.</summary>
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

        if (enemy != null && !enemy.isDead && InCombat && State != CombatState.Dead)
        {
            if (hasFearEffect)
            {
                float missChance = 25f;
                if (GameServices.TryGet(out EnemySpecialController esc))
                    missChance = esc.GetEffectValue(POIType.Bat, 25f);

                float missRoll = Random.Range(0f, 100f);
                if (missRoll < missChance)
                {
                    CombatLog.Info("<color=red>Steve MISSED due to Fear!</color>");
                    if (Application.isPlaying)
                    {
                        string txt = "MISS!";
                        Color col = Color.red;
                        if (GameServices.TryGet(out EnemySpecialController escText))
                        {
                            txt = escText.GetFloatingText(POIType.Bat, txt);
                            col = escText.GetTextColor(POIType.Bat, col);
                        }

                        GameObject missGo = new GameObject("MissText");
                        missGo.transform.position = transform.position + Vector3.up * 2.8f;
                        missGo.AddComponent<FloatingText>().Setup(txt, col);
                    }
                    yield return new WaitForSeconds(settings != null ? settings.combatHeroAttackRecoverDelay : 0.4f);
                    yield break;
                }
            }

            CombatLog.AttackStart("Steve", enemy.name, "hero melee");
            bool hit = enemy.TakeDamage(damage, "Steve");
            if (!hit)
                CombatLog.DamageMitigated("Steve", enemy.name, "dodged");
        }

        yield return new WaitForSeconds(settings != null ? settings.combatHeroAttackRecoverDelay : 0.4f);
    }

    /// <summary>Converts a dice roll into damage (with crit roll). Used by dice attack paths.</summary>
    public int CalculateRollDamage(int rollTotal, out bool isCrit)
    {
        float baseDamage = stats != null ? stats.AttackDamage : 20f;
        float heroDamage = baseDamage * (rollTotal / 7.0f);

        float critChance = stats != null ? stats.CritChance : 0f;
        float critMult = stats != null ? stats.CritDamage : 0f;

        heroDamage = CombatLog.RollAndApplyCrit(heroDamage, critChance, critMult, out isCrit);

        if (stats != null)
            CombatLog.CritCheck("Steve", critChance, Random.Range(0f, 100f), isCrit); // roll shown for logging only

        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(heroDamage));
        CombatLog.DamageCalc("Steve", $"dice roll {rollTotal} | base {baseDamage:F0} × roll/7 → {finalDamage}" + (isCrit ? " (crit)" : ""));
        return finalDamage;
    }

    /// <summary>Public entry point to begin combat with a target enemy.</summary>
    public void EnterCombat(GameObject enemy)
    {
        Enemy ec = GetEnemyFromTarget(enemy);
        if (ec == null)
            return;

        currentEnemy = ec.gameObject;
        movement?.Stop();

        if (agent != null)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.updateRotation = true;
        }

        ec.SetHealthBarVisible(true);
        TransitionCombatState(CombatState.InCombat, ec.gameObject.name);
        CombatLog.EnterCombat(gameObject.name, ec.gameObject.name);
        steveAnim?.SetSpeed(0f);
        steveAnim?.ResetActionTriggers();
    }

    public void ExitCombat()
    {
        ClearFearEffect();
        if (combatState == CombatState.InCombat)
{
            TransitionCombatState(CombatState.Idle, "exit combat");
        }
        currentEnemy = null;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.stoppingDistance = GameConstants.DefaultHeroStoppingDistance;
            agent.updateRotation = true;
        }

        if (RogueLiteManager.HasInstance && RogueLiteManager.Instance.IsRewardFlowActive == false)
        {
            // If we have rewards waiting, trigger the flow (and animation since we are out of combat)
            // But we don't want to trigger it if we are already in another flow.
            // LevelManager doesn't track if PlayLevelUpCelebration was skipped.
            // But we can check if there are pending levels.
            // We'll call PlayLevelUpCelebration which now handles the combat check and stat restore.
            // Since we just exited combat, InCombat will be false.
            // Note: PlayLevelUpCelebration will restore health AGAIN, but that's fine.
            
            // To avoid double-restoring or weirdness, we only call it if we actually have pending levels.
            // We need a way to check pendingLevels in RogueLiteManager.
        }
    }

    /// <summary>Centralized state transition with logging. All combat/movement mode changes go through here.</summary>
    private void TransitionCombatState(CombatState newState, string reason = "")
    {
        if (combatState == newState) return;
        CombatState previous = combatState;
        combatState = newState;
        
        if (steveAnim != null)
            steveAnim.SetInCombat(newState == CombatState.InCombat);

        CombatLog.Info($"[State] Hero {previous} -> {newState}" + (string.IsNullOrEmpty(reason) ? "" : $" ({reason})"));
    }

    public bool TakeDamage(int amount, string attackerName = "Enemy", bool attackerCrit = false, bool ignoreDodge = false)
    {
        if (isDead || stats == null || stats.currentHP <= 0)
            return false;

        if (attackerCrit)
            CombatLog.DamageCalc(attackerName, $"hit Steve for {amount} (critical)");

        bool tookDamage = false;
        if (ignoreDodge)
        {
            stats.currentHP -= amount;
            if (stats.currentHP < 0) stats.currentHP = 0;
            CombatLog.DamageTaken(gameObject.name, amount, stats.currentHP + amount, stats.currentHP);
            tookDamage = true;
        }
        else
        {
            tookDamage = stats.TakeDamage(amount);
        }

        UpdateHealthUI();
        
        if (!tookDamage)
        {
            if (Application.isPlaying)
            {
                GameObject go = new GameObject("DodgeText");
                go.transform.position = transform.position + Vector3.up * 2.8f;
                var ft = go.AddComponent<FloatingText>();
                ft.Setup("DODGE!", Color.cyan);
            }
            return false;
        }

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
        ClearFearEffect();

        if (engageRoutine != null)
{
            StopCoroutine(engageRoutine);
            engageRoutine = null;
        }

        movement?.Stop();
        TransitionCombatState(CombatState.Dead, "hero death");
        currentEnemy = null;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (GameServices.TryGet(out DiceSpawner diceSpawner))
            diceSpawner.CancelActiveRoll();

        if (!GameServices.TryGet(out RunDeathController runDeath))
        {
            Debug.LogError(
                "HeroController: RunDeathController missing on GameServices bootstrap. " +
                "Add RunDeathController under the bootstrap object (FatesRoll → Setup → Add Game Services Bootstrap).",
                this);
            return;
        }

        runDeath.HandleHeroDeath(this);
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

        TransitionCombatState(CombatState.Dead, "prepare respawn");
        currentEnemy = null;
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
        TransitionCombatState(CombatState.Idle, "stood up after death");
        movement?.ResetAll();
        GlobalSettings.LogGameplay("HeroController: Steve stood up — ready to roll.");
    }

    public void PrepareForRespawn(Vector3 position, Quaternion rotation) =>
        PrepareForRespawnAtSpawn(position, rotation);

    public void VictoryFlourish() => steveAnim?.PlayVictory();

    public void ApplyFearEffect()
    {
        if (hasFearEffect) return;
        hasFearEffect = true;
        CombatLog.Info("<color=purple>Steve is AFRAID! (25% miss chance)</color>");
        
        if (Application.isPlaying)
        {
            GameObject go = new GameObject("FearText");
            go.transform.position = transform.position + Vector3.up * 2.8f;
            var ft = go.AddComponent<FloatingText>();
            ft.Setup("FEAR!", Color.magenta);
        }
    }

    public void ClearFearEffect()
    {
        hasFearEffect = false;
    }

    public void ApplyBurnEffect(int damage, int turns)
    {
        burnDamagePerTurn = damage;
        burnTurnsRemaining = turns;
        CombatLog.Info($"<color=orange>Steve is BURNING! ({damage} DMG per turn for {turns} turns)</color>");

        if (Application.isPlaying)
        {
            GameObject go = new GameObject("BurnText");
            go.transform.position = transform.position + Vector3.up * 2.8f;
            var ft = go.AddComponent<FloatingText>();
            ft.Setup("BURN!", new Color(1f, 0.5f, 0f));
        }
    }

    public void ClearBurnEffect()
    {
        burnTurnsRemaining = 0;
    }

    public void ApplyCurseEffect(float reductionPercent, int turns)
    {
        curseReductionPercent = reductionPercent;
        curseTurnsRemaining = turns;
        CombatLog.Info($"<color=purple>Steve is CURSED! (Only ONE die per roll for {turns} turns)</color>");

        if (Application.isPlaying)
        {
            GameObject go = new GameObject("CurseText");
            go.transform.position = transform.position + Vector3.up * 2.8f;
            var ft = go.AddComponent<FloatingText>();
            ft.Setup("CURSE!", new Color(0.5f, 0f, 0.5f));
        }
    }

    public void ClearCurseEffect()
    {
        curseTurnsRemaining = 0;
    }

    public void ApplyPoisonEffect(int damage, int turns)
    {
        poisonDamagePerTurn = damage;
        poisonTurnsRemaining = turns;
        CombatLog.Info($"<color=green>Steve is POISONED! ({damage} DMG per turn for {turns} turns)</color>");

        if (Application.isPlaying)
        {
            GameObject go = new GameObject("PoisonText");
            go.transform.position = transform.position + Vector3.up * 2.8f;
            var ft = go.AddComponent<FloatingText>();
            ft.Setup("POISON!", Color.green);
        }
    }

    public void ClearPoisonEffect()
    {
        poisonTurnsRemaining = 0;
    }

    public void TickPoisonEffect()
    {
        if (poisonTurnsRemaining <= 0) return;

        CombatLog.Info($"<color=green>Poison ticks for {poisonDamagePerTurn} damage.</color>");
        TakeDamage(poisonDamagePerTurn, "Poison", false, true);
        poisonTurnsRemaining--;
        
        if (poisonTurnsRemaining <= 0)
        {
            CombatLog.Info("<color=green>Poison effect wore off.</color>");
        }
    }

    public void ApplyKnockback(Vector3 fromPosition, float meters)
{
        if (isDead) return;

        ExitCombat();
        
        Vector3 dir = (transform.position - fromPosition).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = -transform.forward;
        dir.y = 0f;
        dir.Normalize();

        Vector3 targetPos = transform.position + dir * meters;
        
        // Find valid point on NavMesh
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, meters * 0.5f, NavMesh.AllAreas))
        {
            targetPos = hit.position;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.Warp(targetPos);
        }
        else
        {
            transform.position = targetPos;
        }

        CombatLog.Info($"<color=brown>Steve is KNOCKED BACK {meters:F1}m!</color>");

        if (Application.isPlaying)
        {
            GameObject go = new GameObject("KnockbackText");
            go.transform.position = transform.position + Vector3.up * 2.8f;
            var ft = go.AddComponent<FloatingText>();
            ft.Setup("KNOCKBACK!", new Color(0.6f, 0.4f, 0.2f));
        }
    }

    public void TickCurseEffect()
{
        if (curseTurnsRemaining <= 0) return;

        curseTurnsRemaining--;
        if (curseTurnsRemaining <= 0)
        {
            CombatLog.Info("<color=purple>Curse effect wore off.</color>");
        }
    }

    public void TickBurnEffect()
{
        if (burnTurnsRemaining <= 0) return;

        CombatLog.Info($"<color=orange>Burn ticks for {burnDamagePerTurn} damage.</color>");
        TakeDamage(burnDamagePerTurn, "Burn", false, true);
        burnTurnsRemaining--;
        
        if (burnTurnsRemaining <= 0)
        {
            CombatLog.Info("<color=orange>Burn effect wore off.</color>");
        }
    }

    public void PlayLevelUpCelebration()
{
        if (stats != null)
        {
            stats.RestoreFullHealth();
            UpdateHealthUI();
        }

        EnergyManager.Instance?.RestoreFull();

        // If we are already celebrating or showing rewards, don't restart the routine.
        // The existing routine will handle any newly enqueued levels in the RogueLiteManager queue.
        if (isCelebrating || (RogueLiteManager.HasInstance && RogueLiteManager.Instance.IsRewardFlowActive))
            return;

        if (InCombat)
        {
            // If in combat, skip animation but show popup.
            if (celebrationRoutine != null)
                StopCoroutine(celebrationRoutine);
            celebrationRoutine = StartCoroutine(LevelUpCombatRewardRoutine());
            return;
        }

        // Only play level animation when leveling while moving. Never during combat.
        if (IsMoving)
        {
            isCelebrating = true;
            movement?.Stop();
            steveAnim?.PlayLevelUp();

            if (celebrationRoutine != null)
                StopCoroutine(celebrationRoutine);
            celebrationRoutine = StartCoroutine(LevelUpCelebrationRoutine());
        }
        else
        {
            // Not in combat, but not moving either (e.g. Idle)
            // Still show rewards, just skip celebration animation as per "only while moving" requirement.
            if (celebrationRoutine != null)
                StopCoroutine(celebrationRoutine);
            celebrationRoutine = StartCoroutine(LevelUpCombatRewardRoutine());
        }
    }

    private IEnumerator LevelUpCombatRewardRoutine()
    {
        if (RogueLiteManager.Instance != null)
            yield return RogueLiteManager.Instance.RunPostCelebrationRewards();
        celebrationRoutine = null;
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
        return target.GetComponent<Enemy>()
               ?? target.GetComponentInChildren<Enemy>()
               ?? target.GetComponentInParent<Enemy>();
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
