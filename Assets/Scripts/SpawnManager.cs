using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Populates SpawnNode markers with random enemies on load (world ambience during FTUE).
/// Steve targets spawn nodes only after all visit POIs are consumed.
/// </summary>
[DefaultExecutionOrder(-40)]
public class SpawnManager : MonoBehaviour
{
    private static SpawnManager _instance;
    public static SpawnManager Instance
    {
        get
        {
            if (_instance == null) _instance = Object.FindAnyObjectByType<SpawnManager>();
            return _instance;
        }
    }

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

    private readonly List<SpawnNode> allSpawnNodes = new List<SpawnNode>();
    private readonly List<SpawnNode> activeSpawnNodes = new List<SpawnNode>();
    private int randomPoolKillCounter;
    private bool spawnPoolInitialized;
    private bool randomVisitTargetingEnabled;

    public bool HasInitialized { get; private set; }
    public bool IsRandomVisitTargetingEnabled => randomVisitTargetingEnabled;
    public MonsterPrefabCatalog MonsterCatalog => monsterCatalog;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (poiManager == null) poiManager = Object.FindAnyObjectByType<POIManager>();
        if (statManager == null) statManager = Object.FindAnyObjectByType<EnemyStatManager>();
    }

    private void Start()
    {
        InitializeSpawnNodes();
    }

    public void InitializeSpawnNodes()
    {
        activeSpawnNodes.Clear();
        allSpawnNodes.Clear();
        randomPoolKillCounter = 0;
        spawnPoolInitialized = false;
        randomVisitTargetingEnabled = false;

        if (monsterCatalog == null)
        {
            Debug.LogError("SpawnManager: assign MonsterPrefabCatalog (FatesRoll → Enemies → Build Monster Prefab Catalog).");
            return;
        }

        var found = Object.FindObjectsByType<SpawnNode>(FindObjectsInactive.Include);
        allSpawnNodes.AddRange(found.Where(n => n != null));

        spawnPoolInitialized = true;
        HasInitialized = true;

        if (fillSpawnsOnLoad)
            FillSpawnNodes();

        if (poiManager != null && !poiManager.HasRemainingVisitPOI())
            EnableRandomVisitTargeting();
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
        int visitActive = poiManager != null ? poiManager.ActiveVisitCount : 0;
        return Mathf.Max(0, maxActiveEncounters - visitActive);
    }

    private void FillSpawnNodes()
    {
        if (!spawnPoolInitialized) return;

        int budget = GetSpawnBudget();
        while (activeSpawnNodes.Count < budget)
        {
            var node = PickInactiveNode();
            if (node == null) break;
            ActivateNode(node);
        }
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
        if (node == null || monsterCatalog == null || !spawnPoolInitialized) return;

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
            if (encounter == null) return;

            var enemy = encounter.GetComponent<Enemy>();
            if (enemy != null)
            {
                if (statManager != null)
                    statManager.ApplyScaledStats(enemy);
                else
                    enemy.Initialize();
            }
        }

        if (encounter == null) return;

        if (!activeSpawnNodes.Contains(node))
            RegisterActiveNode(node);
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
