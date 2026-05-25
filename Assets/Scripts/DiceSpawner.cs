using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class DiceSpawner : MonoBehaviour
{
    public GameObject d6Prefab;
    public Transform spawnPoint;
    public float throwForce = 8f;
    public float torque = 15f;

    private List<GameObject> activeDice = new List<GameObject>();
    private bool isRolling = false;
    private bool autoRollActive = false;
    private float autoRollNextCheckTime = 0f;
    private float autoRollCheckInterval = 1.0f;

    public int LastRoll { get; private set; }
    public string LastIndividualRolls { get; private set; }

    [Header("Auto-Roll Settings")]
    public UnityEngine.UI.Image autoRollIndicator;
    public TMPro.TextMeshProUGUI autoRollText;
    public Color autoRollActiveColor = Color.cyan;
    public Color autoRollInactiveColor = Color.white;

    public void ToggleAutoRoll()
    {
        autoRollActive = !autoRollActive;
        Debug.Log($"DiceSpawner: Auto-Roll {(autoRollActive ? "ENABLED" : "DISABLED")}");
        
        if (autoRollIndicator != null)
        {
            autoRollIndicator.color = autoRollActive ? autoRollActiveColor : autoRollInactiveColor;
        }

        if (autoRollText == null)
        {
            var go = GameObject.Find("MainUI_Canvas/HUD_Control/Joystick_Button_l_Attack/AutoText");
            if (go != null) autoRollText = go.GetComponent<TMPro.TextMeshProUGUI>();
        }

        if (autoRollText != null)
        {
            autoRollText.color = autoRollActive ? Color.cyan : new Color(1, 1, 1, 0);
        }
    }

    public void OnRoll(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && CanRoll())
        {
            RollDice();
        }
    }

    private bool CanRoll()
    {
        if (isRolling) return false;
        
        var hero = Object.FindAnyObjectByType<HeroController>();
        if (hero != null && hero.IsMoving) return false;

        // Don't auto-roll in combat?
        // Actually, user said "hold click to enable auto roll", likely for everything.
        
        if (EnergyManager.Instance != null && !EnergyManager.Instance.HasEnergy(GlobalSettings.Instance.energyDepletionPerRoll))
        {
            if (autoRollActive)
            {
                Debug.LogWarning("DiceSpawner: Auto-Roll paused - not enough energy!");
            }
            else
            {
                Debug.LogWarning("DiceSpawner: Not enough energy to roll!");
            }
            return false;
        }
        
        return true;
    }

    void Update()
    {
        if (autoRollActive && !isRolling)
        {
            if (Time.time >= autoRollNextCheckTime)
            {
                if (CanRoll())
                {
                    RollDice();
                    autoRollNextCheckTime = Time.time + autoRollCheckInterval;
                }
            }
        }
    }

    [ContextMenu("Roll Dice")]
    public void RollDice()
    {
        if (!CanRoll()) return;
        StartCoroutine(RollRoutine());
    }

    private IEnumerator RollRoutine()
    {
        if (isRolling) yield break;
        isRolling = true;
        
        try 
        {
            Debug.Log("DiceSpawner: Starting Roll Routine.");

            // Deplete Energy
            if (EnergyManager.Instance != null)
            {
                EnergyManager.Instance.Deplete(GlobalSettings.Instance.energyDepletionPerRoll);
            }

            var hero = Object.FindAnyObjectByType<HeroController>();
            if (hero != null)
            {
                var anim = hero.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    // Only play the body throw animation if we are NOT in combat.
                    // During combat, Steve is already in a battle stance; we just spawn the dice.
                    if (!hero.InCombat)
                    {
                        anim.SetTrigger("Throw");
                        // Wait for the animation to reach the "throw" point
                        yield return new WaitForSeconds(0.2f);
                    }
                }
            }

            // 1. Aggressive Cleanup of old dice
var existingDice = Object.FindObjectsByType<DieResult>(FindObjectsInactive.Exclude);
            foreach (var d in existingDice)
            {
                if (d != null && d.gameObject != null) Destroy(d.gameObject);
            }
            activeDice.Clear();

            if (d6Prefab == null || spawnPoint == null) 
            {
                Debug.LogError("DiceSpawner: Prefab or SpawnPoint is NULL!");
                yield break;
            }

            // 2. Spawn new dice closer to Steve
            float reducedThrowForce = 1.6f; 
            
            // Get hero collider to ignore
            Collider heroCollider = hero != null ? hero.GetComponent<Collider>() : null;
            
            for (int i = 0; i < 2; i++)
            {
                // Position dice at chest height instead of hand to ensure they clear any ground geometry initially
                Vector3 spawnOrigin = hero != null ? hero.transform.position + Vector3.up * 1.2f : spawnPoint.position;
                Vector3 offset = new Vector3(i * 0.2f - 0.1f, 0.2f, 0.5f);
                GameObject die = Instantiate(d6Prefab, spawnOrigin + (transform.forward * 0.2f) + offset, Random.rotation);
                
                // Set Layer to 8 for Highlighting/X-Ray
                SetLayerRecursive(die, 8);
                
                if (die.GetComponent<DieResult>() == null) die.AddComponent<DieResult>();
                activeDice.Add(die);
                
                // Ignore collision with hero to prevent dice from being shoved through the ground or flying away
                Collider dieCollider = die.GetComponent<Collider>();
                if (dieCollider != null && heroCollider != null)
                {
                    Physics.IgnoreCollision(dieCollider, heroCollider);
                }
                
                Rigidbody rb = die.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    
                    // Throw slightly more upward and forward
                    Vector3 throwDir = (transform.forward * 0.6f + transform.right * Random.Range(-0.2f, 0.2f) + Vector3.up * 0.1f).normalized;
                    Vector3 force = (throwDir * reducedThrowForce) + (Vector3.up * 1.5f);
                    rb.AddForce(force, ForceMode.Impulse);
                    rb.AddTorque(Random.onUnitSphere * torque, ForceMode.Impulse);
                }
            }

            // 3. Wait for dice to settle - more robust check
            yield return new WaitForSeconds(0.2f); 
            
            float settleTimeout = 3.0f; // Longer timeout
            bool allSettled = false;
            while (settleTimeout > 0)
            {
                allSettled = true;
                foreach (var d in activeDice)
                {
                    if (d != null)
                    {
                        var res = d.GetComponent<DieResult>();
                        if (res != null && !res.IsSettled())
                        {
                            allSettled = false;
                            break;
                        }
                    }
                }
                if (allSettled) 
                {
                    // Brief additional check to confirm they aren't just bouncing
                    yield return new WaitForSeconds(0.1f);
                    allSettled = true;
                    foreach (var d in activeDice)
                    {
                        if (d != null)
                        {
                            var res = d.GetComponent<DieResult>();
                            if (res == null || !res.IsSettled())
                            {
                                allSettled = false;
                                break;
                            }
                        }
                    }
                    if (allSettled) break;
                }
                settleTimeout -= Time.deltaTime;
                yield return null;
            }

            if (!allSettled) Debug.LogWarning("<b>[Dice Physics]</b> Dice didn't settle perfectly within 3s, reading anyway.");
            else Debug.Log("<b>[Dice Physics]</b> Dice settled successfully.");

            // 4. Extra visual buffer - Player sees the result before action
            yield return new WaitForSeconds(1.0f);

            int total = 0;
            List<int> individual = new List<int>();
            foreach (var d in activeDice)
            {
                if (d != null)
                {
                    var res = d.GetComponent<DieResult>();
                    if (res != null)
                    {
                        int val = res.GetValue();
                        total += val;
                        individual.Add(val);
                    }
                }
            }
            LastRoll = total;
            LastIndividualRolls = string.Join(", ", individual);
            Debug.Log($"<color=green><b>[Final Dice Roll] {total}</b></color> (Individual: {LastIndividualRolls})");

            if (hero != null)
            {
                if (hero.InCombat)
                {
                    // Combat Attack - Driven by PlayerStats
                    PlayerStats pStats = hero.GetComponent<PlayerStats>();
                    float baseDamage = pStats != null ? pStats.AttackDamage : 20f;
                    
                    // Scaling: roll of 7 is 100% base damage
                    float rollMultiplier = total / 7.0f;
                    float heroDamage = baseDamage * rollMultiplier;

                    // Critical Hit Check
                    bool isCrit = false;
                    if (pStats != null && Random.Range(0f, 100f) < pStats.CritChance)
                    {
                        isCrit = true;
                        heroDamage *= (1f + (pStats.CritDamage / 100f));
                    }

                    int finalDamage = Mathf.RoundToInt(heroDamage);
                    
                    if (hero.currentEnemy != null)
                    {
                        var enemy = hero.currentEnemy.GetComponent<Enemy>();
                        if (enemy != null)
                        {
                            string critMsg = isCrit ? " <color=red>CRITICAL HIT!</color>" : "";
                            Debug.Log($"<b>[Combat Action]</b> Steve Attacks: Dealing {finalDamage} damage to {enemy.name}{critMsg}");

                            // [COMBAT CLEANUP]
                            // 1. Force both combatants to face each other snappily
                            hero.FaceTarget(enemy.transform, true);
                            enemy.FaceTarget(hero.transform, true);

                            // 2. Short beat to prepare the lunge
                            yield return new WaitForSeconds(0.25f);

                            // 3. Hero Attack Animation
                            var heroAnim = hero.GetComponentInChildren<Animator>();
                            if (heroAnim != null)
                            {
                                // Partial Attack Animation (Preparation)
                                heroAnim.CrossFade("Challenging_Battle_SwordAndShield", 0.1f);
                                yield return new WaitForSeconds(0.6f);

                                heroAnim.ResetTrigger("Attack");
                                heroAnim.SetTrigger("Attack");
                            }
                            
                            // 4. Enemy takes damage and reacts
                            yield return new WaitForSeconds(0.35f);
                            bool hitOk = enemy.TakeDamage(finalDamage);

                            if (hitOk)
                            {
                                Debug.Log($"<b>[Combat Success]</b> {enemy.name} was hit for {finalDamage}.");
                            }
                            else
                            {
                                Debug.Log($"<b>[Combat Miss]</b> {enemy.name} dodged Steve's attack!");
                            }

                            // Check if enemy died
                            if (enemy.isDead) 
                            {
                                Debug.Log($"{enemy.name} defeated!");
                                hero.VictoryFlourish();
                                if (LevelManager.Instance != null) LevelManager.Instance.AddXP(total * 2);
                                yield return new WaitForSeconds(1.2f);
                                yield break;
                            }

                            // Retaliation delay
                            yield return new WaitForSeconds(GlobalSettings.Instance.combatReactionDelay);

                            // 5. Enemy Attacks back
                            if (hero.currentEnemy != null && enemy != null)
                            {
                                enemy.PerformAttack(hero);
                            }
                        }
                    }
}
                else
                {
                    hero.MoveSteps(total);
                }

                // Add XP after the action (unless died/interrupted)
                if (LevelManager.Instance != null)
                {
                    bool leveledUp = LevelManager.Instance.AddXP(total);
                    if (leveledUp) yield return new WaitForSeconds(2.7f);
                }
            }
else
            {
                Debug.LogError("DiceSpawner: HeroController NOT found!");
            }
        }
        finally
        {
            isRolling = false;
        }
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        if (obj.GetComponent<Canvas>() != null || 
            obj.GetComponent<RectTransform>() != null || 
            obj.GetComponent<LineRenderer>() != null)
        {
            int target = (obj.GetComponent<LineRenderer>() != null) ? 0 : 5;
            SetLayerRecursiveInternal(obj, target);
            return;
        }

        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            obj.layer = layer;
        }
        else
        {
            obj.layer = 0;
        }

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    private void SetLayerRecursiveInternal(GameObject obj, int layer)
{
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursiveInternal(child.gameObject, layer);
        }
    }

}
