using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

/// <summary>
/// Bootstrap for gameplay services — runs before other scripts, holds references, no FindAnyObjectByType in hot paths.
/// Place on a root object (e.g. GameSystems) with managers as children or assign references in the Inspector.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
[AddComponentMenu("FatesRoll/Game Services")]
public class GameServices : MonoBehaviour
{
    public static GameServices Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnSubsystemRegistration()
    {
        ResetDomainStatics();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStaticsBeforeSceneLoad()
    {
        ResetDomainStatics();
    }

    private static void ResetDomainStatics()
    {
        Current = null;
    }

    [Tooltip("Survive scene loads and absorb bootstrap objects from newly loaded scenes.")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("Startup")]
    [Tooltip("Defer child discovery and validation until after the first frame (reduces Awake spike).")]
    [SerializeField] private bool deferHeavyBootstrap = true;

    [Tooltip("Optional cap while testing first-frame hitches (0 = leave Unity default).")]
    [SerializeField] private int targetFrameRateOnStart;

    [Tooltip("When references are empty, discover services under this object only (not a full-scene search).")]
    [SerializeField] private bool discoverOnChildren = true;

    [Header("Services (optional — auto-filled from children when empty)")]
    [SerializeField] private GlobalSettings globalSettings;
    [SerializeField] private DiceSpawner diceSpawner;
    [SerializeField] private POIManager poiManager;
    [SerializeField] private SpawnManager spawnManager;
    [SerializeField] private EnergyManager energyManager;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private LootManager lootManager;
    [SerializeField] private EquipmentLootManager equipmentLootManager;
    [SerializeField] private RogueLiteManager rogueLiteManager;
    [SerializeField] private TalentManager talentManager;
    [SerializeField] private QuestManager questManager;
    [SerializeField] private RunDeathController runDeathController;
    [SerializeField] private EnemyStatManager enemyStatManager;
    [SerializeField] private EnemySpecialController enemySpecialController;

    [Header("Hero")]
    [SerializeField] private HeroController hero;
    [SerializeField] private HeroSpawnPoint heroSpawnPoint;

    private readonly Dictionary<Type, object> registry = new Dictionary<Type, object>();
    private bool requiredServicesValidated;

    /// <summary>Steve / player hero (null until <see cref="HeroController"/> Awake registers).</summary>
    public static HeroController Hero
    {
        get
        {
            PurgeStaleCurrent();
            if (Current == null)
                return null;
            if (IsUnityObjectAlive(Current.hero))
                return Current.hero;
            Current.hero = null;
            return TryGet(out HeroController resolved) ? resolved : null;
        }
    }

    /// <summary>Steve — alias for <see cref="Hero"/>.</summary>
    public static HeroController HeroController => Hero;

    /// <summary>True when the bootstrap has finished Awake and <see cref="Current"/> is valid.</summary>
    public static bool IsInitialized
    {
        get
        {
            PurgeStaleCurrent();
            return Current != null;
        }
    }

    public static HeroSpawnPoint HeroSpawn
    {
        get
        {
            PurgeStaleCurrent();
            if (Current == null)
                return null;
            return IsUnityObjectAlive(Current.heroSpawnPoint) ? Current.heroSpawnPoint : null;
        }
    }

    private void Awake()
    {
        Debug.Log($"[GameServices] Awake on {gameObject.name}");
        if (Current != null && Current != this)
        {
            Debug.Log($"[GameServices] Found existing Current: {Current.name}. Absorbing and destroying self.");
            Current.AbsorbBootstrap(this);
            Destroy(gameObject);
            return;
        }

        Current = this;
        Debug.Log($"[GameServices] I am now Current. Initializing...");
        registry.Clear();
        hero = null;
        heroSpawnPoint = null;

        if (persistAcrossScenes)
            PersistenceUtility.DontDestroyOnLoadRoot(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        if (deferHeavyBootstrap)
            StartCoroutine(DeferredBootstrapRoutine());
        else
            FinishBootstrap();
    }

    private IEnumerator DeferredBootstrapRoutine()
    {
        yield return null;
        FinishBootstrap();
    }

    private void FinishBootstrap()
    {
        if (targetFrameRateOnStart > 0)
            Application.targetFrameRate = targetFrameRateOnStart;

        ResolveReferences();
        EnsureQuestManagerOnBootstrap();
        PublishInspectorReferences();
    }

    private void Start()
    {
        ResolveReferences();
        EnsureQuestManagerOnBootstrap();
        PublishInspectorReferences();
        ValidateRequiredServicesOnce();
    }

    /// <summary>Single QuestManager under this bootstrap (created here only if the scene has none).</summary>
    private void EnsureQuestManagerOnBootstrap()
    {
        questManager ??= GetComponentInChildren<QuestManager>(true);
        if (questManager != null)
            return;

        var go = new GameObject("QuestManager");
        go.transform.SetParent(transform, false);
        questManager = go.AddComponent<QuestManager>();
        GlobalSettings.LogGameplay("GameServices: added QuestManager under GameServices (assign in Bootstrap.unity to keep it in the scene).");
    }

    private void ValidateRequiredServicesOnce()
    {
        if (requiredServicesValidated)
            return;

        requiredServicesValidated = true;
        ValidateRequiredServices();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Current != this)
            return;

        Current = null;
        registry.Clear();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Current != this)
            return;

        RefreshHeroSpawnFromChildren();
        ResolveReferences();
        EnsureQuestManagerOnBootstrap();
        PublishInspectorReferences();
        TryRegisterHeroFromScene(scene);
    }

    /// <summary>Strict lookup — throws if the service was never registered in Awake.</summary>
    public static T Get<T>() where T : class => GetRequired<T>();

    public static T GetRequired<T>() where T : class
    {
        if (TryGet(out T service))
            return service;

        throw BuildMissingServiceException(typeof(T));
    }

    public static bool TryGet<T>(out T service) where T : class
    {
        service = null;
        PurgeStaleCurrent();
        if (Current == null)
            return false;

        if (!Current.registry.TryGetValue(typeof(T), out object entry))
            return false;

        if (!IsAlive(entry))
        {
            Current.registry.Remove(typeof(T));
            return false;
        }

        service = entry as T;
        return service != null;
    }

    private static void PurgeStaleCurrent()
    {
        if (Current is UnityEngine.Object unityObject && unityObject == null)
            Current = null;
    }

    private static bool IsAlive(object entry)
    {
        if (entry == null)
            return false;
        return entry is not UnityEngine.Object unityObject || unityObject != null;
    }

    private static bool IsUnityObjectAlive(UnityEngine.Object obj) => obj != null;

    public static void Register<T>(T service) where T : class
    {
        if (service == null || Current == null)
            return;

        var type = typeof(T);
        if (Current.registry.TryGetValue(type, out object existing))
        {
            if (ReferenceEquals(existing, service))
                return;

            Debug.LogError(
                $"GameServices: duplicate {type.Name} registration. " +
                $"Already registered: '{existing}'. Attempted: '{service}'. " +
                "Remove the extra component from the scene or bootstrap hierarchy.",
                Current);
            return;
        }

        Current.registry[type] = service;
        Current.AssignInspectorField(service);
    }

    public static void Unregister<T>(T service) where T : class
    {
        if (service == null || Current == null)
            return;

        var type = typeof(T);
        if (Current.registry.TryGetValue(type, out object existing) && ReferenceEquals(existing, service))
            Current.registry.Remove(type);
    }

    /// <summary>Called from <see cref="HeroController"/> Awake — registers Steve in the service registry.</summary>
    public static void RegisterHero(HeroController controller)
    {
        if (controller == null)
            return;

        if (Current == null)
        {
            Debug.LogWarning(
                "GameServices.RegisterHero: bootstrap not ready yet. Add GameServices to the scene (FatesRoll → Setup → Add Game Services Bootstrap).",
                controller);
            return;
        }

        Current.hero = controller;
        Register(controller);

        CinemachineCamera vcam = UnityEngine.Object.FindAnyObjectByType<CinemachineCamera>();
        if (vcam != null)
            vcam.Follow = controller.transform;
    }

    public static void UnregisterHero(HeroController controller)
    {
        if (Current == null)
            return;

        if (Current.hero == controller)
            Current.hero = null;
        Unregister(controller);
    }

    private void AbsorbBootstrap(GameServices incoming)
    {
        if (incoming == null)
            return;

        // Discovery without auto-registration to Current
        incoming.ResolveReferences();
        
        // Only take what we are missing
        if (globalSettings == null) globalSettings = incoming.globalSettings;
        if (diceSpawner == null) diceSpawner = incoming.diceSpawner;
        if (poiManager == null) poiManager = incoming.poiManager;
        if (spawnManager == null) spawnManager = incoming.spawnManager;
        if (energyManager == null) energyManager = incoming.energyManager;
        if (levelManager == null) levelManager = incoming.levelManager;
        if (lootManager == null) lootManager = incoming.lootManager;
        if (equipmentLootManager == null) equipmentLootManager = incoming.equipmentLootManager;
        if (rogueLiteManager == null) rogueLiteManager = incoming.rogueLiteManager;
        if (talentManager == null) talentManager = incoming.talentManager;
        if (questManager == null) questManager = incoming.questManager;
        if (runDeathController == null) runDeathController = incoming.runDeathController;
        if (enemyStatManager == null) enemyStatManager = incoming.enemyStatManager;
        if (enemySpecialController == null) enemySpecialController = incoming.enemySpecialController;

        PublishInspectorReferences();
        RefreshHeroSpawnFromChildren();
    }

    private void RefreshHeroSpawnFromChildren()
    {
        if (heroSpawnPoint != null)
            return;

        heroSpawnPoint = GetComponentInChildren<HeroSpawnPoint>(true);
        if (heroSpawnPoint != null)
            Register(heroSpawnPoint);
    }

    private void ResolveReferences()
    {
        if (!discoverOnChildren)
            return;

        globalSettings ??= GetComponentInChildren<GlobalSettings>(true);
        diceSpawner ??= GetComponentInChildren<DiceSpawner>(true);
        poiManager ??= GetComponentInChildren<POIManager>(true);
        spawnManager ??= GetComponentInChildren<SpawnManager>(true);
        energyManager ??= GetComponentInChildren<EnergyManager>(true);
        levelManager ??= GetComponentInChildren<LevelManager>(true);
        lootManager ??= GetComponentInChildren<LootManager>(true);
        equipmentLootManager ??= GetComponentInChildren<EquipmentLootManager>(true);
        rogueLiteManager ??= GetComponentInChildren<RogueLiteManager>(true);
        talentManager ??= GetComponentInChildren<TalentManager>(true);
        questManager ??= GetComponentInChildren<QuestManager>(true);
        runDeathController ??= GetComponentInChildren<RunDeathController>(true);
        enemyStatManager ??= GetComponentInChildren<EnemyStatManager>(true);
        enemySpecialController ??= GetComponentInChildren<EnemySpecialController>(true);
        hero ??= GetComponentInChildren<HeroController>(true);
        heroSpawnPoint ??= GetComponentInChildren<HeroSpawnPoint>(true);
    }

    private void PublishInspectorReferences()
    {
        RegisterIfPresent(globalSettings);
        RegisterIfPresent(diceSpawner);
        RegisterIfPresent(poiManager);
        RegisterIfPresent(spawnManager);
        RegisterIfPresent(energyManager);
        RegisterIfPresent(levelManager);
        RegisterIfPresent(lootManager);
        RegisterIfPresent(equipmentLootManager);
        RegisterIfPresent(rogueLiteManager);
        RegisterIfPresent(talentManager);
        RegisterIfPresent(questManager);
        RegisterIfPresent(runDeathController);
        RegisterIfPresent(enemyStatManager);
        RegisterIfPresent(enemySpecialController);
        RegisterIfPresent(hero);
        RegisterIfPresent(heroSpawnPoint);
    }

    private void RegisterIfPresent<T>(T service) where T : class
    {
        if (service != null)
            Register(service);
    }

    private void AssignInspectorField<T>(T service) where T : class
    {
        switch (service)
        {
            case GlobalSettings gs: globalSettings = gs; break;
            case DiceSpawner ds: diceSpawner = ds; break;
            case POIManager pm: poiManager = pm; break;
            case SpawnManager sm: spawnManager = sm; break;
            case EnergyManager em: energyManager = em; break;
            case LevelManager lm: levelManager = lm; break;
            case LootManager loot: lootManager = loot; break;
            case EquipmentLootManager elm: equipmentLootManager = elm; break;
            case RogueLiteManager rlm: rogueLiteManager = rlm; break;
            case TalentManager tm: talentManager = tm; break;
            case QuestManager qm: questManager = qm; break;
            case RunDeathController rdc: runDeathController = rdc; break;
            case EnemyStatManager esm: enemyStatManager = esm; break;
            case EnemySpecialController esc: enemySpecialController = esc; break;
            case HeroController h: hero = h; break;
            case HeroSpawnPoint sp: heroSpawnPoint = sp; break;
        }
    }

    /// <summary>Bootstrap-scoped services only. Hero lives in the gameplay scene and registers via <see cref="RegisterHero"/>.</summary>
    private void ValidateRequiredServices()
    {
        if (!TryGet<GlobalSettings>(out _))
            Debug.LogWarning("GameServices: GlobalSettings missing. Add one under the bootstrap object.", this);
        if (!TryGet<DiceSpawner>(out _))
            Debug.LogWarning("GameServices: DiceSpawner missing.", this);
        if (!TryGet<RunDeathController>(out _))
            Debug.LogWarning("GameServices: RunDeathController missing.", this);
        if (!TryGet<QuestManager>(out _))
            Debug.LogWarning("GameServices: QuestManager missing. Add a QuestManager child under GameServices in Bootstrap.unity.", this);
    }

    private static void TryRegisterHeroFromScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || Current == null)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var controller = root.GetComponentInChildren<HeroController>(true);
            if (controller == null)
                continue;

            RegisterHero(controller);
            return;
        }
    }

    private static InvalidOperationException BuildMissingServiceException(Type type)
    {
        string bootstrapHint = Current == null
            ? "No GameServices bootstrap is active. Add one via FatesRoll → Setup → Add Game Services Bootstrap."
            : "Ensure the service component is under the GameServices object (or assigned in the Inspector) " +
              "and that its Awake runs after GameServices (DefaultExecutionOrder -10000).";

        return new InvalidOperationException(
            $"GameServices: required service '{type.Name}' is not registered. {bootstrapHint}");
    }
}
