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
    private float nextPatrolTime;
    private bool navHeld;
    private bool combatNavLocked;
    private bool visualYawFixApplied;

    private Camera mainCamera;

    private void Start()
    {
        Initialize();
        cachedHero = Object.FindAnyObjectByType<HeroController>();
        SetHealthBarVisible(false);
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (isDead) return;

        if (!Application.isPlaying) return;

        HandleAI();
        UpdateAnimation();
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

        if (navHeld || isAttacking || (cachedHero != null && IsFightingHero(cachedHero)))
        {
            animator.SetFloat("Speed", 0f, 0.15f, Time.deltaTime);
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

        animator.SetFloat("Speed", speed, 0.2f, Time.deltaTime);
    }

    public void Initialize()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;

        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        
        agent.speed = patrolSpeed;
        agent.acceleration = 12f;
        agent.angularSpeed = 360f;
        agent.stoppingDistance = 0.35f;
        agent.autoBraking = true;
        agent.avoidancePriority = avoidancePriority;
        agent.updateRotation = true;

        ApplyVisualLocomotionFix();
        CalculateDerivedStats();
        currentHP = maxHP;
        spawnPosition = transform.position;

        UpdateHealthUI();
    }

    /// <summary>Bake 180° yaw on the visual rig once so NavMeshAgent can own rotation (no per-frame fighting).</summary>
    private void ApplyVisualLocomotionFix()
    {
        if (visualYawFixApplied || animator == null) return;

        Transform visualRoot = animator.transform;
        while (visualRoot.parent != null && visualRoot.parent != transform)
            visualRoot = visualRoot.parent;

        Vector3 animForward = visualRoot.forward;
        animForward.y = 0f;
        if (animForward.sqrMagnitude < 0.01f) return;
        animForward.Normalize();

        if (Vector3.Dot(animForward, transform.forward) < 0f)
            visualRoot.localRotation *= Quaternion.Euler(0f, 180f, 0f);

        visualYawFixApplied = true;
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
        return agent != null && agent.enabled && !agent.isStopped && !navHeld && !isDead && !combatNavLocked;
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
        if (cachedHero == null) cachedHero = Object.FindAnyObjectByType<HeroController>();
        if (cachedHero == null) return;

        bool isEngaged = IsFightingHero(cachedHero);

        if (animator != null)
            animator.SetBool("InCombat", isEngaged);

        if (isEngaged)
        {
            HoldEngaged();
            return;
        }

        UnlockCombatNavigation();

        // Steve walks in or is in melee range — stand still. Never chase; never walk off when he arrives.
        if (cachedHero.IsEngageBusy || isAttacking ||
            IsSteveApproachingUs() || IsWithinEngageRange(cachedHero) ||
            (cachedHero.IsMoving && IsHeroInPatrolZone(cachedHero)))
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
                    StartCoroutine(TauntRoutine());
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

    private System.Collections.IEnumerator TauntRoutine()
    {
        isAttacking = true;
        LockCombatNavigation();

        if (animator != null)
        {
            animator.SetTrigger("Taunting");
        }

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
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion want = Quaternion.LookRotation(dir.normalized);
        if (Quaternion.Angle(transform.rotation, want) > 2f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, speed * 20f * Time.deltaTime);
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
        {
            healthSlider.maxValue = maxHP;
            healthSlider.value = currentHP;
        }
    }

    private void UpdateHealthUI()
    {
        if (healthSlider == null || healthCanvas == null) EnsureHealthBar();

        if (healthSlider != null)
        {
            if (healthSlider.maxValue != maxHP) healthSlider.maxValue = maxHP;
            healthSlider.value = currentHP;

            float hpPercent = Mathf.Clamp01(currentHP / maxHP);
            
            // Background
            var bg = healthSlider.transform.Find("Bg")?.GetComponent<UnityEngine.UI.Image>();
            if (bg == null) bg = healthSlider.transform.Find("Background")?.GetComponent<UnityEngine.UI.Image>();
            
            if (bg != null) 
            {
                // Subtle darkening of the existing fantasy background
                bg.color = new Color(0.2f, 0.2f, 0.2f, 0.85f); 
            }

            // Fill
            var fill = healthSlider.fillRect?.GetComponent<UnityEngine.UI.Image>();
            if (fill != null) 
            {
                // Dynamic health color (Green -> Red)
                Color c = Color.Lerp(Color.red, Color.green, hpPercent);
                c.a = 1.0f;
                fill.color = c;
            }

            // Border - Leave as-is or keep bright
            var border = healthSlider.transform.Find("Border")?.GetComponent<UnityEngine.UI.Image>();
            if (border != null)
            {
                border.color = Color.white;
            }
        }
    }

    public bool TakeDamage(float amount, string attackerName = "Steve")
    {
        if (isDead || currentHP <= 0) return false;

        float dodgeRoll = Random.Range(0f, 100f);
        if (dodgeRoll < dodgeChance)
        {
            CombatLog.Dodge(gameObject.name, dodgeChance, dodgeRoll);
            CombatLog.DamageMitigated(attackerName, gameObject.name, "dodged");
            return false;
        }

        float hpBefore = currentHP;
        SetHealthBarVisible(true);
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
        else if (animator != null)
        {
            animator.SetTrigger("GetHit");
        }

        return true;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        UnlockCombatNavigation();

        if (animator != null)
        {
            animator.SetTrigger("Die");
            animator.SetBool("IsDead", true);
            animator.SetBool("InCombat", false);
        }

        if (LootManager.Instance != null)
            LootManager.Instance.OnEnemyDied(this);
        
        gameObject.tag = "Untagged";
        if (healthSlider != null) healthSlider.gameObject.SetActive(false);

        if (cachedHero != null)
        {
            cachedHero.ExitCombat();
            cachedHero.OnPOIDefeated(GetComponentInParent<POINode>());
        }

        if (POIManager.Instance != null)
        {
            StartCoroutine(DelayedResolve(POIManager.Instance));
        }
        else
        {
            Destroy(gameObject, 2.5f);
        }
    }

    private void OnDestroy()
    {
        if (cachedHero != null && cachedHero.currentEnemy == gameObject)
        {
            cachedHero.ExitCombat();
        }
    }

    private System.Collections.IEnumerator DelayedResolve(POIManager pm)
    {
        yield return new WaitForSeconds(2.0f);
        pm.ResolvePOI(gameObject);
    }

    public void PerformAttack(HeroController hero)
    {
        if (isDead || currentHP <= 0) return;
        if (isAttacking) return;
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
        CombatLog.AttackStart(gameObject.name, hero != null ? hero.name : "Steve", "enemy melee");

        float windUp = settings != null ? settings.enemyAttackWindUp : 0.14f;
        float hitDelay = settings != null ? settings.enemyAttackHitDelay : 0.26f;

        yield return new WaitForSeconds(windUp);
        if (isDead || hero == null) { isAttacking = false; yield break; }

        if (animator != null)
        {
            animator.ResetTrigger("Attack");
            animator.SetTrigger("Attack");
        }

        yield return new WaitForSeconds(hitDelay);
        if (!isDead && currentHP > 0 && hero != null)
        {
            float damage = attackDamage;
            float critRoll = Random.Range(0f, 100f);
            bool isCrit = critRoll < critChance;
            CombatLog.CritCheck(gameObject.name, critChance, critRoll, isCrit);
            if (isCrit) damage *= (1f + (critDamage / 100f));

            CombatLog.DamageCalc(gameObject.name, $"base {attackDamage:F0}" + (isCrit ? $" × crit {critDamage:F0}%" : "") + $" → {(int)damage}");
            bool hit = hero.TakeDamage((int)damage, gameObject.name, isCrit);
            if (!hit)
                CombatLog.DamageMitigated(gameObject.name, hero.name, "Steve dodged or invulnerable");
        }
        isAttacking = false;
    }
}
