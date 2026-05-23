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
            var existing = transform.Find("HealthCanvas");
            if (existing != null)
            {
                healthCanvas = existing.GetComponent<Canvas>();
                var sliderObj = existing.Find("Slider");
                if (sliderObj != null) healthSlider = sliderObj.GetComponent<Slider>();
            }
            else
            {
                CreateHealthBar();
            }
        }
        
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHP;
            healthSlider.value = currentHP;
        }
    }

    private void CreateHealthBar()
    {
        GameObject canvasGO = new GameObject("HealthCanvas");
        canvasGO.transform.SetParent(transform);
        canvasGO.transform.localPosition = Vector3.up * 2.5f;
        
        healthCanvas = canvasGO.AddComponent<Canvas>();
        healthCanvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<CanvasScaler>();
        
        RectTransform rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1, 0.2f);
        rt.localScale = Vector3.one * 1.0f; // Increased from 0.5

        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bg = bgGO.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.8f);
        string bgPath = "Assets/UI/GUI Pro-FantasyRPG/ResourcesData/Sprites/Component/Slider/Slider_Basic_Rectangle_Bg.png";
        bg.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(bgPath);
        bg.type = Image.Type.Sliced;
        bgGO.GetComponent<RectTransform>().sizeDelta = new Vector2(1, 0.2f);

        GameObject sliderGO = new GameObject("Slider");
        sliderGO.transform.SetParent(canvasGO.transform, false);
        healthSlider = sliderGO.AddComponent<Slider>();
        
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillArea.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.sizeDelta = new Vector2(-0.05f, -0.05f); // Padding

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = Color.red;
        fillImg.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(bgPath); // Use same solid rect
        fillImg.type = Image.Type.Sliced;
        fill.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 0);
        fill.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        fill.GetComponent<RectTransform>().anchorMax = Vector2.one;

        healthSlider.fillRect = fill.GetComponent<RectTransform>();
        healthSlider.minValue = 0;
        healthSlider.maxValue = maxHP;
        healthSlider.value = currentHP;
        
        sliderGO.GetComponent<RectTransform>().sizeDelta = new Vector2(1, 0.2f);
    }

    private bool isDead = false;
    public bool IsDead => isDead;

    private void Update()
{
        if (isDead) return;

        if (healthCanvas == null)
{
            // Try to find it if it was created but reference lost
            var existing = transform.Find("HealthCanvas");
            if (existing != null) 
            {
                healthCanvas = existing.GetComponent<Canvas>();
                var sliderObj = existing.Find("Slider");
                if (sliderObj != null) healthSlider = sliderObj.GetComponent<Slider>();
            }
            else
            {
                CreateHealthBar();
            }
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
        
        // Hide health bar immediately for cleaner look
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(false);

        if (cachedHero != null) 
        {
            cachedHero.ExitCombat();
        }

        var pm = POIManager.Instance;
        if (pm != null)
        {
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
