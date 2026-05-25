using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
public class EnemyCombatant : MonoBehaviour
{
    private PlayerStats stats;
    private Slider healthSlider;
    private Canvas healthCanvas;
    private Animator animator;
    private HeroController cachedHero;

    private void Start()
    {
        Initialize();
        cachedHero = Object.FindAnyObjectByType<HeroController>();
        SetHealthBarVisible(false); // Hide by default
    }

    private void Initialize()
    {
        stats = GetComponent<PlayerStats>();
        if (stats == null) stats = gameObject.AddComponent<PlayerStats>();

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInParent<Animator>();
        
        // Ensure root motion doesn't override our manual rotation
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        EnsureHealthBar();
    }

    public void SetHealthBarVisible(bool visible)
    {
        if (healthCanvas != null) healthCanvas.enabled = visible;
        // If slider is on a separate canvas or object, handle that too
        if (healthSlider != null && healthSlider.gameObject.activeSelf != visible)
        {
             // If we don't have a canvas, we might toggle the slider GO
             if (healthCanvas == null) healthSlider.gameObject.SetActive(visible);
        }
    }

    private void EnsureHealthBar()
    {
        if (healthCanvas == null)
        {
            healthCanvas = GetComponentInChildren<Canvas>(true);
        }

        if (healthSlider == null)
        {
            healthSlider = GetComponentInChildren<Slider>(true);
        }
        
        if (healthSlider != null && stats != null)
        {
            healthSlider.maxValue = stats.MaxHP;
            healthSlider.value = stats.currentHP;
        }
    }

    private bool isDead = false;
    public bool IsDead => isDead;
    private bool isAttacking = false;

    private void Update()
    {
        if (isDead) return;

        // Ensure UI references are fresh
        if (healthSlider == null || healthCanvas == null)
        {
            EnsureHealthBar();
        }

        // Keep HP Bar updated every frame to reflect reality
        if (healthSlider != null && stats != null)
        {
            // Sync max value just in case stats changed
            if (healthSlider.maxValue != stats.MaxHP) healthSlider.maxValue = stats.MaxHP;
            
            // Snapping is safer for "reflecting reality" immediately.
            healthSlider.value = stats.currentHP;
        }

        // Billboard logic: ONLY rotate if it's a child, not the root!
        if (healthCanvas != null && healthCanvas.transform != transform && Camera.main != null)
        {
            healthCanvas.transform.rotation = Camera.main.transform.rotation;
        }

        if (!Application.isPlaying) return;

        // Face the hero if nearby and not dead
        if (stats != null && stats.currentHP > 0)
        {
            if (cachedHero == null) cachedHero = Object.FindAnyObjectByType<HeroController>();
            if (cachedHero != null)
            {
                float dist = Vector3.Distance(transform.position, cachedHero.transform.position);
                bool isEngaged = (cachedHero.currentEnemy == gameObject);

                // If engaged in combat, face Steve aggressively.
                // If just nearby, face him smoothly.
                if (isEngaged)
                {
                    // Snappy rotation when in active combat
                    FaceTarget(cachedHero.transform, false, 20.0f);
                }
                else if (dist < 10.0f && !isAttacking)
                {
                    // Slightly slower but smoother rotation for passive tracking
                    FaceTarget(cachedHero.transform, false, 8.0f);
                }
            }
        }
    }

    public void FaceTarget(Transform target, bool instant = false, float speed = 15.0f)
    {
        if (target == null) return;
        
        // Use a position at the same height to avoid tilting
        Vector3 targetPos = target.position;
        targetPos.y = transform.position.y;
        
        Vector3 direction = (targetPos - transform.position).normalized;
        if (direction.sqrMagnitude > 0.001f)
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

    public bool TakeDamage(int amount)
    {
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (stats == null) 
        {
            Debug.LogError($"[Combat] {gameObject.name} is missing PlayerStats component!");
            return false;
        }

        if (stats.currentHP <= 0) return false; // Already dead

        SetHealthBarVisible(true);

        float oldHP = stats.currentHP;
        bool tookDamage = stats.TakeDamage((float)amount);
        
        if (tookDamage)
        {
            // Sync UI
            if (healthSlider == null) healthSlider = GetComponentInChildren<Slider>(true);
            if (healthSlider != null) 
            {
                healthSlider.maxValue = stats.MaxHP;
                healthSlider.value = stats.currentHP;
            }

            Debug.Log($"[Combat] {gameObject.name} HP: {oldHP:F1} -> {stats.currentHP:F1} (Took {amount} damage)");

            if (Application.isPlaying)
            {
                GameObject go = new GameObject("FloatingText_Damage");
                go.transform.position = transform.position + Vector3.up * 2.8f;
                var ft = go.AddComponent<FloatingText>();
                ft.Setup($"-{amount} HP", Color.yellow);
            }

            if (stats.currentHP <= 0)
            {
                Die();
            }
            else
            {
                if (animator != null) animator.SetTrigger("GetHit");
            }
        }
        
        return tookDamage;
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
        
        // Hide health bar components instead of deactivating the whole root GameObject
        if (healthSlider != null) healthSlider.gameObject.SetActive(false);
        var bg = transform.Find("Background");
        if (bg != null) bg.gameObject.SetActive(false);

        if (cachedHero != null) 
        {
            cachedHero.ExitCombat();
        }

        var pm = POIManager.Instance;
        if (pm != null)
        {
            // Now safe to start coroutine as the GameObject is still active
            StartCoroutine(DelayedResolve(pm));
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
        if (isDead || stats == null || stats.currentHP <= 0) return;
        
        isAttacking = true;

        // 1. Snappy face target
        FaceTarget(hero.transform, true); 
        
        // 2. Preparation delay so the retaliation isn't instant and jarring
        StartCoroutine(DelayedAttackAnimation(hero));
    }

    private System.Collections.IEnumerator DelayedAttackAnimation(HeroController hero)
    {
        yield return new WaitForSeconds(0.4f);
        
        if (isDead || hero == null) 
        {
            isAttacking = false;
            yield break;
        }

        if (animator != null)
        {
            animator.ResetTrigger("Attack");
            animator.SetTrigger("Attack");
        }
        
        // 3. Delay damage until animation swing (approx 0.4s for Orc)
        StartCoroutine(DealDelayedDamage(hero));
    }

    private System.Collections.IEnumerator DealDelayedDamage(HeroController hero)
    {
        yield return new WaitForSeconds(0.4f);
        if (!isDead && stats != null && stats.currentHP > 0 && hero != null)
        {
            float baseDamage = stats.AttackDamage;
            if (Random.Range(0f, 100f) < stats.CritChance)
            {
                baseDamage *= (1f + (stats.CritDamage / 100f));
                Debug.Log($"<color=orange>{gameObject.name} landed a CRITICAL retaliation hit!</color>");
            }

            int finalDamage = Mathf.RoundToInt(baseDamage);
            bool hitSucceeded = hero.TakeDamage(finalDamage);

            if (hitSucceeded)
            {
                Debug.Log($"[Combat retaliation] {gameObject.name} deals {finalDamage} damage to Hero.");
            }
            else
            {
                Debug.Log($"[Combat retaliation] {gameObject.name}'s attack was DODGED by Hero.");
            }
        }
        isAttacking = false;
    }
}
