using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class Enemy : MonoBehaviour
{
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

    private void Start()
    {
        Initialize();
        cachedHero = Object.FindAnyObjectByType<HeroController>();
        SetHealthBarVisible(false);
        spawnPosition = transform.position;
    }

    private void Update()
    {
        CalculateDerivedStats();

        if (isDead) return;

        UpdateHealthUI();

        if (!Application.isPlaying) return;

        HandleAI();
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;
        
        float speed = 0f;
        if (agent != null && agent.enabled)
        {
            speed = agent.velocity.magnitude;
            // If the agent is moving but speed is very low, boost it slightly for the animation to trigger
            if (speed > 0.1f && speed < 1.0f) speed = 1.0f;
        }
        
        animator.SetFloat("Speed", speed);
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
        agent.stoppingDistance = 0.4f; // Must be less than the 0.5f check in HandlePatrol
        agent.avoidancePriority = avoidancePriority;
        agent.updateRotation = true;

        CalculateDerivedStats();
        currentHP = maxHP;

        EnsureHealthBar();
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

        if (isEngaged)
        {
            if (agent != null && agent.enabled) agent.isStopped = true;
            FaceTarget(cachedHero.transform, false, 20.0f);
        }
        else if (distToSpawn < 4.5f || distToHero < 6.0f)
        {
            // Run at Steve
            if (agent != null && agent.enabled)
            {
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                agent.SetDestination(cachedHero.transform.position);
                if (animator != null) animator.SetFloat("Speed", agent.velocity.magnitude);
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

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
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

            // Restore the "green red black" look
            float hpPercent = Mathf.Clamp01(currentHP / maxHP);
            
            // Set Background to Solid Black
            var bg = healthSlider.transform.Find("Background")?.GetComponent<UnityEngine.UI.Image>();
            if (bg == null) bg = healthSlider.transform.parent.Find("Background")?.GetComponent<UnityEngine.UI.Image>();
            
            if (bg != null) 
            {
                bg.sprite = null; // Remove any transparent sprite
                bg.color = new Color(0, 0, 0, 1); // Solid 100% opaque black
            }

            // Set Fill to Solid Green/Red Lerp
            var fill = healthSlider.fillRect?.GetComponent<UnityEngine.UI.Image>();
            if (fill != null) 
            {
                fill.sprite = null; // Remove any transparent sprite
                Color c = Color.Lerp(Color.red, Color.green, hpPercent);
                c.a = 1.0f; // Force full opacity
                fill.color = c;
            }
        }

        if (healthCanvas != null && healthCanvas.transform != transform && Camera.main != null)
        {
            healthCanvas.transform.rotation = Camera.main.transform.rotation;
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

    private System.Collections.IEnumerator DelayedResolve(POIManager pm)
    {
        yield return new WaitForSeconds(2.0f);
        pm.ResolvePOI(gameObject);
    }

    public void PerformAttack(HeroController hero)
    {
        if (isDead || currentHP <= 0) return;
        isAttacking = true;
        FaceTarget(hero.transform, true);
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
