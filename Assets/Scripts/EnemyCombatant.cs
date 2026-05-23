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

    private void Update()
    {
        if (isDead) return;

        // Ensure UI references are fresh
        if (healthSlider == null || healthCanvas == null)
        {
            EnsureHealthBar();
        }

        if (healthCanvas != null && Camera.main != null)
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
                if (dist < 10.0f)
                {
                    FaceTarget(cachedHero.transform);
                }
            }
        }
    }

    public void FaceTarget(Transform target)
    {
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10.0f); // Snappier
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
        
        FaceTarget(hero.transform); // Explicit face target
        if (animator != null)
        {
            animator.ResetTrigger("Attack");
            animator.SetTrigger("Attack");
        }
        Debug.Log($"{gameObject.name} plays attack animation.");

        // Delay damage until animation swing (approx 0.4s for Orc)
        StartCoroutine(DealDelayedDamage(hero));
    }

    private System.Collections.IEnumerator DealDelayedDamage(HeroController hero)
    {
        yield return new WaitForSeconds(0.4f);
        if (!isDead && stats != null && stats.currentHP > 0 && hero != null)
        {
            // Use derived Attack Damage from stats
            float baseDamage = stats.AttackDamage;
            
            // Enemy Crit Check
            if (Random.Range(0f, 100f) < stats.CritChance)
            {
                baseDamage *= (1f + (stats.CritDamage / 100f));
            }

            int finalDamage = Mathf.RoundToInt(baseDamage);
hero.TakeDamage(finalDamage);
            // Logging is now handled by PlayerStats.TakeDamage
        }
    }
}
