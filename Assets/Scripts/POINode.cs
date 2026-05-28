using UnityEngine;

public enum POIType
{
    Orc,
    Skeleton,
    Slime,
    Bat,
    Dragon,
    EvilMage,
    Golem,
    MonsterPlant,
    Spider,
    TurtleShell,
    TreasureChest
}

public class POINode : MonoBehaviour
{
    public POIType type = POIType.Orc;
    public int order = 0;

    [HideInInspector] public GameObject currentVisual;
    [HideInInspector] public GameObject monsterVisualPrefab;
    [HideInInspector] public GameObject healthBarPrefab;

    [Tooltip("Optional tuning asset; otherwise EnemyStatManager scales by visit order.")]
    public EnemyData enemyData;

    [Header("Base Stats (Manual Override)")]
    public bool useManualStats;
    public float baseStrength = 8f;
    public float baseAgility = 8f;
    public float baseVitality = 8f;
    public float baseLuck = 5f;

    [Header("Treasure chest")]
    public bool isTreasureChest;
    public int ftueLootIndex = -1;
    public EquipmentItemDefinition ftueForcedOptionA;
    public EquipmentItemDefinition ftueForcedOptionB;

    public bool IsTreasureChest => isTreasureChest || type == POIType.TreasureChest;

    void Awake()
    {
        gameObject.tag = "POI";
    }

    void Start()
    {
        if (IsTreasureChest && currentVisual != null)
            PoiVisualPlacer.PlaceTreasureChestVisual(transform, currentVisual);

        if (GameServices.TryGet(out POIManager manager) && manager.HasInitialized)
        {
            if (gameObject.activeInHierarchy)
            {
                manager.RegisterPOI(this);
                InitializeEnemy();
            }
            return;
        }

        if (gameObject.activeInHierarchy && GameServices.TryGet(out POIManager fallback))
        {
            fallback.RegisterPOI(this);
            InitializeEnemy();
        }
    }

    public void InitializeEnemy()
    {
        Enemy enemy = GetComponent<Enemy>();
        if (enemy == null) return;

        if (IsTreasureChest)
        {
            enemy.ConfigureAsTreasureChest();
            return;
        }

        if (enemyData != null)
            enemy.InitializeFromData(enemyData);
        else if (useManualStats)
        {
            enemy.strength = baseStrength;
            enemy.agility = baseAgility;
            enemy.vitality = baseVitality;
            enemy.luck = baseLuck;
            enemy.Initialize();
        }
        else if (GameServices.TryGet(out EnemyStatManager statManager))
            statManager.ApplyFtueStepStats(enemy, order);
        else
            enemy.Initialize();
    }

    void OnDestroy()
    {
        if (GameServices.TryGet(out POIManager manager))
            manager.UnregisterPOI(this);
    }
}
