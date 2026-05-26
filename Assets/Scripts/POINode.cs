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

        if (POIManager.Instance != null && POIManager.Instance.HasInitialized)
        {
            if (gameObject.activeInHierarchy)
            {
                POIManager.Instance.RegisterPOI(this);
                InitializeEnemy();
            }
            return;
        }

        if (gameObject.activeInHierarchy && POIManager.Instance != null)
        {
            POIManager.Instance.RegisterPOI(this);
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
        else if (EnemyStatManager.Instance != null)
            EnemyStatManager.Instance.ApplyFtueStepStats(enemy, order);
        else
            enemy.Initialize();
    }

    void OnDestroy()
    {
        if (POIManager.Instance != null)
            POIManager.Instance.UnregisterPOI(this);
    }
}
