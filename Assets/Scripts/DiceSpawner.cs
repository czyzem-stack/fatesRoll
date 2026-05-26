using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <remarks>Inherits <see cref="GameServiceBehaviour{T}"/> — auto-registers in Awake via <see cref="GameServices"/>.</remarks>
public class DiceSpawner : GameServiceBehaviour<DiceSpawner>
{

    private const string DefaultD6PrefabPath = "Assets/Dice/Prefabs/Dice_d6.prefab";
    private const string D6ResourcesPath = "Dice/Dice_d6";

    public GameObject d6Prefab;
    public Transform spawnPoint;
    [Tooltip("Impulse scale for dice throw (horizontal component).")]
    public float throwForce = 1.6f;
    public float torque = 15f;

    private List<GameObject> activeDice = new List<GameObject>();
    private HeroController cachedHero;
    private bool isRolling = false;
    private Coroutine rollCoroutine;
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

    protected override void Awake()
    {
        base.Awake();
        EnsureReferences();
        cachedHero = GameServices.Hero;
    }

    public void CancelActiveRoll()
    {
        if (rollCoroutine != null)
        {
            StopCoroutine(rollCoroutine);
            rollCoroutine = null;
        }

        isRolling = false;
        DestroyActiveDice();
    }

    private void DestroyActiveDice()
    {
        foreach (var d in activeDice)
        {
            if (d != null)
                Destroy(d);
        }

        activeDice.Clear();
    }

    /// <summary>Assigns default d6 prefab and spawn point when Inspector references are missing.</summary>
    public void EnsureReferences()
    {
        if (spawnPoint == null)
            spawnPoint = transform;

        if (d6Prefab != null)
            return;

#if UNITY_EDITOR
        d6Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultD6PrefabPath);
#endif
        if (d6Prefab == null)
            d6Prefab = Resources.Load<GameObject>(D6ResourcesPath);

        if (d6Prefab == null)
            GlobalSettings.LogGameplayWarning(
                $"DiceSpawner: d6Prefab is not assigned. Assign {DefaultD6PrefabPath} on DiceSpawner in the scene.");
    }

    public void ToggleAutoRoll()
    {
        autoRollActive = !autoRollActive;
        GlobalSettings.LogGameplay($"DiceSpawner: Auto-Roll {(autoRollActive ? "ENABLED" : "DISABLED")}");
        
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
        
        if (cachedHero == null)
            cachedHero = GameServices.Hero;
        if (cachedHero != null && cachedHero.IsMoving) return false;

        var settings = GlobalSettings.Instance;
        int rollCost = settings != null ? settings.energyDepletionPerRoll : 3;
        if (EnergyManager.Instance != null && !EnergyManager.Instance.HasEnergy(rollCost))
        {
            string msg = autoRollActive
                ? "DiceSpawner: Auto-Roll paused - not enough energy!"
                : "DiceSpawner: Not enough energy to roll!";
            GlobalSettings.LogGameplayWarning(msg);
            return false;
        }
        
        if (cachedHero != null && (cachedHero.IsDead || cachedHero.IsRespawning))
            return false;

        if (RunDeathController.Instance != null && RunDeathController.Instance.IsDeathInProgress)
            return false;

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
        if (rollCoroutine != null)
            StopCoroutine(rollCoroutine);
        rollCoroutine = StartCoroutine(RollRoutine());
    }

    private IEnumerator RollRoutine()
    {
        if (isRolling) yield break;
        isRolling = true;
        
        try 
        {
            GlobalSettings.LogGameplay("DiceSpawner: Starting Roll Routine.");

            var settings = GlobalSettings.Instance;
            if (EnergyManager.Instance != null && settings != null)
                EnergyManager.Instance.Deplete(settings.energyDepletionPerRoll);

            if (cachedHero == null)
                cachedHero = GameServices.Hero;
            var hero = cachedHero;
            if (hero != null && !hero.InCombat)
            {
                var steveAnim = hero.GetComponent<SteveAnimator>();
                steveAnim?.PlayThrow();
                yield return new WaitForSeconds(0.2f);
            }

            // 1. Aggressive Cleanup of old dice
            var existingDice = Object.FindObjectsByType<DieResult>(FindObjectsInactive.Exclude);
            foreach (var d in existingDice)
            {
                if (d != null && d.gameObject != null) Destroy(d.gameObject);
            }
            activeDice.Clear();

            EnsureReferences();

            if (d6Prefab == null)
            {
                Debug.LogError(
                    $"DiceSpawner: d6Prefab is missing. Assign {DefaultD6PrefabPath} on DiceSpawner in the scene.");
                yield break;
            }

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
                    Vector3 force = (throwDir * throwForce) + (Vector3.up * 1.5f);
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

            if (!allSettled)
                GlobalSettings.LogGameplayWarning("<b>[Dice Physics]</b> Dice didn't settle in time, reading anyway.");
            else
                GlobalSettings.LogGameplay("<b>[Dice Physics]</b> Dice settled successfully.");

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
            GlobalSettings.LogGameplay($"<color=green><b>[Final Dice Roll] {total}</b></color> (Individual: {LastIndividualRolls})");

            if (hero != null)
            {
                Enemy combatEnemy = hero.GetCurrentEnemy();

                if (hero.InCombat && combatEnemy != null && !combatEnemy.isDead)
                {
                    float readDelay = settings != null ? settings.combatDiceReadDelay : 0.35f;
                    yield return new WaitForSeconds(readDelay);

                    bool isCrit;
                    int finalDamage = hero.CalculateRollDamage(total, out isCrit);
                    CombatLog.Info($"Dice combat turn | roll {total} → {finalDamage} damage{(isCrit ? " (CRIT)" : "")}");

                    yield return hero.HeroAttackRoutine(combatEnemy, finalDamage);

                    if (combatEnemy.isDead)
                    {
                        int levelsGained = 0;
                        if (LevelManager.Instance != null)
                            levelsGained = LevelManager.Instance.AddXP(total * 2);

                        if (levelsGained > 0)
                        {
                            while (hero.IsBlockedForDice)
                                yield return null;
                        }
                        else
                        {
                            hero.VictoryFlourish();
                            yield return new WaitForSeconds(0.75f);
                        }
                        yield break;
                    }

                    if (settings != null)
                        yield return new WaitForSeconds(settings.combatReactionDelay);

                    if (hero.currentEnemy != null && combatEnemy != null && !combatEnemy.isDead)
                        combatEnemy.PerformAttack(hero);
                }
                else
                {
                    // In combat but enemy missing — don't bail out and walk away.
                    if (hero.InCombat && combatEnemy == null)
                        CombatLog.Info("Dice roll ignored — no valid combat target on currentEnemy");

                    if (!hero.InCombat)
                    {
                        if (hero.IsDead || hero.IsRespawning)
                            yield break;

                        hero.RecordRoll(total);
                        hero.MoveSteps(total);

                        float moveWait = 12f;
                        while (hero.IsMoving && moveWait > 0f && !hero.IsDead && !hero.IsRespawning)
                        {
                            moveWait -= Time.deltaTime;
                            yield return null;
                        }

                        if (hero.IsDead || hero.IsRespawning)
                            yield break;

                        Enemy pending = hero.GetPendingCombatEnemy();
                        if (pending != null && hero.TryBeginMeleeWithRoll(pending, total))
                        {
                            while (hero.IsEngageBusy)
                                yield return null;
                        }
                    }
                }

                // Add XP after the action (unless died/interrupted)
                if (LevelManager.Instance != null)
                {
                    int levelsGained = LevelManager.Instance.AddXP(total);
                    if (levelsGained > 0)
                    {
                        while (hero.IsBlockedForDice)
                            yield return null;
                    }
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
            rollCoroutine = null;
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
