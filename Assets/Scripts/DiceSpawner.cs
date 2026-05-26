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

            // 3. Wait for dice to settle (faster timeout while in combat)
            bool combatRoll = hero != null && hero.InCombat;
            yield return new WaitForSeconds(combatRoll ? 0.1f : 0.2f);

            float settleTimeout = combatRoll ? 1.25f : 3.0f;
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

            // 4. Brief read delay (shorter in combat for snappy turns)
            float readDelay = (hero != null && hero.InCombat)
                ? GlobalSettings.Instance.combatDiceReadDelay
                : GlobalSettings.Instance.travelDiceReadDelay;
            yield return new WaitForSeconds(readDelay);

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
                Enemy combatEnemy = null;
                if (hero.currentEnemy != null)
                    combatEnemy = hero.currentEnemy.GetComponent<Enemy>();

                if (hero.InCombat && combatEnemy != null && !combatEnemy.isDead)
                {
                    bool isCrit;
                    int finalDamage = hero.CalculateRollDamage(total, out isCrit);
                    Debug.Log($"<b>[Combat]</b> Dice combat turn | roll {total} → {finalDamage} damage{(isCrit ? " (CRIT)" : "")}");

                    yield return hero.HeroAttackRoutine(combatEnemy, finalDamage);

                    if (combatEnemy.isDead)
                    {
                        Debug.Log($"{combatEnemy.name} defeated!");
                        int levelsGained = 0;
                        if (LevelManager.Instance != null)
                            levelsGained = LevelManager.Instance.AddXP(total * 2);

                        if (levelsGained > 0)
                            yield return new WaitForSeconds(hero.LevelUpCelebrationSeconds * levelsGained);
                        else
                        {
                            hero.VictoryFlourish();
                            yield return new WaitForSeconds(0.75f);
                        }
                        yield break;
                    }

                    yield return new WaitForSeconds(GlobalSettings.Instance.combatReactionDelay);

                    if (hero.currentEnemy != null && combatEnemy != null && !combatEnemy.isDead)
                        combatEnemy.PerformAttack(hero);
                }
                else
                {
                    if (hero.InCombat)
                        hero.ExitCombat();

                    hero.RecordRoll(total);
                    hero.MoveSteps(total);
                }

                // Add XP after the action (unless died/interrupted)
                if (LevelManager.Instance != null)
                {
                    int levelsGained = LevelManager.Instance.AddXP(total);
                    if (levelsGained > 0)
                        yield return new WaitForSeconds(hero.LevelUpCelebrationSeconds * levelsGained);
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
