using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string MainRollButtonPath = "MainUI_Canvas/HUD_Control/Joystick_Button_l_Attack";

    public GameObject d6Prefab;
    public Transform spawnPoint;
    [Tooltip("Impulse scale for dice throw (horizontal component).")]
    public float throwForce = 1.6f;
    public float torque = 15f;

    [SerializeField] private string gameplaySceneName = MainSceneGameplayGate.DefaultMainSceneName;

    private readonly List<GameObject> activeDice = new List<GameObject>();
    private HeroController cachedHero;
    private bool isRolling;
    private Coroutine rollCoroutine;
    private Coroutine initCoroutine;
    private bool autoRollActive;
    private float autoRollNextCheckTime;
    private float autoRollCheckInterval = 1.0f;
    private bool uiRollWired;
    private InputAction rollAction;

    public int LastRoll { get; private set; }
    public string LastIndividualRolls { get; private set; }

    [Header("Auto-Roll Settings")]
    public Image autoRollIndicator;
    public TMPro.TextMeshProUGUI autoRollText;
    public Color autoRollActiveColor = Color.cyan;
    public Color autoRollInactiveColor = Color.white;

    protected override void Awake()
    {
        base.Awake();

        if (!GameServices.IsInitialized)
            Debug.LogError("DiceSpawner: GameServices not ready yet!");

        EnsureReferences();
        cachedHero = GameServices.Hero;
    }

    protected override void Start()
    {
        base.Start();
        initCoroutine = StartCoroutine(InitializeGameplayInput());
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        rollAction?.Enable();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        rollAction?.Disable();
    }

    protected override void OnDestroy()
    {
        if (rollAction != null)
            rollAction.performed -= OnRoll;
        base.OnDestroy();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsMainScene(scene))
            return;

        MainSceneGameplayGate.Reset();
        if (initCoroutine != null)
            StopCoroutine(initCoroutine);
        initCoroutine = StartCoroutine(InitializeGameplayInput());
    }

    private IEnumerator InitializeGameplayInput()
    {
        yield return MainSceneGameplayGate.WaitUntilReady(gameplaySceneName);

        if (!EnsureGameServicesReady(nameof(InitializeGameplayInput)))
            yield break;

        cachedHero = GameServices.Hero;
        BindRollInputAction();
        WireMainSceneRollButtons();
        WireAutoRollUiReferences();
    }

    private bool EnsureGameServicesReady(string context)
    {
        if (!GameServices.IsInitialized)
        {
            Debug.LogError($"DiceSpawner: GameServices not ready yet! ({context})");
            return false;
        }

        if (!GameServices.TryGet(out DiceSpawner registered) || registered != this)
            GameServices.Register(this);

        return true;
    }

    private void BindRollInputAction()
    {
        if (rollAction != null)
            return;

        InputActionAsset asset = null;
#if UNITY_EDITOR
        asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
#endif
        if (asset == null)
            return;

        InputActionMap map = asset.FindActionMap("Player", throwIfNotFound: false);
        if (map == null)
            return;

        rollAction = map.FindAction("Roll", throwIfNotFound: false);
        if (rollAction == null)
            return;

        rollAction.performed += OnRoll;
        rollAction.Enable();
    }

    private void WireMainSceneRollButtons()
    {
        if (uiRollWired)
            return;

        Scene main = SceneManager.GetSceneByName(gameplaySceneName);
        if (!main.IsValid() || !main.isLoaded)
            return;

        bool wired = false;
        var attackButtonGo = GameObject.Find(MainRollButtonPath);
        if (attackButtonGo != null && attackButtonGo.TryGetComponent(out Button attackButton))
        {
            attackButton.onClick.AddListener(DiceRollGateway.Roll);
            wired = true;
        }

        foreach (GameObject root in main.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button == null || button.gameObject == attackButtonGo)
                    continue;

                if (!IsLikelyRollButton(button))
                    continue;

                button.onClick.AddListener(DiceRollGateway.Roll);
                wired = true;
            }
        }

        if (wired)
            uiRollWired = true;
    }

    private static bool IsLikelyRollButton(Button button)
    {
        string name = button.name;
        return name.Contains("Attack", System.StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Roll", System.StringComparison.OrdinalIgnoreCase);
    }

    private void WireAutoRollUiReferences()
    {
        if (autoRollText == null)
        {
            var autoTextGo = GameObject.Find($"{MainRollButtonPath}/AutoText");
            if (autoTextGo != null)
                autoRollText = autoTextGo.GetComponent<TMPro.TextMeshProUGUI>();
        }

        if (autoRollIndicator == null)
        {
            var attackButtonGo = GameObject.Find(MainRollButtonPath);
            if (attackButtonGo != null)
                autoRollIndicator = attackButtonGo.GetComponent<Image>();
        }
    }

    private bool IsMainScene(Scene scene) =>
        scene.IsValid() &&
        scene.name.Equals(gameplaySceneName, System.StringComparison.OrdinalIgnoreCase);

    private Transform GetRollAimTransform(HeroController hero)
    {
        if (hero != null)
            return hero.transform;
        if (spawnPoint != null)
            return spawnPoint;
        return transform;
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

        WireAutoRollUiReferences();

        if (autoRollIndicator != null)
            autoRollIndicator.color = autoRollActive ? autoRollActiveColor : autoRollInactiveColor;

        if (autoRollText != null)
            autoRollText.color = autoRollActive ? Color.cyan : new Color(1, 1, 1, 0);
    }

    public void OnRoll(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            RollDice();
    }

    private bool CanRoll()
    {
        if (!MainSceneGameplayGate.IsReady)
            return false;

        if (!EnsureGameServicesReady(nameof(CanRoll)))
            return false;

        if (isRolling)
            return false;

        if (cachedHero == null)
            cachedHero = GameServices.Hero;
        if (cachedHero != null && cachedHero.IsMoving)
            return false;

        var settings = GlobalSettings.Instance;
        int rollCost = settings != null ? settings.energyDepletionPerRoll : 3;
        if (GameServices.TryGet(out EnergyManager energy) && !energy.HasEnergy(rollCost))
        {
            string msg = autoRollActive
                ? "DiceSpawner: Auto-Roll paused - not enough energy!"
                : "DiceSpawner: Not enough energy to roll!";
            GlobalSettings.LogGameplayWarning(msg);
            return false;
        }

        if (cachedHero != null && (cachedHero.IsDead || cachedHero.IsRespawning))
            return false;

        if (GameServices.TryGet(out RunDeathController runDeath) && runDeath.IsDeathInProgress)
            return false;

        return true;
    }

    private void Update()
    {
        if (!MainSceneGameplayGate.IsReady || !autoRollActive || isRolling)
            return;

        if (Time.time >= autoRollNextCheckTime)
        {
            if (CanRoll())
            {
                RollDice();
                autoRollNextCheckTime = Time.time + autoRollCheckInterval;
            }
        }
    }

    [ContextMenu("Roll Dice")]
    public void RollDice()
    {
        if (!EnsureGameServicesReady(nameof(RollDice)))
            return;

        if (!CanRoll())
            return;

        if (rollCoroutine != null)
            StopCoroutine(rollCoroutine);
        rollCoroutine = StartCoroutine(RollRoutine());
    }

    private IEnumerator RollRoutine()
    {
        if (isRolling)
            yield break;

        isRolling = true;

        try
        {
            GlobalSettings.LogGameplay("DiceSpawner: Starting Roll Routine.");

            var settings = GlobalSettings.Instance;
            if (GameServices.TryGet(out EnergyManager energy) && settings != null)
                energy.Deplete(settings.energyDepletionPerRoll);

            if (cachedHero == null)
                cachedHero = GameServices.Hero;
            var hero = cachedHero;

            if (hero != null)
            {
                hero.TickBurnEffect();
                hero.TickPoisonEffect();
            }

            Transform aim = GetRollAimTransform(hero);

            if (hero != null && !hero.InCombat)
            {
                var steveAnim = hero.GetComponent<SteveAnimator>();
                steveAnim?.PlayThrow();
                yield return new WaitForSeconds(0.2f);
            }

            var existingDice = Object.FindObjectsByType<DieResult>(FindObjectsInactive.Exclude);
            foreach (var d in existingDice)
            {
                if (d != null && d.gameObject != null)
                    Destroy(d.gameObject);
            }

            activeDice.Clear();
            EnsureReferences();

            if (d6Prefab == null)
            {
                Debug.LogError(
                    $"DiceSpawner: d6Prefab is missing. Assign {DefaultD6PrefabPath} on DiceSpawner in the scene.");
                yield break;
            }

            Collider heroCollider = hero != null ? hero.GetComponent<Collider>() : null;

            int diceCount = (hero != null && hero.IsCursed) ? 1 : 2;
            if (hero != null && hero.IsCursed)
                CombatLog.Info("<color=purple>Steve is CURSED! Rolling only ONE die.</color>");

            for (int i = 0; i < diceCount; i++)
            {
                Vector3 spawnOrigin = hero != null ? hero.transform.position + Vector3.up * 1.2f : spawnPoint.position;
                Vector3 offset = new Vector3(i * 0.2f - 0.1f, 0.2f, 0.5f);
                Vector3 forward = aim.forward;
                Vector3 right = aim.right;
                GameObject die = Instantiate(
                    d6Prefab,
                    spawnOrigin + (forward * 0.2f) + offset,
                    Random.rotation);

                SetLayerRecursive(die, 8);

                if (die.GetComponent<DieResult>() == null)
                    die.AddComponent<DieResult>();
                activeDice.Add(die);

                Collider dieCollider = die.GetComponent<Collider>();
                if (dieCollider != null && heroCollider != null)
                    Physics.IgnoreCollision(dieCollider, heroCollider);

                Rigidbody rb = die.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;

                    Vector3 throwDir = (forward * 0.6f + right * Random.Range(-0.2f, 0.2f) + Vector3.up * 0.1f).normalized;
                    Vector3 force = (throwDir * throwForce) + (Vector3.up * 1.5f);
                    rb.AddForce(force, ForceMode.Impulse);
                    rb.AddTorque(Random.onUnitSphere * torque, ForceMode.Impulse);
                }
            }

            bool combatRoll = hero != null && hero.InCombat;
            yield return new WaitForSeconds(combatRoll ? 0.1f : 0.2f);

            float settleTimeout = combatRoll ? 1.25f : 3.0f;
            bool allSettled = false;
            while (settleTimeout > 0)
            {
                allSettled = true;
                foreach (var d in activeDice)
                {
                    if (d == null)
                        continue;

                    var res = d.GetComponent<DieResult>();
                    if (res != null && !res.IsSettled())
                    {
                        allSettled = false;
                        break;
                    }
                }

                if (allSettled)
                {
                    yield return new WaitForSeconds(0.1f);
                    allSettled = true;
                    foreach (var d in activeDice)
                    {
                        if (d == null)
                            continue;

                        var res = d.GetComponent<DieResult>();
                        if (res == null || !res.IsSettled())
                        {
                            allSettled = false;
                            break;
                        }
                    }

                    if (allSettled)
                        break;
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
                if (d == null)
                    continue;

                var res = d.GetComponent<DieResult>();
                if (res == null)
                    continue;

                int val = res.GetValue();
                total += val;
                individual.Add(val);
            }

            LastRoll = total;
            LastIndividualRolls = string.Join(", ", individual);
            GlobalSettings.LogGameplay($"<color=green><b>[Final Dice Roll] {total}</b></color> (Individual: {LastIndividualRolls})");

            if (hero != null)
            {
                hero.TickCurseEffect();

                Enemy combatEnemy = hero.GetCurrentEnemy();

                bool handledCombat = false;
                if (hero.InCombat && hero.State == HeroController.CombatState.InCombat && combatEnemy != null && !combatEnemy.isDead)
                {
                    float readDelay = settings != null ? settings.combatDiceReadDelay : 0.35f;
                    yield return new WaitForSeconds(readDelay);

                    // Re-validate after delay (protects against death mid-roll or external state change)
                    if (hero.InCombat && hero.State == HeroController.CombatState.InCombat && combatEnemy != null && !combatEnemy.isDead)
                    {
                        bool isCrit;
                        int finalDamage = hero.CalculateRollDamage(total, out isCrit);
                        CombatLog.Info($"Dice combat turn | roll {total} → {finalDamage} damage{(isCrit ? " (CRIT)" : "")}");

                        yield return hero.HeroAttackRoutine(combatEnemy, finalDamage);

                        if (combatEnemy.isDead)
                        {
                            int levelsGained = 0;
                            if (GameServices.TryGet(out LevelManager levelManager))
                                levelsGained = levelManager.AddXP(total * 2);

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

                            handledCombat = true;
                            yield break;
                        }

                        if (settings != null)
                            yield return new WaitForSeconds(settings.combatReactionDelay);

                        if (hero.currentEnemy != null && combatEnemy != null && !combatEnemy.isDead && hero.InCombat)
                            combatEnemy.PerformAttack(hero);
                    }
                    handledCombat = true;
                }

                if (!handledCombat)
                {
                    if (hero.InCombat && combatEnemy == null)
                        CombatLog.Info("Dice roll ignored — no valid combat target on currentEnemy");

                    if (!hero.InCombat)
                    {
                        if (hero.IsDead || hero.IsRespawning)
                            yield break;

                        LogDiceMovementTarget(hero, total);
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
                        if (pending != null &&
                            (hero.TryBeginMeleeWithRoll(pending, total) ||
                             hero.TryBeginMeleeWithRoll(pending, total, engageExtraBuffer: 1.75f)))
                        {
                            while (hero.IsEngageBusy)
                                yield return null;
                        }
                    }
                }

                if (GameServices.TryGet(out LevelManager levels))
                {
                    int levelsGained = levels.AddXP(total);
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

    private static void LogDiceMovementTarget(HeroController hero, int rollTotal)
    {
        if (!GameServices.TryGet(out POIManager poiManager))
        {
            Debug.LogWarning("Dice resolved - Target POI: none (POIManager not registered).");
            return;
        }

        int visitOrder = 0;
        if (hero.TryGetComponent(out SteveMovement mv))
            visitOrder = mv.NextVisitPoiOrder;

        GameObject target = poiManager.GetNextPOITarget(visitOrder);
        string targetName = target != null ? target.name : "none";
        Debug.Log(
            $"Dice resolved - Target POI: {targetName} (roll {rollTotal}, visit POIs {poiManager.VisitPoiCount}, " +
            $"spawn targeting {(GameServices.TryGet(out SpawnManager spawn) && spawn.IsRandomVisitTargetingEnabled ? "on" : "off")})");
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        if (obj.GetComponent<Canvas>() != null ||
            obj.GetComponent<RectTransform>() != null ||
            obj.GetComponent<LineRenderer>() != null)
        {
            int target = obj.GetComponent<LineRenderer>() != null ? 0 : 5;
            SetLayerRecursiveInternal(obj, target);
            return;
        }

        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
            obj.layer = layer;
        else
            obj.layer = 0;

        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private void SetLayerRecursiveInternal(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursiveInternal(child.gameObject, layer);
    }
}
