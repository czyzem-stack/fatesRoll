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
    public float chaseSpeed = 5.5f;
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
    private bool idleAggroTriggered;

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
        
        float speed = 0f;
        if (agent != null && agent.enabled && !agent.isStopped)
        {
            speed = agent.velocity.magnitude;
            // Use 0.2f threshold to match the animator's new stability thresholds
            if (speed < 0.2f) speed = 0f;
            else if (speed < 1.0f) speed = 1.0f;
        }
        
        // Use 0.1s damping to smoothly transition between idle/run and eliminate jitter
        animator.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
    }

    public void Initialize()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;

        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        
        agent.speed = patrolSpeed;
        agent.acceleration = 40.0f; // Very snappy
        agent.angularSpeed = 720.0f; // Instant turning
        agent.stoppingDistance = 1.4f; // Melee range to prevent circling Steve's agent
        agent.avoidancePriority = avoidancePriority;
        agent.updateRotation = true;

        CalculateDerivedStats();
        currentHP = maxHP;
        spawnPosition = transform.position;

        UpdateHealthUI();
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
        maxHP = vitality * 10f + 100f;
        attackDamage = strength * 4f + 20f;
        attackSpeed = 1.0f + (agility * 0.03f);
        critChance = luck * 0.8f;
        critDamage = 50f + (luck * 1.5f);
        dodgeChance = agility * 0.6f;

        if (currentHP > maxHP) currentHP = maxHP;
    }

    private void HandleAI()
    {
        if (cachedHero == null) cachedHero = Object.FindAnyObjectByType<HeroController>();
        if (cachedHero == null) return;

        float distToHero = Vector3.Distance(transform.position, cachedHero.transform.position);
        float distToSpawn = Vector3.Distance(spawnPosition, cachedHero.transform.position);
        bool isEngaged = (cachedHero.currentEnemy == gameObject);

        if (animator != null)
        {
            animator.SetBool("InCombat", isEngaged);
        }

        if (distToHero >= 2.5f || cachedHero.IsMoving ||
            (cachedHero.InCombat && cachedHero.currentEnemy != gameObject))
        {
            idleAggroTriggered = false;
        }

        if (isEngaged)
        {
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero; // Stop immediate to allow idle transition
            }
            FaceTarget(cachedHero.transform, false, 25.0f); // Snappy but smooth
        }
        else if (distToHero < 2.0f && !cachedHero.InCombat && !cachedHero.IsMoving &&
                 !idleAggroTriggered && !isAttacking)
        {
            idleAggroTriggered = true;
            Debug.Log($"<b>[Combat]</b> {gameObject.name} initiates combat with idle Steve!");
            cachedHero.EnterCombat(gameObject);
            PerformAttack(cachedHero);
        }
        else if (distToSpawn < 4.5f || distToHero < 6.0f)
        {
            // Run at Steve
            if (agent != null && agent.enabled)
            {
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                agent.SetDestination(cachedHero.transform.position);
            }
        }
        else if (!isAttacking)
        {
            HandlePatrol();
        }
    }

    private void HandlePatrol()
    {
        if (agent == null || !agent.enabled) return;
        agent.speed = patrolSpeed;

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
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                    nextPatrolTime = Time.time + Random.Range(3f, 7f);
                }
            }
        }
    }

    private System.Collections.IEnumerator TauntRoutine()
    {
        isAttacking = true;
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

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
        Vector3 targetPos = target.position;
        targetPos.y = transform.position.y;
        Vector3 direction = (targetPos - transform.position).normalized;
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            if (instant) transform.rotation = targetRotation;
            else transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * speed);
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

    private void OnGUI()
    {
        // Removed to ensure HP bar draws behind ScreenSpaceOverlay UI.
        // Handled via child uGUI Canvas in World Space.
    }

    public bool TakeDamage(float amount)
    {
        if (isDead || currentHP <= 0) return false;

        // Dodge check
        if (Random.Range(0f, 100f) < dodgeChance)
        {
            Debug.Log($"<b>[Combat]</b> {gameObject.name} dodged!");
            return false;
        }

        SetHealthBarVisible(true);
        currentHP -= amount;
        UpdateHealthUI();
        
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

        if (animator != null)
        {
            animator.SetTrigger("Die");
            animator.SetBool("IsDead", true);
            animator.SetBool("InCombat", false);
        }
        
        gameObject.tag = "Untagged";
        if (healthSlider != null) healthSlider.gameObject.SetActive(false);

        if (cachedHero != null) cachedHero.ExitCombat();

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
        isAttacking = true;
        FaceTarget(hero.transform, false, 30.0f); // Smooth snappy rotation
        StartCoroutine(AttackRoutine(hero));
    }

    private System.Collections.IEnumerator AttackRoutine(HeroController hero)
    {
        yield return new WaitForSeconds(0.4f);
        if (isDead || hero == null) { isAttacking = false; yield break; }

        if (animator != null)
        {
            animator.ResetTrigger("Attack");
            animator.SetTrigger("Attack");
        }

        yield return new WaitForSeconds(0.4f);
        if (!isDead && currentHP > 0 && hero != null)
        {
            float damage = attackDamage;
            if (Random.Range(0f, 100f) < critChance) damage *= (1f + (critDamage / 100f));
            hero.TakeDamage((int)damage);
        }
        isAttacking = false;
    }
}
