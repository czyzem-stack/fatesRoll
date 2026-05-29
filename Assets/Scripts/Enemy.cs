using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Enemy : MonoBehaviour
{
#if UNITY_EDITOR
    private void OnValidate()
    {
        CalculateDerivedStats();
        // Don't call UpdateHealthUI here as it might try to access UI elements 
        // that aren't properly initialized in the prefab/editor state.
        // But we want derived stats to show in the inspector.
    }
#endif

    [Header("Enemy Stats")]
    public float strength = 8f;
    public float agility = 8f;
    public float vitality = 8f;
    public float luck = 5f;
    
    [Header("Current State")]
    public float currentHP;
    public bool isDead = false;
    public bool isAttacking = false;
    private bool isRunningAway = false;
    private int battleShoutTurnsRemaining = 0;
private float battleShoutMultiplier = 1.0f;
    private int regenTurnsRemaining = 0;
    private float regenPercentPerTurn = 0f;
    private Color regenTextColor = Color.green;
    private int hardenedTurnsRemaining = 0;
    private float hardenedReductionPercent = 0f;

    public bool IsRegenerating => regenTurnsRemaining > 0;
    public bool IsHardened => hardenedTurnsRemaining > 0;

    [Header("UI Smoothing")]
    public float healthLerpSpeed = 5f;

    [Header("Derived Stats (Read-Only)")]
    public float maxHP;
    public float attackDamage;
    public float attackSpeed;
    public float critChance;
    public float critDamage;
    public float dodgeChance;

    [Header("Patrol & AI Settings")]
    public float patrolRadius = 5.0f;
    public float patrolSpeed = 1.5f;
    public int avoidancePriority = 50;
    public int patrolPointsBeforeTaunt = 5;

    private int currentPatrolPoints = 0;
    private Vector3 spawnPosition;
    private NavMeshAgent agent;
    private Animator animator;
    private HeroController cachedHero;
    private Slider healthSlider;
    private Canvas healthCanvas;
    private UnityEngine.UI.Image healthFillImage;
    private UnityEngine.UI.Image healthBgImage;
    private UnityEngine.UI.Image healthBorderImage;
    private bool healthBarVisualsCached;
    private float lastDisplayedHP = -1f;
    private float lastDisplayedMaxHP = -1f;
    private float nextPatrolTime;
    private bool navHeld;
    private bool combatNavLocked;
    private bool visualYawFixApplied;
    private MonsterLocomotionDriver locomotionDriver;

    private Camera mainCamera;

    public bool IsTreasureChest
    {
        get
        {
            var poi = GetComponent<POINode>() ?? GetComponentInParent<POINode>();
            return poi != null && poi.IsTreasureChest;
        }
    }

    private void Start()
    {
        Initialize();
        cachedHero = GameServices.Hero;
        if (IsTreasureChest)
            ConfigureAsTreasureChest();
        SetHealthBarVisible(false);
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (isDead) return;

        if (!Application.isPlaying) return;

        if (IsTreasureChest) return;

        HandleAI();
        UpdateAnimation();
        UpdateHealthUI();
    }

    private void LateUpdate()
    {
        if (isDead || !Application.isPlaying) return;

        if (combatNavLocked && cachedHero != null)
            FaceTarget(cachedHero.transform, false, 12f);

        // Stabilize and billboard the health bar
        if (healthCanvas != null && healthCanvas.enabled)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                // 1. Force position to be strictly above the enemy, ignoring parent rotation
                // This stops the "shaking" caused by the enemy's Y-axis jittering
                healthCanvas.transform.position = transform.position + Vector3.up * 3.0f;

                // 2. Snap rotation to face the camera
                healthCanvas.transform.rotation = mainCamera.transform.rotation;
            }
        }
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        bool isProtected = locomotionDriver != null && locomotionDriver.IsInProtectedAnimation;

        if (locomotionDriver != null && locomotionDriver.UsesStatePlay)
        {
            bool inCombat = cachedHero != null && IsFightingHero(cachedHero);
            float agentSpeed = 0f;
            if (isRunningAway || (!navHeld && !isAttacking && !inCombat && !isProtected && IsLocomoting()))
                agentSpeed = agent.velocity.magnitude;

            locomotionDriver.UpdateLocomotion(agentSpeed, inCombat, isAttacking);
            return;
        }

        if (!isRunningAway && (navHeld || isAttacking || isProtected || (cachedHero != null && IsFightingHero(cachedHero))))
        {
            HeroAnimatorParams.SetFloatSafe(animator, HeroAnimatorParams.Speed, 0f, 0.15f, Time.deltaTime);
            return;
        }

        float speed = 0f;
        if (IsLocomoting())
        {
            speed = agent.velocity.magnitude;
            if (speed < 0.35f) speed = 0f;
            else if (speed < 1.2f) speed = 1.0f;
            else if (speed > 3.5f) speed = 2f;
        }

        HeroAnimatorParams.SetFloatSafe(animator, HeroAnimatorParams.Speed, speed, 0.2f, Time.deltaTime);
    }

    public void Initialize()
    {
        battleShoutTurnsRemaining = 0;
        battleShoutMultiplier = 1.0f;
        regenTurnsRemaining = 0;
        regenPercentPerTurn = 0f;
        hardenedTurnsRemaining = 0;
        hardenedReductionPercent = 0f;

        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            EnsureGameplayAnimator();
        }

        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        
        agent.speed = patrolSpeed;
        agent.acceleration = 12f;
        agent.angularSpeed = 360f;
        agent.stoppingDistance = 0.35f;
        agent.autoBraking = true;
        agent.avoidancePriority = avoidancePriority;
        agent.updateRotation = true;

        HeroLocomotionUtility.ApplyVisualYawFix(transform, animator, ref visualYawFixApplied);
        CalculateDerivedStats();
        currentHP = maxHP;
        spawnPosition = transform.position;

        UpdateHealthUI();
    }

    public POIType MonsterType => ResolveMonsterType();

    private POIType ResolveMonsterType()
    {
        var poi = GetComponent<POINode>() ?? GetComponentInParent<POINode>();
        if (poi != null)
        {
            if (poi.type == POIType.MonsterPlant) GlobalSettings.LogGameplay($"[Enemy] {gameObject.name} identified as MonsterPlant.");
            return poi.type;
        }

        var spawnNode = GetComponentInParent<SpawnNode>();
        if (spawnNode != null && spawnNode.hasSpawnedType)
        {
            if (spawnNode.lastSpawnedType == POIType.MonsterPlant) GlobalSettings.LogGameplay($"[Enemy] {gameObject.name} (spawned) identified as MonsterPlant.");
            return spawnNode.lastSpawnedType;
        }

        return POIType.Orc;
    }

    private void EnsureGameplayAnimator()
    {
        if (IsTreasureChest || animator == null)
            return;

        POIType type = ResolveMonsterType();
        var catalog = MonsterAnimatorUtility.ResolveCatalog();

        MonsterAnimatorUtility.ApplyToVisual(animator.gameObject, type, catalog);
        animator = GetComponentInChildren<Animator>();

        locomotionDriver = GetComponent<MonsterLocomotionDriver>();
        if (locomotionDriver == null)
            locomotionDriver = gameObject.AddComponent<MonsterLocomotionDriver>();
        locomotionDriver.Bind(type, animator);
    }

    /// <summary>Full heal and base difficulty after Steve dies (visit POI or stray enemy).</summary>
    public void ReviveForRunReset(POINode poiNode = null)
    {
        if (IsTreasureChest) return;

        isDead = false;
        isAttacking = false;
        navHeld = false;
        combatNavLocked = false;
        currentPatrolPoints = 0;
        nextPatrolTime = Time.time + 1f;

        POINode poi = poiNode ?? GetComponent<POINode>() ?? GetComponentInParent<POINode>();
        if (poi != null)
            gameObject.tag = "POI";
        if (poi != null && poi.enemyData != null)
            InitializeFromData(poi.enemyData);
        else if (poi != null && EnemyStatManager.Instance != null)
            EnemyStatManager.Instance.ApplyFtueStepStats(this, poi.order);
        else if (EnemyStatManager.Instance != null)
            EnemyStatManager.Instance.ApplyScaledStats(this);
        else
            Initialize();

        currentHP = maxHP;
        SetHealthBarVisible(true);
        UpdateHealthUI();
        UnlockCombatNavigation();
        cachedHero = GameServices.Hero;
    }

    /// <summary>Re-enable a pooled POI enemy after the POI was deactivated.</summary>
    public void ResetForSpawn()
    {
        isDead = false;
        isAttacking = false;
        navHeld = false;
        combatNavLocked = false;
        currentPatrolPoints = 0;
        nextPatrolTime = Time.time + 1f;

        Initialize();
        SetHealthBarVisible(true);
        cachedHero = GameServices.Hero;
    }

    public void InitializeFromData(EnemyData data)
    {
        if (data == null) return;
        
        this.name = data.enemyName;
        this.strength = data.strength;
        this.agility = data.agility;
        this.vitality = data.vitality;
        this.luck = data.luck;

        Initialize();
    }

    private void CalculateDerivedStats()
    {
        // Formulas match PlayerStats.CalculateAllDerivedStats — keep in sync when changing.
        maxHP = vitality * 10f + 100f;
        attackDamage = strength * 4f + 20f;
        attackSpeed = 1.0f + (agility * 0.03f);
        critChance = luck * 0.8f;
        critDamage = 50f + (luck * 1.5f);
        dodgeChance = agility * 0.6f;

        if (currentHP > maxHP) currentHP = maxHP;
    }

    /// <summary>Steve is inside this enemy's patrol territory (spawn-centered).</summary>
    public bool IsHeroInPatrolZone(HeroController hero)
    {
        if (hero == null) return false;
        Vector3 a = spawnPosition;
        Vector3 b = hero.transform.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b) <= patrolRadius;
    }

    /// <summary>Steve is close enough for melee (tighter than patrol aggro zone).</summary>
    public bool IsWithinEngageRange(HeroController hero)
    {
        if (hero == null) return false;
        float limit = GlobalSettings.GetMeleeEngageDistance();
        if (IsFightingHero(hero))
            limit += 0.35f;

        return HorizontalDistanceTo(hero.transform.position) <= limit;
    }

    /// <summary>Nav destination for Steve to stand toe-to-toe with this enemy.</summary>
    public Vector3 GetMeleeApproachPoint(Vector3 heroPosition)
    {
        float engage = GlobalSettings.GetMeleeEngageDistance();
        Vector3 toHero = heroPosition - transform.position;
        toHero.y = 0f;
        if (toHero.sqrMagnitude < 0.01f)
            toHero = Vector3.forward;
        toHero.Normalize();

        Vector3 point = transform.position + toHero * engage;
        if (UnityEngine.AI.NavMesh.SamplePosition(point, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            return hit.position;
        return point;
    }

    public bool IsFightingHero(HeroController hero)
    {
        if (hero == null || !hero.InCombat) return false;
        Enemy target = hero.GetCurrentEnemy();
        return target != null && target == this;
    }

    public Vector3 GetEngagePosition()
    {
        return transform.position;
    }

    /// <summary>World position used for chest pathing and open range (visual when present).</summary>
    public Vector3 GetChestInteractPosition()
    {
        var poi = GetComponent<POINode>() ?? GetComponentInParent<POINode>();
        if (poi != null && poi.currentVisual != null)
            return poi.currentVisual.transform.position;
        return transform.position;
    }

    private float HorizontalDistanceTo(Vector3 worldPos)
    {
        Vector3 a = transform.position;
        Vector3 b = worldPos;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void HoldPosition()
    {
        if (agent == null || !agent.enabled || combatNavLocked) return;
        if (navHeld) return;
        navHeld = true;
        agent.isStopped = true;
        agent.updateRotation = false;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    private void ReleaseNavHold()
    {
        navHeld = false;
    }

    private void BeginLocomotion(float speed)
    {
        if (agent == null || !agent.enabled || combatNavLocked) return;
        ReleaseNavHold();
        agent.isStopped = false;
        agent.updateRotation = true;
        agent.speed = speed;
    }

    private bool IsLocomoting()
    {
        return agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped && !navHeld && !isDead &&
               !combatNavLocked;
    }

    private void LockCombatNavigation()
    {
        if (combatNavLocked || agent == null || !agent.enabled) return;
        combatNavLocked = true;
        navHeld = true;
        agent.isStopped = true;
        agent.updateRotation = false;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    private void UnlockCombatNavigation()
    {
        if (!combatNavLocked) return;
        combatNavLocked = false;
        navHeld = false;
        if (agent != null && agent.enabled)
            agent.updateRotation = true;
    }

    private void SnapFaceTarget(Transform target)
    {
        if (target == null) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private void HoldEngaged()
    {
        LockCombatNavigation();
    }

    private bool IsSteveApproachingUs()
    {
        return cachedHero != null && cachedHero.ApproachingEnemy == this;
    }

    private void HandleAI()
    {
        if (cachedHero == null) cachedHero = GameServices.Hero;
        if (cachedHero == null) return;

        bool isEngaged = IsFightingHero(cachedHero);

        HeroAnimatorParams.SetBoolSafe(animator, "InCombat", isEngaged);

        if (isEngaged)
        {
            HoldEngaged();
            return;
        }

        UnlockCombatNavigation();

        bool isProtected = locomotionDriver != null && locomotionDriver.IsInProtectedAnimation;

        // Steve walks in or is in melee range — stand still. Never chase; never walk off when he arrives.
        if (!isRunningAway && (cachedHero.IsEngageBusy || isAttacking || isProtected ||
            IsSteveApproachingUs() || IsWithinEngageRange(cachedHero) ||
            (cachedHero.IsMoving && IsHeroInPatrolZone(cachedHero))))
        {
            HoldPosition();
            return;
        }

        if (navHeld)
            ReleaseNavHold();

        if (!isAttacking)
            HandlePatrol();
    }

    private void HandlePatrol()
    {
        if (agent == null || !agent.enabled) return;

        if (!agent.pathPending && agent.remainingDistance < agent.stoppingDistance + 0.1f)
        {
            if (Time.time >= nextPatrolTime)
            {
                currentPatrolPoints++;
                if (currentPatrolPoints >= patrolPointsBeforeTaunt)
                {
                    currentPatrolPoints = 0;
                    if (CanPlayTaunt())
                        StartCoroutine(TauntRoutine());
                    else
                        nextPatrolTime = Time.time + Random.Range(1f, 3f);
                    return;
                }

                Vector3 randomPoint = spawnPosition + Random.insideUnitSphere * patrolRadius;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomPoint, out hit, patrolRadius, NavMesh.AllAreas))
                {
                    BeginLocomotion(patrolSpeed);
                    agent.SetDestination(hit.position);
                    nextPatrolTime = Time.time + Random.Range(3f, 7f);
                }
            }
        }
    }

    private bool CanPlayTaunt()
    {
        if (locomotionDriver != null && locomotionDriver.UsesStatePlay)
            return locomotionDriver.HasTauntState;
        return animator != null;
    }

    private System.Collections.IEnumerator TauntRoutine()
    {
        isAttacking = true;
        LockCombatNavigation();

        if (locomotionDriver != null)
            locomotionDriver.PlayTaunt();
        else
            HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Taunt);

        // Wait for taunt animation to finish (approx 2s)
        yield return new WaitForSeconds(2.2f);

        isAttacking = false;
        nextPatrolTime = Time.time + Random.Range(1f, 3f);
    }

    public void FaceTarget(Transform target, bool instant = false, float speed = 15.0f)
    {
        if (target == null) return;
        if (instant)
        {
            SnapFaceTarget(target);
            return;
        }

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.1f) return; // Dead-zone for stability

        Quaternion want = Quaternion.LookRotation(dir.normalized);
        if (Quaternion.Angle(transform.rotation, want) > 5f) // Don't rotate for small angles
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, speed * 10f * Time.deltaTime);
        }
    }

    public void SetHealthBarVisible(bool visible)
    {
        if (healthCanvas != null) healthCanvas.enabled = visible;
        else if (healthSlider != null) healthSlider.gameObject.SetActive(visible);
    }

    private void EnsureHealthBar()
    {
        if (healthCanvas == null) healthCanvas = GetComponentInChildren<Canvas>(true);
        if (healthSlider == null) healthSlider = GetComponentInChildren<Slider>(true);

        if (healthSlider != null)
            CacheHealthBarVisuals();
    }

    private void CacheHealthBarVisuals()
    {
        if (healthSlider == null || healthBarVisualsCached)
            return;

        healthBgImage = healthSlider.transform.Find("Bg")?.GetComponent<UnityEngine.UI.Image>();
        if (healthBgImage == null)
            healthBgImage = healthSlider.transform.Find("Background")?.GetComponent<UnityEngine.UI.Image>();
        healthFillImage = healthSlider.fillRect?.GetComponent<UnityEngine.UI.Image>();
        healthBorderImage = healthSlider.transform.Find("Border")?.GetComponent<UnityEngine.UI.Image>();
        healthBarVisualsCached = true;

        if (healthBgImage != null)
            healthBgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);
        if (healthBorderImage != null)
            healthBorderImage.color = Color.white;
    }

    private void UpdateHealthUI()
    {
        if (healthSlider == null || healthCanvas == null)
            EnsureHealthBar();

        if (healthSlider == null)
            return;

        if (healthSlider.maxValue != maxHP)
            healthSlider.maxValue = maxHP;

        bool needsUpdate = Mathf.Abs(healthSlider.value - currentHP) > 0.01f ||
                          Mathf.Abs(maxHP - lastDisplayedMaxHP) > 0.01f;

        if (!needsUpdate)
            return;

        if (Application.isPlaying)
        {
            healthSlider.value = Mathf.Lerp(healthSlider.value, currentHP, Time.deltaTime * healthLerpSpeed);
            if (Mathf.Abs(healthSlider.value - currentHP) < 0.01f)
                healthSlider.value = currentHP;
        }
        else
        {
            healthSlider.value = currentHP;
        }

        lastDisplayedHP = healthSlider.value;
        lastDisplayedMaxHP = maxHP;

        if (!healthBarVisualsCached)
            CacheHealthBarVisuals();

        if (healthFillImage != null && maxHP > 0f)
        {
            float hpPercent = Mathf.Clamp01(healthSlider.value / maxHP);
            Color c = Color.Lerp(Color.red, Color.green, hpPercent);
            c.a = 1.0f;
            healthFillImage.color = c;
        }
    }

    public void ConfigureAsTreasureChest()
    {
        strength = 1f;
        agility = 1f;
        vitality = 9999f;
        luck = 1f;
        patrolRadius = 0f;
        patrolSpeed = 0f;
        attackDamage = 0f;
        CalculateDerivedStats();
        currentHP = maxHP;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = 0f;
            agent.angularSpeed = 0f;
        }

        SetHealthBarVisible(false);
    }

    /// <summary>Steve reached the chest — open immediately (no combat).</summary>
    public void OpenTreasureChest()
    {
        if (isDead || !IsTreasureChest) return;

        var poi = GetComponent<POINode>() ?? GetComponentInParent<POINode>();
        if (poi == null) return;

        isDead = true;
        currentHP = 0;
        isAttacking = false;
        UnlockCombatNavigation();
        gameObject.tag = "Untagged";
        SetHealthBarVisible(false);

        if (EquipmentLootManager.Instance != null)
            EquipmentLootManager.Instance.EnqueueChestReward(poi);

        if (cachedHero == null)
            cachedHero = GameServices.Hero;

        if (cachedHero != null)
        {
            cachedHero.ExitCombat();
            if (poi.GetComponentInParent<SpawnNode>() == null)
                cachedHero.OnPOIDefeated(poi);
            if (EquipmentLootManager.Instance != null && EquipmentLootManager.Instance.HasPendingChestRewards)
                cachedHero.PlayChestRewardCelebration(instantOpen: true);
        }

        POIResolve.Resolve(gameObject);
    }

    public void ApplyRegenEffect(float percent, int turns, Color textColor)
    {
        regenPercentPerTurn = percent;
        regenTurnsRemaining = turns;
        regenTextColor = textColor;
    }

    public void TickRegenEffect()
    {
        if (regenTurnsRemaining <= 0 || isDead) return;

        float amount = maxHP * (regenPercentPerTurn / 100f);
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        UpdateHealthUI();

        if (Application.isPlaying)
        {
            GameObject go = new GameObject("HealText");
            go.transform.position = transform.position + Vector3.up * 3.2f;
            var ft = go.AddComponent<FloatingText>();
            ft.Setup($"+{amount:F0}", regenTextColor);
        }

        regenTurnsRemaining--;
        if (regenTurnsRemaining <= 0)
        {
            CombatLog.Info($"{gameObject.name} REGEN finished.");
        }
    }

    public void ApplyHardenedEffect(float percent, int turns)
    {
        hardenedReductionPercent = percent;
        hardenedTurnsRemaining = turns;
    }

    public void TickHardenedEffect()
    {
        if (hardenedTurnsRemaining <= 0 || isDead) return;

        hardenedTurnsRemaining--;
        if (hardenedTurnsRemaining <= 0)
        {
            CombatLog.Info($"{gameObject.name} HARDENED effect wore off.");
        }
    }

    public bool TakeDamage(float amount, string attackerName = "Steve")
{
        if (IsTreasureChest)
            return false;

        if (isDead || currentHP <= 0) return false;

        // Skeleton unique ability: block chance from EnemySpecialController
        if (ResolveMonsterType() == POIType.Skeleton)
        {
            float blockChance = 50f;
            if (GameServices.TryGet(out EnemySpecialController esc))
            {
                blockChance = esc.GetEffectValue(POIType.Skeleton, 50f);

                float blockRoll = Random.Range(0f, 100f);
                if (blockRoll < blockChance)
                {
                    CombatLog.Info($"{gameObject.name} BLOCKED the attack!");
                    if (locomotionDriver != null) locomotionDriver.PlayDefense();
                    else HeroAnimatorParams.SetTriggerSafe(animator, "Defense");

                    if (Application.isPlaying)
                    {
                        string txt = "BLOCKED";
                        Color col = Color.white;
                        if (GameServices.TryGet(out EnemySpecialController escTxt))
                        {
                            txt = escTxt.GetFloatingText(POIType.Skeleton, txt);
                            col = escTxt.GetTextColor(POIType.Skeleton, col);
                        }

                    GameObject blockGo = new GameObject("BlockText");
                    blockGo.transform.position = transform.position + Vector3.up * 2.8f;
                    blockGo.AddComponent<FloatingText>().Setup(txt, col);
                }
                return false;
            }
        }
    }

        float dodgeRoll = Random.Range(0f, 100f);
        if (dodgeRoll < dodgeChance)
        {
            CombatLog.Dodge(gameObject.name, dodgeChance, dodgeRoll);
            CombatLog.DamageMitigated(attackerName, gameObject.name, "dodged");
            return false;
        }

        float hpBefore = currentHP;
        SetHealthBarVisible(true);

        if (IsHardened)
        {
            float reduction = amount * (hardenedReductionPercent / 100f);
            CombatLog.Info($"{gameObject.name} reduced damage by {reduction:F0} (HARDENED)!");
            amount -= reduction;
        }

        currentHP -= amount;
        UpdateHealthUI();
CombatLog.DamageDealt(attackerName, gameObject.name, amount, currentHP);
        
        if (Application.isPlaying)
        {
            GameObject go = new GameObject("DamageText");
            go.transform.position = transform.position + Vector3.up * 2.8f;
            var ft = go.AddComponent<FloatingText>();
            ft.Setup($"-{amount:F0}", Color.yellow);
        }

        if (currentHP <= 0)
        {
            currentHP = 0;
            CombatLog.Death(gameObject.name);
            Die();
        }
        else if (locomotionDriver != null)
            locomotionDriver.PlayGetHit();
        else
            HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.GetHit);

        return true;
    }

    private void Die()
    {
        if (isDead) return;

        if (IsTreasureChest)
        {
            OpenTreasureChest();
            return;
        }

        isDead = true;
        UnlockCombatNavigation();

        if (locomotionDriver != null)
            locomotionDriver.PlayDie();
        else
        {
            HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Die);
            HeroAnimatorParams.SetBoolSafe(animator, "IsDead", true);
        }

        HeroAnimatorParams.SetBoolSafe(animator, "InCombat", false);

        if (LootManager.Instance != null)
            LootManager.Instance.OnEnemyDied(this);

        if (QuestManager.Instance != null)
            QuestManager.Instance.NotifyEnemyKilled(ResolveMonsterType());

        gameObject.tag = "Untagged";
        if (healthSlider != null) healthSlider.gameObject.SetActive(false);

        if (cachedHero != null)
        {
            // Ensure hero state exits combat even if called from mid-roll or movement desync paths.
            if (cachedHero.currentEnemy == gameObject)
                cachedHero.currentEnemy = null;
            cachedHero.ExitCombat();
            cachedHero.OnPOIDefeated(GetComponentInParent<POINode>());
        }

        CombatLog.Info($"[State] Enemy {gameObject.name} -> Dead (combat ended, POI resolve in 2s)");
        StartCoroutine(DelayedResolvePoi());
    }

    private void OnDestroy()
    {
        if (cachedHero != null && cachedHero.InCombat && cachedHero.currentEnemy == gameObject)
        {
            cachedHero.ExitCombat();
        }
    }

    private System.Collections.IEnumerator DelayedResolvePoi()
    {
        yield return new WaitForSeconds(2.0f);
        POIResolve.Resolve(gameObject);
    }

    public void PerformAttack(HeroController hero)
    {
        if (IsTreasureChest) return;
        if (isDead || currentHP <= 0) return;
        if (isAttacking) return;
        if (hero == null || !hero.InCombat) return; // guard against state desync mid-sequence
        isAttacking = true;
        FaceTarget(hero.transform, false, 20f);
        StartCoroutine(AttackRoutine(hero));
    }

    public float AttackDurationSeconds
    {
        get
        {
            var s = GlobalSettings.Instance;
            if (s == null) return 0.45f;
            return s.enemyAttackWindUp + s.enemyAttackHitDelay + 0.05f;
        }
    }

    private System.Collections.IEnumerator AttackRoutine(HeroController hero)
    {
        var settings = GlobalSettings.Instance;
        POIType type = ResolveMonsterType();

        TickRegenEffect();
        TickHardenedEffect();

        // Orc special: Battle Shout (increase damage multiplier)
        if (type == POIType.Orc && battleShoutTurnsRemaining <= 0)
        {
            float shoutChance = 25f;
            float multiplier = 1.5f;
            int turns = 3;
            float duration = 2.0f;
            if (GameServices.TryGet(out EnemySpecialController esc))
            {
                shoutChance = esc.GetSpecialChance(POIType.Orc, 25f);
                var s = esc.GetSettings(POIType.Orc);
                multiplier = s != null ? s.effectValue : 1.5f;
                turns = s != null ? s.buffTurns : 3;
                duration = s != null ? s.effectDuration : 2.0f;
            }

            float shoutRoll = UnityEngine.Random.Range(0f, 100f);
            if (shoutRoll < shoutChance)
            {
                CombatLog.Info($"{gameObject.name} uses <color=orange>BATTLE SHOUT</color>! (+{((multiplier - 1) * 100):F0}% DMG for {turns} turns)");
                battleShoutMultiplier = multiplier;
                battleShoutTurnsRemaining = turns;

                if (Application.isPlaying)
                {
                    string txt = "BATTLE SHOUT!";
                    Color col = Color.yellow;
                    if (GameServices.TryGet(out EnemySpecialController escText))
                    {
                        txt = escText.GetFloatingText(POIType.Orc, txt);
                        col = escText.GetTextColor(POIType.Orc, col);
                    }

                    GameObject shoutGo = new GameObject("ShoutText");
                    shoutGo.transform.position = transform.position + Vector3.up * 2.8f;
                    shoutGo.AddComponent<FloatingText>().Setup(txt, col);
                }

                if (locomotionDriver != null)
                    locomotionDriver.PlayTaunt();
                else
                    HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Taunt);

                yield return new WaitForSeconds(duration);
                isAttacking = false;
                yield break;
            }
        }

        // Bat special: chance to cast Fear (Taunt) from EnemySpecialController
        if (type == POIType.Bat && !hero.HasFearEffect)
        {
            float castChance = 25f;
            float duration = 2.0f;
            if (GameServices.TryGet(out EnemySpecialController esc))
            {
                castChance = esc.GetSpecialChance(POIType.Bat, 25f);
                duration = esc.GetSettings(POIType.Bat)?.effectDuration ?? 2.0f;
            }

            float fearRoll = Random.Range(0f, 100f);
            if (fearRoll < castChance)
            {
                CombatLog.Info($"{gameObject.name} casts <color=purple>FEAR</color>!");
                isAttacking = true;
                if (locomotionDriver != null)
                    locomotionDriver.PlayTaunt();
                else
                    HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Taunt);

                hero.ApplyFearEffect();
                
                if (Application.isPlaying)
                {
                    string txt = "FEAR!";
                    Color col = Color.magenta;
                    if (GameServices.TryGet(out EnemySpecialController escTxt))
                    {
                        txt = escTxt.GetFloatingText(POIType.Bat, txt);
                        col = escTxt.GetTextColor(POIType.Bat, col);
                    }

                    GameObject fearGo = new GameObject("FearText");
                    fearGo.transform.position = transform.position + Vector3.up * 2.8f;
                    fearGo.AddComponent<FloatingText>().Setup(txt, col);
                }

                // Taunt usually takes longer
                yield return new WaitForSeconds(duration);
                isAttacking = false;
                yield break;
            }
        }

        // Dragon special: BURN
        if (type == POIType.Dragon)
        {
            float burnChance = 10f;
            int damage = 10;
            int turns = 3;
            float duration = 2.0f;

            if (GameServices.TryGet(out EnemySpecialController esc))
            {
                burnChance = esc.GetSpecialChance(POIType.Dragon, 10f);
                var s = esc.GetSettings(POIType.Dragon);
                damage = s != null ? (int)s.effectValue : 10;
                turns = s != null ? s.buffTurns : 3;
                duration = s != null ? s.effectDuration : 2.0f;
            }

            float burnRoll = Random.Range(0f, 100f);
            if (burnRoll < burnChance)
            {
                CombatLog.Info($"{gameObject.name} uses <color=orange>BURN</color>!");
                isAttacking = true;
                
                if (locomotionDriver != null)
                {
                    locomotionDriver.PlayAttack(1); // 1 = Attack02 (usually breath)
                }
                else
                {
                    if (HeroAnimatorParams.HasParameter(animator, "AttackIndex"))
                        animator.SetInteger("AttackIndex", 1);
                    HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Attack);
                }

                hero.ApplyBurnEffect(damage, turns);

                if (Application.isPlaying)
                {
                    string txt = "BURN!";
                    Color col = new Color(1f, 0.5f, 0f);
                    if (GameServices.TryGet(out EnemySpecialController escTxt))
                    {
                        txt = escTxt.GetFloatingText(POIType.Dragon, txt);
                        col = escTxt.GetTextColor(POIType.Dragon, col);
                    }

                    // Show above STEVE since he is the one burning
                    GameObject burnGo = new GameObject("BurnText");
                    burnGo.transform.position = hero.transform.position + Vector3.up * 2.8f;
                    burnGo.AddComponent<FloatingText>().Setup(txt, col);
                }

                // Deal initial damage so Steve flinches/reacts
                yield return new WaitForSeconds(0.25f);
                if (!isDead && currentHP > 0 && hero != null && hero.InCombat)
                {
                    float initDmg = CombatLog.RollAndApplyCrit(attackDamage, critChance, critDamage, out bool isCrit);
                    hero.TakeDamage((int)initDmg, gameObject.name, isCrit);
                }

                yield return new WaitForSeconds(Mathf.Max(0, duration - 0.25f));
                isAttacking = false;
                yield break;
            }
        }

        // EvilMage special: CURSE
        if (type == POIType.EvilMage)
        {
            float curseChance = 25f;
            float reductionPercent = 25f;
            int turns = 3;
            float duration = 2.0f;

            if (GameServices.TryGet(out EnemySpecialController esc))
            {
                curseChance = esc.GetSpecialChance(POIType.EvilMage, 25f);
                var s = esc.GetSettings(POIType.EvilMage);
                reductionPercent = s != null ? s.effectValue : 25f;
                turns = s != null ? s.buffTurns : 3;
                duration = s != null ? s.effectDuration : 2.0f;
            }

            float curseRoll = Random.Range(0f, 100f);
            if (curseRoll < curseChance)
            {
                CombatLog.Info($"{gameObject.name} casts CURSE! (Turns: {turns})");
                isAttacking = true;

                if (locomotionDriver != null)
                {
                    locomotionDriver.PlayAttack(1); // Use Attack02 for casting
                }
                else
                {
                    if (HeroAnimatorParams.HasParameter(animator, "AttackIndex"))
                        animator.SetInteger("AttackIndex", 1);
                    HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Attack);
                }

                hero.ApplyCurseEffect(reductionPercent, turns);

                if (Application.isPlaying)
                {
                    string txt = "CURSE!";
                    Color col = new Color(0.5f, 0f, 0.5f);
                    if (GameServices.TryGet(out EnemySpecialController escTxt))
                    {
                        txt = escTxt.GetFloatingText(POIType.EvilMage, txt);
                        col = escTxt.GetTextColor(POIType.EvilMage, col);
                    }

                    GameObject curseGo = new GameObject("CurseText");
                    curseGo.transform.position = transform.position + Vector3.up * 2.8f;
                    curseGo.AddComponent<FloatingText>().Setup(txt, col);
                }

                // Deal initial damage so Steve flinches/reacts
                yield return new WaitForSeconds(0.25f);
                if (!isDead && currentHP > 0 && hero != null && hero.InCombat)
                {
                    float initDmg = CombatLog.RollAndApplyCrit(attackDamage, critChance, critDamage, out bool isCrit);
                    hero.TakeDamage((int)initDmg, gameObject.name, isCrit);
                }

                yield return new WaitForSeconds(Mathf.Max(0, duration - 0.25f));
                isAttacking = false;
                yield break;
            }
        }

        // Slime special: REGEN
        if (type == POIType.Slime && !IsRegenerating)
        {
            float regenChance = 15f;
            float healPercent = 10f;
            int turns = 3;
            float duration = 2.0f;

            if (GameServices.TryGet(out EnemySpecialController esc))
            {
                regenChance = esc.GetSpecialChance(POIType.Slime, 15f);
                var s = esc.GetSettings(POIType.Slime);
                healPercent = s != null ? s.effectValue : 10f;
                turns = s != null ? s.buffTurns : 3;
                duration = s != null ? s.effectDuration : 2.0f;
            }

            float regenRoll = Random.Range(0f, 100f);
            if (regenRoll < regenChance)
            {
                CombatLog.Info($"{gameObject.name} uses REGEN!");
                isAttacking = true;

                if (locomotionDriver != null)
                    locomotionDriver.PlaySpecial();
                else
                    HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Special);

                string txt = "REGEN!";
                Color col = Color.green;
                if (GameServices.TryGet(out EnemySpecialController escTxt))
                {
                    txt = escTxt.GetFloatingText(POIType.Slime, txt);
                    col = escTxt.GetTextColor(POIType.Slime, col);
                }

                ApplyRegenEffect(healPercent, turns, col);

                if (Application.isPlaying)
                {
                    GameObject regenGo = new GameObject("RegenText");
                    regenGo.transform.position = transform.position + Vector3.up * 2.8f;
                    regenGo.AddComponent<FloatingText>().Setup(txt, col);
                }

                yield return new WaitForSeconds(duration);
                isAttacking = false;
                yield break;
            }
        }

        // Golem special: EARTHEN SURGE
        if (type == POIType.Golem)
        {
            float surgeChance = 10f;
            float diceValueMeters = 10f;
            float duration = 2.0f;

            if (GameServices.TryGet(out EnemySpecialController esc))
            {
                surgeChance = esc.GetSpecialChance(POIType.Golem, 10f);
                var s = esc.GetSettings(POIType.Golem);
                diceValueMeters = s != null ? s.effectValue : 10f;
                duration = s != null ? s.effectDuration : 2.0f;
            }

            float surgeRoll = Random.Range(0f, 100f);
            if (surgeRoll < surgeChance)
            {
                CombatLog.Info($"{gameObject.name} uses EARTHEN SURGE!");
                isAttacking = true;

                if (locomotionDriver != null)
                    locomotionDriver.PlaySpecial();
                else
                    HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Special);

                float meters = diceValueMeters * (GlobalSettings.Instance != null ? GlobalSettings.Instance.metersPerStep : 3.0f);
                hero.ApplyKnockback(transform.position, meters);

                if (Application.isPlaying)
                {
                    string txt = "EARTHEN SURGE!";
                    Color col = new Color(0.6f, 0.4f, 0.2f);
                    if (GameServices.TryGet(out EnemySpecialController escTxt))
                    {
                        txt = escTxt.GetFloatingText(POIType.Golem, txt);
                        col = escTxt.GetTextColor(POIType.Golem, col);
                    }

                    GameObject surgeGo = new GameObject("SurgeText");
                    surgeGo.transform.position = transform.position + Vector3.up * 2.8f;
                    surgeGo.AddComponent<FloatingText>().Setup(txt, col);
                }

                yield return new WaitForSeconds(duration);
                isAttacking = false;
                yield break;
            }
        }

        // MonsterPlant special: POISON
        if (type == POIType.MonsterPlant)
        {
            float poisonChance = 20f;
            int damage = 8;
            int turns = 4;
            float duration = 2.0f;

            if (GameServices.TryGet(out EnemySpecialController esc))
            {
                poisonChance = esc.GetSpecialChance(POIType.MonsterPlant, 20f);
                var s = esc.GetSettings(POIType.MonsterPlant);
                damage = s != null ? (int)s.effectValue : 8;
                turns = s != null ? s.buffTurns : 4;
                duration = s != null ? s.effectDuration : 2.0f;
            }

            float poisonRoll = Random.Range(0f, 100f);
            if (poisonRoll < poisonChance)
            {
                CombatLog.Info($"{gameObject.name} uses <color=green>POISON</color>!");
                isAttacking = true;

                if (locomotionDriver != null)
                    locomotionDriver.PlaySpecial();
                else
                    HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Special);

                hero.ApplyPoisonEffect(damage, turns);

                if (Application.isPlaying)
                {
                    string txt = "POISON!";
                    Color col = Color.green;
                    if (GameServices.TryGet(out EnemySpecialController escTxt))
                    {
                        txt = escTxt.GetFloatingText(POIType.MonsterPlant, txt);
                        col = escTxt.GetTextColor(POIType.MonsterPlant, col);
                    }

                    // Show above STEVE since he is the one poisoned
                    GameObject poisonGo = new GameObject("PoisonText");
                    poisonGo.transform.position = hero.transform.position + Vector3.up * 2.8f;
                    poisonGo.AddComponent<FloatingText>().Setup(txt, col);
                }

                // Deal initial damage so Steve flinches/reacts
                yield return new WaitForSeconds(0.75f);
                if (!isDead && currentHP > 0 && hero != null && hero.InCombat)
                {
                    float initDmg = CombatLog.RollAndApplyCrit(attackDamage, critChance, critDamage, out bool isCrit);
                    hero.TakeDamage((int)initDmg, gameObject.name, isCrit);
                }

                yield return new WaitForSeconds(Mathf.Max(0, duration - 0.75f));
                isAttacking = false;
                yield break;
            }
        }

        // Spider special: RUN
        if (type == POIType.Spider)
        {
            float runChance = 50f;
            float diceSteps = 7f;
            float duration = 2.0f;

            if (GameServices.TryGet(out EnemySpecialController esc))
            {
                runChance = esc.GetSpecialChance(POIType.Spider, 50f);
                var s = esc.GetSettings(POIType.Spider);
                diceSteps = s != null ? s.effectValue : 7f;
                duration = s != null ? s.effectDuration : 2.0f;
            }

            float runRoll = Random.Range(0f, 100f);
            if (runRoll < runChance)
            {
                CombatLog.Info($"{gameObject.name} uses <color=white>SKITTER</color>!");
                isAttacking = true;
                isRunningAway = true;

                if (locomotionDriver != null)
                    locomotionDriver.PlaySpecial();
                else
                    HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Special);

                float meters = diceSteps * (GlobalSettings.Instance != null ? GlobalSettings.Instance.metersPerStep : 3.0f);
                
                // Steve exits combat
                hero.ExitCombat();

                // Calculate runaway position (directly away from hero)
                Vector3 awayDir = (transform.position - hero.transform.position).normalized;
                if (awayDir.sqrMagnitude < 0.001f) awayDir = transform.forward;
                awayDir.y = 0f;
                
                Vector3 targetPos = transform.position + awayDir.normalized * meters;
                
                // Find valid point on NavMesh
                if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, meters * 0.7f, NavMesh.AllAreas))
                {
                    targetPos = hit.position;
                }

                // Unlock and move
                UnlockCombatNavigation();
                if (agent != null && agent.enabled)
                {
                    agent.isStopped = false;
                    agent.speed = patrolSpeed * 2.5f; // Scurry away!
                    agent.SetDestination(targetPos);
                }

                if (Application.isPlaying)
                {
                    string txt = "SKITTER!";
                    Color col = Color.white;
                    if (GameServices.TryGet(out EnemySpecialController escTxt))
                    {
                        txt = escTxt.GetFloatingText(POIType.Spider, txt);
                        col = escTxt.GetTextColor(POIType.Spider, col);
                    }

                    GameObject runGo = new GameObject("RunText");
                    runGo.transform.position = transform.position + Vector3.up * 2.8f;
                    runGo.AddComponent<FloatingText>().Setup(txt, col);
                }

                // Wait for the scurry duration
                yield return new WaitForSeconds(duration);
                
                if (agent != null && agent.enabled)
                {
                    agent.speed = patrolSpeed;
                    // Note: destination remains set so it finishes moving to the runaway spot
                }

                isRunningAway = false;
                isAttacking = false;
                yield break;
            }
        }

        // TurtleShell special: HARDENED
        if (type == POIType.TurtleShell && !IsHardened)
        {
            float hardenChance = 25f;
            float reductionPercent = 50f;
            int turns = 2;
            float duration = 2.0f;

            if (GameServices.TryGet(out EnemySpecialController esc))
            {
                hardenChance = esc.GetSpecialChance(POIType.TurtleShell, 25f);
                var s = esc.GetSettings(POIType.TurtleShell);
                reductionPercent = s != null ? s.effectValue : 50f;
                turns = s != null ? s.buffTurns : 2;
                duration = s != null ? s.effectDuration : 2.0f;
            }

            float hardenRoll = Random.Range(0f, 100f);
            if (hardenRoll < hardenChance)
            {
                CombatLog.Info($"{gameObject.name} uses <color=blue>HARDENED</color>!");
                isAttacking = true;

                if (locomotionDriver != null)
                    locomotionDriver.PlaySpecial();
                else
                    HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Special);

                string txt = "HARDENED!";
                Color col = Color.blue;
                if (GameServices.TryGet(out EnemySpecialController escTxt))
                {
                    txt = escTxt.GetFloatingText(POIType.TurtleShell, txt);
                    col = escTxt.GetTextColor(POIType.TurtleShell, col);
                }

                ApplyHardenedEffect(reductionPercent, turns);

                if (Application.isPlaying)
                {
                    GameObject hardenGo = new GameObject("HardenedText");
                    hardenGo.transform.position = transform.position + Vector3.up * 2.8f;
                    hardenGo.AddComponent<FloatingText>().Setup(txt, col);
                }

                yield return new WaitForSeconds(duration);
                isAttacking = false;
                yield break;
            }
        }

        CombatLog.AttackStart(gameObject.name, hero != null ? hero.name : "Steve", "enemy melee");

        float windUp = settings != null ? settings.enemyAttackWindUp : 0.14f;
        float hitDelay = settings != null ? settings.enemyAttackHitDelay : 0.26f;

        yield return new WaitForSeconds(windUp);
        if (isDead || hero == null) { isAttacking = false; yield break; }

        if (locomotionDriver != null)
            locomotionDriver.PlayAttack();
        else
        {
            HeroAnimatorParams.ResetTriggerSafe(animator, HeroAnimatorParams.Attack);
            HeroAnimatorParams.SetTriggerSafe(animator, HeroAnimatorParams.Attack);
        }

        yield return new WaitForSeconds(hitDelay);
        if (!isDead && currentHP > 0 && hero != null && hero.InCombat)
        {
            float damage = CombatLog.RollAndApplyCrit(attackDamage, critChance, critDamage, out bool isCrit, out float critRoll);
            
            if (battleShoutTurnsRemaining > 0)
            {
                damage *= battleShoutMultiplier;
                battleShoutTurnsRemaining--;
                if (battleShoutTurnsRemaining <= 0)
                {
                    battleShoutMultiplier = 1.0f;
                }
            }

            CombatLog.CritCheck(gameObject.name, critChance, critRoll, isCrit);

            CombatLog.DamageCalc(gameObject.name, $"base {attackDamage:F0}" + (isCrit ? $" × crit {critDamage:F0}%" : "") + $" → {(int)damage}");
            bool hit = hero.TakeDamage((int)damage, gameObject.name, isCrit);
            if (!hit)
                CombatLog.DamageMitigated(gameObject.name, hero.name, "Steve dodged or invulnerable");
        }

        yield return new WaitForSeconds(settings != null ? settings.enemyAttackRecoverDelay : 0.4f);
        isAttacking = false;
    }
}
