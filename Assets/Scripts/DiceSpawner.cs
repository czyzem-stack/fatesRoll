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
                var anim = hero.GetComponent<Animator>();
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
            float reducedThrowForce = 1.6f; // Further reduced from 2.5 to keep it very tight
            
            for (int i = 0; i < 2; i++)
            {
                // Position dice slightly in front of the hand spawn point
                Vector3 offset = new Vector3(i * 0.2f - 0.1f, 0.1f, 0.2f);
                GameObject die = Instantiate(d6Prefab, spawnPoint.position + offset, Random.rotation);
                
                // Set Layer to 8 for Highlighting/X-Ray
                SetLayerRecursive(die, 8);
                
                if (die.GetComponent<DieResult>() == null) die.AddComponent<DieResult>();
activeDice.Add(die);
                
                Rigidbody rb = die.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    
                    // Throw more downward to keep it close
                    Vector3 throwDir = (transform.forward * 0.4f + transform.right * Random.Range(-0.2f, 0.2f) + Vector3.down * 0.4f).normalized;
                    Vector3 force = (throwDir * reducedThrowForce) + (Vector3.up * 0.8f);
                    rb.AddForce(force, ForceMode.Impulse);
                    rb.AddTorque(Random.onUnitSphere * torque, ForceMode.Impulse);
                }
            }

            // 3. Wait for dice to settle - more robust check
            yield return new WaitForSeconds(0.1f); 
            
            float settleTimeout = hero != null && hero.InCombat ? 1.0f : 2.0f; // Faster in combat
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
                    yield return new WaitForSeconds(0.05f);
                    allSettled = true;
                    foreach (var d in activeDice)
                    {
                        if (d != null && !d.GetComponent<DieResult>().IsSettled())
                        {
                            allSettled = false;
                            break;
                        }
                    }
                    if (allSettled) break;
                }
                settleTimeout -= Time.deltaTime;
                yield return null;
            }

            if (!allSettled) Debug.LogWarning("DiceSpawner: Dice didn't settle perfectly, reading anyway.");

            int total = 0;
            List<int> individual = new List<int>();
            foreach (var d in activeDice)
            {
                if (d != null)
                {
                    int val = d.GetComponent<DieResult>().GetValue();
                    total += val;
                    individual.Add(val);
                }
            }
            LastRoll = total;
            LastIndividualRolls = string.Join(", ", individual);
            Debug.Log($"<color=green><b>[QA] FINAL DICE RESULT: {total}</b></color> (Individual: {LastIndividualRolls})");

            // Add XP based on roll
            if (LevelManager.Instance != null)
            {
                bool leveledUp = LevelManager.Instance.AddXP(total);
                if (leveledUp)
                {
                    // Wait for dance animation to finish (approx 2.67s)
                    yield return new WaitForSeconds(2.7f);
                }
            }

            if (hero != null)
            {
                if (hero.InCombat)
                {
                    // Combat Attack - Hero must roll to continue
                    int heroDamage = total * GlobalSettings.Instance.combatDamageMultiplier;
                    
                    if (hero.currentEnemy != null)
                    {
                        var enemy = hero.currentEnemy.GetComponent<EnemyCombatant>();
                        if (enemy != null)
                        {
                            // 1. Hero Attack Animation
                            var heroAnim = hero.GetComponent<Animator>();
                            if (heroAnim != null)
                            {
                                heroAnim.ResetTrigger("Attack");
                                heroAnim.SetTrigger("Attack");
                            }
                            Debug.Log($"DiceSpawner: Hero ATTACKS {enemy.name} for {heroDamage} damage.");

                            // Perfect strike timing (aligned with sword impact)
                            yield return new WaitForSeconds(0.25f);

                            // 2. Enemy takes damage and reacts
                            enemy.TakeDamage(heroDamage);

                            // Check if enemy died (use IsDead because ExitCombat is delayed for visuals)
                            if (enemy.IsDead) 
                            {
                                Debug.Log("DiceSpawner: Enemy defeated.");
                                hero.VictoryFlourish();
                                yield return new WaitForSeconds(1.2f); // Buttery delay to let animations play
                                yield break;
                            }

                            // Snappier reaction delay
                            yield return new WaitForSeconds(GlobalSettings.Instance.combatReactionDelay);

                            // 3. Enemy Attacks back
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
