using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
public class EnemyCombatant : MonoBehaviour
{
    private int currentHP;
    private int maxHP;
    private Slider healthSlider;
    private Canvas healthCanvas;
    private Animator animator;
    private HeroController cachedHero;

    private void Start()
    {
        Initialize();
        cachedHero = Object.FindAnyObjectByType<HeroController>();
    }

    private void Initialize()
{
        if (GlobalSettings.Instance != null)
        {
            maxHP = GlobalSettings.Instance.orcStartHP;
            if (currentHP == 0) currentHP = maxHP;
        }
        else
        {
            maxHP = 15;
            if (currentHP == 0) currentHP = 15;
        }

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInParent<Animator>();
        
        EnsureHealthBar();
    }

    private void EnsureHealthBar()
    {
        if (healthCanvas == null)
        {
            healthCanvas = GetComponent<Canvas>();
        }

        if (healthSlider == null)
        {
            var sliderObj = transform.Find("Slider");
            if (sliderObj != null) healthSlider = sliderObj.GetComponent<Slider>();
        }
        
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHP;
            healthSlider.value = currentHP;
        }
    }

    private bool isDead = false;
public bool IsDead => isDead;

    private void Update()
{
        if (isDead) return;

        if (healthCanvas == null)
        {
            healthCanvas = GetComponent<Canvas>();
            var sliderObj = transform.Find("Slider");
            if (sliderObj != null) healthSlider = sliderObj.GetComponent<Slider>();
        }

        if (healthCanvas != null && Camera.main != null)
        {
            healthCanvas.transform.rotation = Camera.main.transform.rotation;
        }

        if (!Application.isPlaying) return;

        // Face the hero if nearby and not dead
        if (currentHP > 0)
{
            var hero = Object.FindAnyObjectByType<HeroController>();
            if (hero != null)
            {
                float dist = Vector3.Distance(transform.position, hero.transform.position);
                if (dist < 10.0f) // Increased range
                {
                    FaceTarget(hero.transform);
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

    public void TakeDamage(int amount)
    {
        if (currentHP <= 0) return; // Already dead

        currentHP -= amount;
        if (healthSlider != null) healthSlider.value = currentHP;
        
        GameObject go = new GameObject("FloatingText_Damage");
        go.transform.position = transform.position + Vector3.up * 2.8f;
        var ft = go.AddComponent<FloatingText>();
        ft.Setup($"-{amount} HP", Color.yellow);

        if (currentHP <= 0)
        {
            currentHP = 0;
            Die();
        }
        else
        {
            if (animator != null) animator.SetTrigger("GetHit");
        }
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
        if (isDead || currentHP <= 0) return;
        
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
        if (!isDead && currentHP > 0 && hero != null)
        {
            // Simple enemy damage logic
            int damage = Random.Range(2, 5);
            hero.TakeDamage(damage);
            Debug.Log($"{gameObject.name} deals {damage} damage to hero.");
        }
    }
}
