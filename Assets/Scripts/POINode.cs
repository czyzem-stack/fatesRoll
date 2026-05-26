using UnityEngine;

public enum POIType
{
    Orc,
    Skeleton,
    Slime,
    TreasureChest
}

public class POINode : MonoBehaviour
{
    public POIType type = POIType.Orc;
    public int order = 0;
    
    [HideInInspector]
    public GameObject currentVisual;

    [Tooltip("Optional tuning asset; overrides default Enemy stats when set.")]
    public EnemyData enemyData;

    [Header("Treasure chest")]
    [Tooltip("When true (or type is TreasureChest), defeat opens equipment loot instead of coin celebration.")]
    public bool isTreasureChest;

    [Tooltip("-1 = random weapon vs armor roll. 0+ marks this POI for FTUE sequencing in EquipmentLootManager.")]
    public int ftueLootIndex = -1;

    [Tooltip("Optional forced A/B picks for tutorial chests (leave empty for random).")]
    public EquipmentItemDefinition ftueForcedOptionA;
    public EquipmentItemDefinition ftueForcedOptionB;

    public bool IsTreasureChest => isTreasureChest || type == POIType.TreasureChest;

    void Awake()
    {
        gameObject.tag = "POI";
    }

    void Start()
    {
        if (POIManager.Instance != null)
        {
            POIManager.Instance.RegisterPOI(this);
        }

        if (IsTreasureChest && currentVisual != null)
            PoiVisualPlacer.PlaceTreasureChestVisual(transform, currentVisual);

        // Visuals should already be present from Editor or Spawn
        InitializeEnemy();
    }

    public void InitializeEnemy()
    {
        Enemy enemy = GetComponent<Enemy>();
        if (enemy == null) return;

        if (enemyData != null)
            enemy.InitializeFromData(enemyData);
        else
            enemy.Initialize();
    }

    void OnDestroy()
    {
        if (POIManager.Instance != null)
        {
            POIManager.Instance.UnregisterPOI(this);
        }
    }
}