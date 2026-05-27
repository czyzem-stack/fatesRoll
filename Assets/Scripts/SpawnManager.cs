using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Populates SpawnNode markers with random enemies on load (world ambience during FTUE).
/// Steve targets spawn nodes only after all visit POIs are consumed.
/// </summary>
/// <remarks>Inherits <see cref="GameServiceBehaviour{T}"/> — auto-registers in Awake via <see cref="GameServices"/>.</remarks>
[DefaultExecutionOrder(-40)]
public class SpawnManager : GameServiceBehaviour<SpawnManager>
{

    [SerializeField] private POIManager poiManager;
    [SerializeField] private MonsterPrefabCatalog monsterCatalog;
    [SerializeField] private EnemyStatManager statManager;

    [Header("Random spawns")]
    [Tooltip("Fill spawn nodes on play start (even while visit-order FTUE is active).")]
    [SerializeField] private bool fillSpawnsOnLoad = true;

    [SerializeField] private int maxActiveEncounters = 25;
    [SerializeField] private int enemiesKilledPerRespawn = 3;

    [Header("Treasure chests")]
    [Tooltip("On monster spawn nodes, chance to spawn a treasure chest instead.")]
    [SerializeField] [Range(0f, 1f)] private float spawnChestChance = 0.12f;

    [Header("Hero (main scene)")]
    [Tooltip("Steve prefab (Assets/Heroes/Prefab/Steve.prefab). Used only if no HeroController exists when main loads.")]
    [SerializeField] private GameObject heroPrefab;
    [SerializeField] private string gameplaySceneName = MainSceneGameplayGate.DefaultMainSceneName;
    [SerializeField] private HeroSpawnPoint heroSpawnPoint;

    private readonly List<SpawnNode> allSpawnNodes = new List<SpawnNode>();
    private readonly List<SpawnNode> activeSpawnNodes = new List<SpawnNode>();
    private int randomPoolKillCounter;
    private bool spawnPoolInitialized;
    private bool randomVisitTargetingEnabled;

    public bool HasInitialized { get; private set; }
    public bool IsRandomVisitTargetingEnabled => randomVisitTargetingEnabled;
    public MonsterPrefabCatalog MonsterCatalog => monsterCatalog;

    protected override void Awake()
    {
        base.Awake();
        ResolveManagerReferences();
    }

    private void ResolveManagerReferences()
    {
        if (poiManager == null && GameServices.TryGet(out POIManager poi))
            poiManager = poi;
        if (statManager == null && GameServices.TryGet(out EnemyStatManager stats))
            statManager = stats;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    protected override void Start()
    {
        base.Start();
        TryRefreshMainScene(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryRefreshMainScene(scene);
    }

    private Coroutine refreshRoutine;

    private void TryRefreshMainScene(Scene scene)
    {
        if (!IsGameplayScene(scene))
            return;

        if (refreshRoutine != null)
            StopCoroutine(refreshRoutine);

        refreshRoutine = StartCoroutine(RefreshMainSceneRoutine(scene));
    }

    private IEnumerator RefreshMainSceneRoutine(Scene scene)
    {
        yield return new WaitUntil(() => GameServices.IsInitialized);
        yield return null;

        yield return EnsureHeroRegistered(scene);

        ResolveManagerReferences();

        if (GameServices.TryGet(out POIManager poiMgr))
        {
            if (!poiMgr.HasInitialized || poiMgr.VisitPoiCount == 0)
                poiMgr.RefreshFromScene();

            float poiWait = 8f;
            while (poiWait > 0f && !poiMgr.HasInitialized)
            {
                poiWait -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        yield return null; // wait one frame for SpawnNode scene objects to settle
        InitializeSpawnNodes();
        refreshRoutine = null;
    }

    private IEnumerator EnsureHeroRegistered(Scene scene)
    {
        if (GameServices.Hero != null)
            yield break;

        HeroController existing = FindHeroInScene(scene);
        if (existing != null)
        {
            GameServices.RegisterHero(existing);
            GlobalSettings.LogGameplay("SpawnManager: registered Steve from main scene.");
            yield break;
        }

        if (heroPrefab == null)
        {
            Debug.LogError(
                "SpawnManager: Steve is missing in main and heroPrefab is not assigned. " +
                "Run FatesRoll → Setup → Restore Steve In Main Scene.",
                this);
            yield break;
        }

        if (!TryResolveHeroSpawn(scene, out Vector3 position, out Quaternion rotation))
        {
            Debug.LogError("SpawnManager: could not resolve a spawn position for Steve.", this);
            yield break;
        }

        GameObject steveObject = Instantiate(heroPrefab, position, rotation);
        steveObject.name = "Steve";
        if (steveObject.scene != scene)
            SceneManager.MoveGameObjectToScene(steveObject, scene);

        HeroController hero = steveObject.GetComponent<HeroController>();
        if (hero == null)
            hero = steveObject.AddComponent<HeroController>();

        GameServices.RegisterHero(hero);
        GlobalSettings.LogGameplay("SpawnManager: instantiated Steve in main (heroPrefab fallback).");
        yield return null;
    }

    private bool IsGameplayScene(Scene scene) =>
        scene.IsValid() &&
        scene.name.Equals(gameplaySceneName, System.StringComparison.OrdinalIgnoreCase);

    private HeroController FindHeroInScene(Scene scene)
    {
        if (!scene.IsValid())
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null)
                continue;

            if (root.TryGetComponent(out HeroController onRoot))
                return onRoot;

            HeroController inChildren = root.GetComponentInChildren<HeroController>(true);
            if (inChildren != null)
                return inChildren;
        }

        return null;
    }

    private bool TryResolveHeroSpawn(Scene scene, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (heroSpawnPoint == null)
            heroSpawnPoint = FindHeroSpawnInScene(scene);
        if (heroSpawnPoint == null)
            heroSpawnPoint = GameServices.HeroSpawn;

        if (heroSpawnPoint != null)
        {
            heroSpawnPoint.SnapToPlaySpawnSurface();
            position = heroSpawnPoint.transform.position;
            rotation = heroSpawnPoint.transform.rotation;
            return true;
        }

        if (HeroSpawnUtility.TryResolveSpawnPosition(position, out Vector3 resolved))
        {
            position = resolved;
            return true;
        }

        return false;
    }

    private static HeroSpawnPoint FindHeroSpawnInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var marker = root.GetComponentInChildren<HeroSpawnPoint>(true);
            if (marker != null)
                return marker;
        }

        return null;
    }

    public void InitializeSpawnNodes()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!IsGameplayScene(activeScene))
        {
            Debug.Log(
                $"SpawnManager: InitializeSpawnNodes skipped — active scene is '{activeScene.name}', expected '{gameplaySceneName}'.");
            return;
        }

        ResolveManagerReferences();

        activeSpawnNodes.Clear();
        allSpawnNodes.Clear();
        randomPoolKillCounter = 0;
        spawnPoolInitialized = false;
        randomVisitTargetingEnabled = false;
        HasInitialized = false;

        if (monsterCatalog == null)
        {
            Debug.LogError(
                "SpawnManager: MonsterPrefabCatalog is not assigned. " +
                "Assign Assets/Data/Monsters/MonsterPrefabCatalog.asset on the Bootstrap SpawnManager.",
                this);
            return;
        }

        SpawnNode[] foundAll = Object.FindObjectsByType<SpawnNode>(FindObjectsInactive.Include);
        List<SpawnNode> spawnNodes = foundAll
            .Where(n => n != null &&
                        n.gameObject.scene == activeScene &&
                        n.gameObject.activeInHierarchy)
            .ToList();

        Debug.Log(
            $"SpawnManager: Found {foundAll.Length} total SpawnNode components. {spawnNodes.Count} active in main scene.");
        foreach (SpawnNode node in spawnNodes.Take(5))
        {
            Debug.Log(
                $"→ SpawnNode: {node.name} | Position: {node.transform.position} | Active: {node.gameObject.activeInHierarchy}");
        }

        allSpawnNodes.AddRange(spawnNodes);

        spawnPoolInitialized = true;
        HasInitialized = true;

        if (fillSpawnsOnLoad)
            FillSpawnNodes();

        GlobalSettings.LogGameplay(
            $"SpawnManager: {allSpawnNodes.Count} spawn node(s) in {gameplaySceneName}, " +
            $"{activeSpawnNodes.Count} active encounter(s).");

        if (allSpawnNodes.Count == 0)
            Debug.LogWarning($"SpawnManager: no SpawnNode markers found in {gameplaySceneName}.");

        if (GameServices.TryGet(out POIManager poi) &&
            poi.VisitPoiCount > 0 &&
            !poi.HasRemainingVisitPOI())
        {
            EnableRandomVisitTargeting();
        }
    }

    /// <summary>Steve may path to spawn encounters when no visit POIs remain.</summary>
    public void EnableRandomVisitTargeting()
    {
        if (randomVisitTargetingEnabled) return;
        randomVisitTargetingEnabled = true;
        FillSpawnNodes();
        GlobalSettings.LogGameplay("SpawnManager: visit POIs complete — Steve can target random spawns.");
    }

    /// <summary>Legacy name used by POIManager.</summary>
    public void EnableRandomSpawner() => EnableRandomVisitTargeting();

    private int GetSpawnBudget()
    {
        int visitActive = 0;
        if (GameServices.TryGet(out POIManager poi))
            visitActive = poi.ActiveVisitCount;
        return Mathf.Max(0, maxActiveEncounters - visitActive);
    }

    private void FillSpawnNodes()
    {
        if (!spawnPoolInitialized)
            return;

        int visitActive = 0;
        if (GameServices.TryGet(out POIManager poi))
            visitActive = poi.ActiveVisitCount;

        int budget = GetSpawnBudget();
        Debug.Log(
            $"SpawnManager: FillSpawnNodes budget={budget} " +
            $"(maxActiveEncounters={maxActiveEncounters}, visitPOIsActive={visitActive}, fillSpawnsOnLoad={fillSpawnsOnLoad}).");

        int spawned = 0;
        while (activeSpawnNodes.Count < budget)
        {
            SpawnNode node = PickInactiveNode();
            if (node == null)
            {
                Debug.LogWarning(
                    $"SpawnManager: FillSpawnNodes stopped — no free nodes " +
                    $"(encounters={activeSpawnNodes.Count}, budget={budget}).");
                break;
            }

            Debug.Log($"SpawnManager: activating '{node.name}' at {node.transform.position}");
            ActivateNode(node);
            spawned++;
        }

        Debug.Log($"SpawnManager: FillSpawnNodes done — activated {spawned}, total encounters={activeSpawnNodes.Count}.");
    }

    private SpawnNode PickInactiveNode()
    {
        var candidates = allSpawnNodes
            .Where(n => n != null && !n.IsOccupied)
            .ToList();
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    public void RegisterActiveNode(SpawnNode node)
    {
        if (node == null || activeSpawnNodes.Contains(node)) return;
        activeSpawnNodes.Add(node);
    }

    public void UnregisterActiveNode(SpawnNode node)
    {
        activeSpawnNodes.Remove(node);
    }

    public void ActivateNode(SpawnNode node)
    {
        if (node == null || !spawnPoolInitialized)
            return;

        if (monsterCatalog == null)
        {
            Debug.LogError("SpawnManager: ActivateNode failed — MonsterPrefabCatalog is not assigned.", this);
            return;
        }

        if (activeSpawnNodes.Count >= GetSpawnBudget() && !activeSpawnNodes.Contains(node))
            return;

        GameObject encounter;
        if (ShouldSpawnChest(node))
        {
            encounter = SpawnEncounterBuilder.BuildChest(node);
        }
        else
        {
            POIType pick = PickRandomType(node.GetLastSpawnedType());
            encounter = SpawnEncounterBuilder.Build(node, pick, monsterCatalog);
            if (encounter == null)
            {
                Debug.LogError($"SpawnManager: failed to build encounter for '{node.name}' (type={pick}).", node);
                return;
            }

            var enemy = encounter.GetComponent<Enemy>();
            if (enemy != null)
            {
                if (statManager != null)
                    statManager.ApplyScaledStats(enemy);
                else if (GameServices.TryGet(out EnemyStatManager fallbackStats))
                {
                    statManager = fallbackStats;
                    statManager.ApplyScaledStats(enemy);
                }
                else
                    enemy.Initialize();
            }
        }

        if (encounter == null)
        {
            Debug.LogError($"SpawnManager: ActivateNode produced null encounter on '{node.name}'.", node);
            return;
        }

        if (!activeSpawnNodes.Contains(node))
            RegisterActiveNode(node);

        Debug.Log($"SpawnManager: encounter ready on '{node.name}' → {encounter.name}", encounter);
    }

    private bool ShouldSpawnChest(SpawnNode node)
    {
        if (node == null) return false;
        if (node.spawnKind == SpawnNodeKind.TreasureChest)
            return true;
        return spawnChestChance > 0f && Random.value < spawnChestChance;
    }

    public static POIType PickRandomType(POIType? exclude)
    {
        var pool = new List<POIType>(MonsterPOIDefinitions.CombatTypes);
        if (exclude.HasValue)
            pool.RemoveAll(t => t == exclude.Value);
        if (pool.Count == 0)
            pool = new List<POIType>(MonsterPOIDefinitions.CombatTypes);
        return pool[Random.Range(0, pool.Count)];
    }

    public void ResolveEncounter(SpawnNode node)
    {
        if (node == null || !spawnPoolInitialized) return;

        UnregisterActiveNode(node);
        node.ClearEncounter();
        randomPoolKillCounter++;

        if (statManager != null)
            statManager.NotifyRandomPoolKill();

        if (randomPoolKillCounter >= enemiesKilledPerRespawn)
        {
            randomPoolKillCounter = 0;
            var respawn = PickInactiveNode();
            if (respawn != null)
                ActivateNode(respawn);
        }
    }

    public void ResolveEncounterFromEnemy(GameObject enemyObject)
    {
        if (enemyObject == null) return;
        var node = enemyObject.GetComponentInParent<SpawnNode>();
        if (node != null)
            ResolveEncounter(node);
    }

    public GameObject GetRandomSpawnTarget()
    {
        if (!randomVisitTargetingEnabled) return null;

        var occupied = new List<SpawnNode>();
        foreach (var node in activeSpawnNodes)
        {
            if (node == null || !node.IsOccupied) continue;
            var enemy = node.GetActiveEnemy();
            if (enemy != null && !enemy.isDead)
                occupied.Add(node);
        }

        if (occupied.Count == 0) return null;
        return occupied[Random.Range(0, occupied.Count)].activeEncounter;
    }

    /// <summary>Clears random spawns and refills at base difficulty (after Steve dies).</summary>
    public void ResetRunEncounters()
    {
        if (!spawnPoolInitialized) return;

        foreach (var node in allSpawnNodes)
        {
            if (node == null) continue;
            UnregisterActiveNode(node);
            node.ClearEncounter();
        }

        activeSpawnNodes.Clear();
        randomPoolKillCounter = 0;
        FillSpawnNodes();
        GlobalSettings.LogGameplay("SpawnManager: random encounters reset after Steve respawn.");
    }

    public GameObject GetNearestSpawn(Vector3 position)
    {
        if (!spawnPoolInitialized) return null;

        SpawnNode nearest = null;
        float minDist = float.MaxValue;
        foreach (var node in activeSpawnNodes)
        {
            if (node == null || !node.IsOccupied) continue;
            float d = Vector3.Distance(position, node.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = node;
            }
        }

        return nearest != null ? nearest.activeEncounter : null;
    }
}
